﻿Imports Microsoft.VisualBasic
Imports DotNetNuke.Services.FileSystem
Imports DotNetNuke.Services.Search
Imports DotNetNuke.Entities.Modules
Imports Documents

#Region "Modules defining constant values"

' Controller global constants
Public Module DocumentsControllerConstants
    Public Const PortalSettingKey As String = "PortalSettings"

    ' Module control keys used in DNN extension definition
    Public Const ViewDocumentControlKey As String = "ViewDocument"
    Public Const AddEditDocumentControlKey As String = "AddEditDocument"
    Public Const DocumentSettingsControlKey As String = "DocumentSettings"
    Public Const DownloadDocumentControlKey As String = "DownloadDocument"

    ' Request param keys
    Public Const DocIdParamKey As String = "DocId"
    Public Const SearchWordsParamKey As String = "SearchWords"
End Module

' Folder constants
Public Module FolderConstants
    Public Const NoParentFolderId As Integer = -1
    Public Const ModuleFolderSettingKey As String = "RootFolder" 'cooresponds to a folder id in the settings table
    Public Const DocumentsModuleRootFolderName As String = "acDocuments"
End Module

'Document constants
Public Module DocumentConstants

    'Link types
    Public Const LinkTypeKeepFileForUpdate As Integer = -1
    Public Const LinkTypeUrl As Integer = 0
    Public Const LinkTypeYouTube As Integer = 1
    Public Const LinkTypeGoogleDoc As Integer = 2
    Public Const LinkTypePage As Integer = 3
    Public Const LinkTypeFile As Integer = 4

    'FileId
    'TODO: See if we can get rid of FileId -2 as we have FileType
    Public Const FileIdForLinks As Integer = -2 'All documents that are not a real uploaded file have that FileId.

End Module

#End Region ' Modules defining constant values

Public Class DocumentsController
    'Inherits ModuleSearchBase

#Region "Document Settings"

    'Returns a list of all folders in the portal
    Public Shared Function GetFolders() As List(Of AP_Documents_Folder)
        Dim d As New DocumentsDataContext()
        Return (From c In d.AP_Documents_Folders Where c.PortalId = GetPortalId()).ToList
    End Function

    'Returns the folder asked for
    Public Shared Function GetFolder(ByVal folderID As Integer) As AP_Documents_Folder
        Dim d As New DocumentsDataContext()
        Return (From c In d.AP_Documents_Folders Where c.PortalId = GetPortalId() And c.FolderId = folderID).First
    End Function

    'Returns child folders of specified folder, only looks one level down.
    Public Shared Function GetChildFolders(ByVal parentFolderID As Integer) As IQueryable(Of AP_Documents_Folder)
        Dim d As New DocumentsDataContext()
        Return From c In d.AP_Documents_Folders Where c.PortalId = GetPortalId() And c.ParentFolder = parentFolderID
    End Function

    'Saves in the database the newFolder
    Public Shared Sub InsertFolder(ByVal newFolder As String, ByVal parentFolderId As Integer)
        Dim d As New DocumentsDataContext()

        Dim insert As New AP_Documents_Folder
        insert.ParentFolder = parentFolderId
        insert.Name = newFolder
        insert.Description = ""
        insert.PortalId = GetPortalId()
        insert.Permission = (From c In d.AP_Documents_Folders Where c.FolderId = insert.ParentFolder Select c.Permission).First
        d.AP_Documents_Folders.InsertOnSubmit(insert)
        d.SubmitChanges()

    End Sub

    'Updates only the name of the folder
    Public Shared Sub UpdateFolder(ByVal newFolderName As String, ByVal folderId As Integer)
        Dim d As New DocumentsDataContext()

        'Get resource to update
        Dim folderToUpdate = (From c In d.AP_Documents_Folders Where c.PortalId = GetPortalId() And c.FolderId = folderId).First

        folderToUpdate.Name = newFolderName
        d.SubmitChanges()

    End Sub

    'Determines if newFolder already exists, respecting the path that newFolder is in
    Public Shared Function IsFolder(ByVal newFolder As String, ByVal parentFolderId As Integer, ByVal caseInsensitive As Boolean) As Boolean

        Dim d As New DocumentsDataContext()

        ' retrieve all folders with same parent folder as the newFolder and same name as the newFolder
        Dim foldersWithSameParentAndSameName As List(Of AP_Documents_Folder) = (From c In d.AP_Documents_Folders _
                                                           Where c.ParentFolder = parentFolderId _
                                                           And c.PortalId = GetPortalId() _
                                                           And c.Name = newFolder).ToList

        For Each folder In foldersWithSameParentAndSameName
            If (String.Compare(newFolder, folder.Name, caseInsensitive) = 0) Then
                'newFolder exists already
                Return True
            End If
        Next

        'newFolder does not exist
        Return False

    End Function

    'Determines if the folder corresponding to folderID is empty
    'Empty means without docs trashed, withougt docs not trashed and without any subfolders
    Public Shared Function IsFolderEmpty(ByVal folderID As Integer) As Boolean
        If (GetDocuments(folderID, True, False).Count > 0 Or GetChildFolders(folderID).Count > 0) Then
            Return False
        End If
        Return True
    End Function

    Public Shared Function HasParentFolder(ByVal folderID As Integer) As Boolean
        If (GetFolder(folderID).ParentFolder = NoParentFolderId) Then
            Return False
        End If
        Return True
    End Function

    Public Shared Sub DeleteFolder(ByVal folderID As Integer)
        Dim d As New DocumentsDataContext()

        Dim deleteFolder As IQueryable(Of AP_Documents_Folder) = From c In d.AP_Documents_Folders Where _
                             c.PortalId = GetPortalId() And _
                             c.FolderId = folderID

        d.AP_Documents_Folders.DeleteOnSubmit(deleteFolder.First)

        Try
            d.SubmitChanges()
        Catch ex As Exception
            'TODO 
        End Try

    End Sub

    'Intermediate function that calls the recursive function BuildPath()
    Public Shared Function GetFullPath(ByVal Folder As AP_Documents_Folder) As String
        Dim pathName As String = ""
        Return BuildPath(Folder, pathName)
    End Function

    'Sees if the root folder has been chosen and saved in Settings. 
    'If it has it returns the folderID of the saved setting
    'If not it returns the folderId of the folder that has no parent
    'If there are no folders it creates one
    Public Shared Function GetModuleFolderId(ByVal tabModuleId As Integer) As Integer
        Dim d As New DocumentsDataContext()
        Dim objModules As New ModuleController
        Dim tabModuleSettings As Hashtable = objModules.GetTabModule(tabModuleId).TabModuleSettings

        Dim moduleFolderId As Integer

        ' Get module folder ID from module settings and initialize it to root folder if not already in settings (also create root folder in DB if it doesn't exist)
        If Not Integer.TryParse(tabModuleSettings(ModuleFolderSettingKey), moduleFolderId) Then ' TryParse will return False if the setting doesn't exist and convert the String to Integer in moduleFolderId otherwise

            Dim rootNode = From c In d.AP_Documents_Folders Where c.PortalId = GetPortalId() And c.ParentFolder = FolderConstants.NoParentFolderId
            'No rootNode found
            If rootNode.Count = 0 Then

                'Add the rootNode acDocuments in the database
                Dim insert As New AP_Documents_Folder
                insert.CustomIcon = Nothing
                insert.ParentFolder = NoParentFolderId
                insert.Name = DocumentsModuleRootFolderName
                insert.Permission = tabModuleSettings("DefaultPermissions")
                insert.PortalId = GetPortalId()
                d.AP_Documents_Folders.InsertOnSubmit(insert)
                d.SubmitChanges()

                moduleFolderId = insert.FolderId
            End If

            moduleFolderId = rootNode.First.FolderId

            'Set default folder for module in module settings
            SetModuleFolderId(tabModuleId, moduleFolderId)

        End If
        Return moduleFolderId
    End Function

    Public Shared Sub SetModuleFolderId(ByVal tabModuleId As Integer, ByVal folderId As Integer)
        Dim objModules As New ModuleController

        objModules.UpdateTabModuleSetting(tabModuleId, ModuleFolderSettingKey, folderId)

        ' refresh cache
        Dim moduleId As Integer = objModules.GetTabModule(tabModuleId).ModuleID
        DotNetNuke.Entities.Modules.ModuleController.SynchronizeModule(moduleId)
    End Sub

#End Region 'Document Settings

#Region "Document Main"


    Public Shared Function GetPhysicalFolderForFiles() As IFolderInfo

        Dim folderPath = FolderConstants.DocumentsModuleRootFolderName + "/"
        If Not FolderManager.Instance.FolderExists(GetPortalId(), folderPath) Then
            Return FolderManager.Instance.AddFolder(GetPortalId(), folderPath)
        Else
            Return FolderManager.Instance.GetFolder(GetPortalId(), folderPath)
        End If

    End Function

    Public Shared Function GetDocuments(ByVal folderId As Integer, ByVal includeTrashed As Boolean, ByVal includeDocsInSubFolders As Boolean) As IQueryable(Of AP_Documents_Doc)

        Dim d As New DocumentsDataContext()

        ' The list of folder IDs to return documents from
        Dim folderIDs As New List(Of Integer)

        ' Add requested folder in the list
        folderIDs.Add(folderId)

        If includeDocsInSubFolders = True Then ' Add recursively all sub folder IDs if requested
            'TODO: Create function to add recursively all subfolder IDs in folderIDs list
        End If

        'Get docs from all folders in folderIDs list
        Dim docs As IQueryable(Of AP_Documents_Doc) = (From c In d.AP_Documents_Docs Where _
                             c.AP_Documents_Folder.PortalId = GetPortalId() And _
                             folderIDs.Contains(c.AP_Documents_Folder.FolderId))

        ' Filter on non trashed docs if requested 
        If includeTrashed = False Then
            docs = From c In docs Where c.Trashed = False
        End If

        Return docs.OrderBy(Function(x) x.DisplayName).AsQueryable() 'Display resources ordered alphabetically by their title

    End Function

    Public Shared Function GetDocument(ByVal DocId As Integer) As AP_Documents_Doc
        Dim d As New DocumentsDataContext()
        Dim theDoc = From c In d.AP_Documents_Docs Where c.DocId = DocId
        Return theDoc.First
    End Function

    Public Shared Function GetFileIcon(ByVal FileId As Integer?, ByVal LinkType As Integer, Optional IconId As Integer? = -1) As String
        If Not IconId Is Nothing And IconId > 0 Then
            Return FileManager.Instance.GetUrl(FileManager.Instance.GetFile(IconId))
        End If
        If FileId Is Nothing Then
            Return "images/folder.png"
        End If
        Dim Path As String = "images/"
        If Not LinkType = DocumentConstants.LinkTypeFile Then
            Select Case LinkType
                Case DocumentConstants.LinkTypeUrl : Return Path & "URL.png"
                Case DocumentConstants.LinkTypeYouTube : Return Path & "YouTube.png"
                Case DocumentConstants.LinkTypeGoogleDoc : Return Path & "GoogleDoc.png"
                Case DocumentConstants.LinkTypePage : Return Path & "Url.png"
            End Select
        End If
        Dim theFile = FileManager.Instance.GetFile(FileId)
        If Not theFile Is Nothing Then
            Select Case theFile.Extension.ToLower
                Case "gif"
                    Return Path & "GIF.png"
                Case "bmp"
                    Return Path & "BMP.png"
                Case "doc"
                    Return Path & "DOC.png"
                Case "jpg"
                    Return Path & "JPG.png"
                Case "mov"
                    Return Path & "MOV.png"
                Case "mp3"
                    Return Path & "MP3.png"
                Case "mp4"
                    Return Path & "MP4.png"
                Case "mpg"
                    Return Path & "MPG.png"
                Case "pdf"
                    Return Path & "PDF.png"
                Case "png"
                    Return Path & "PNG.png"
                Case "psd"
                    Return Path & "PSD.png"
                Case "tiff"
                    Return Path & "TIFF.png"
                Case "txt"
                    Return Path & "TXT.png"
                Case "wav"
                    Return Path & "WAV.png"
                Case "zip"
                    Return Path & "ZIP.png"
                Case Else
                    Return Path & "Blank.png"
            End Select
        End If
        Return "images/Blank.png"
    End Function

    Public Shared Sub DeleteDocument(ByVal DocId As Integer)
        Dim d As New DocumentsDataContext()

        ' Mark the document as Trashed
        Dim theDoc = From c In d.AP_Documents_Docs Where c.DocId = DocId
        theDoc.First.Trashed = True
        d.SubmitChanges()
    End Sub

#End Region 'Document Main

#Region "Add/Edit"

    ' Insert resource with uploaded file
    Public Shared Sub InsertResourceWithFile(DisplayName As String, _
                                     Author As String, LinkType As String, LinkURL As String, _
                                     Trashed As Boolean, ByVal tabModuleId As Integer, _
                                     ByVal Description As String, ByVal fileName As String, ByVal fileContent As IO.Stream)

        'Add the file to dnn file system
        Dim theFileId = DocumentsController.AddFile(fileName, fileContent)

        'Insert the document into the database
        DocumentsController.InsertResource(theFileId, DisplayName, Author, LinkType, LinkURL, Trashed, tabModuleId, Description)

    End Sub

    ' Insert resource without uploaded file
    Public Shared Sub InsertResource(ByVal FileId As Integer, DisplayName As String, _
                                     Author As String, LinkType As String, LinkURL As String, _
                                     Trashed As Boolean, ByVal tabModuleId As Integer, _
                                     ByVal Description As String)
        Dim d As New DocumentsDataContext()
        Dim insert As New AP_Documents_Doc
        insert.FolderId = GetModuleFolderId(tabModuleId)
        insert.FileId = FileId
        insert.DisplayName = DisplayName
        insert.Author = Author
        insert.VersionNumber = "1.0"
        insert.CustomIcon = -1
        insert.LinkType = LinkType
        insert.Description = Description
        insert.LinkValue = LinkURL
        insert.Trashed = Trashed
        'insert.Permissions = Permissions
        'Todo: determine permissions implementation
        d.AP_Documents_Docs.InsertOnSubmit(insert)
        d.SubmitChanges()
    End Sub

    ' Update resource with uploaded file
    Public Shared Sub UpdateResourceWithFile(ByVal DocId As Integer, DisplayName As String, _
                                 Author As String, LinkType As String, LinkURL As String, _
                                 Trashed As Boolean, ByVal tabModuleId As Integer, _
                                 ByVal Description As String, ByVal fileName As String, ByVal fileContent As IO.Stream)

        Dim theFileId As Integer

        'Update file in dnn file system if type was already file, simply add file otherwise
        If DocumentsController.GetDocument(DocId).LinkType = DocumentConstants.LinkTypeFile Then
            theFileId = DocumentsController.UpdateFile(DocumentsController.GetDocument(DocId).FileId, fileName, fileContent)
        Else
            theFileId = DocumentsController.AddFile(fileName, fileContent)
        End If

        'Now update the document into the database
        DocumentsController.UpdateResource(DocId, theFileId, DisplayName, Author, LinkType, LinkURL, Trashed, tabModuleId, Description)

    End Sub

    ' Update resource without uploaded file
    Public Shared Sub UpdateResource(ByVal DocId As Integer, ByVal FileId As Integer, DisplayName As String, _
                                 Author As String, LinkType As String, LinkURL As String, _
                                 Trashed As Boolean, ByVal tabModuleId As Integer, _
                                 ByVal Description As String)
        Dim d As New DocumentsDataContext()

        'Get resource to update
        Dim theDoc = (From c In d.AP_Documents_Docs Where c.DocId = DocId).First

        'If a file was uploaded then the ressource type was changed to something other than a file
        'need to delete the file on the server.
        If (theDoc.LinkType = DocumentConstants.LinkTypeFile And LinkType <> DocumentConstants.LinkTypeFile) Then
            FileManager.Instance.DeleteFile(FileManager.Instance.GetFile(theDoc.FileId))
        End If

        'Update resource values
        theDoc.FolderId = GetModuleFolderId(tabModuleId)
        theDoc.FileId = FileId
        theDoc.DisplayName = DisplayName
        theDoc.Author = Author
        theDoc.VersionNumber = "1.0"
        theDoc.CustomIcon = -1
        theDoc.LinkType = LinkType
        theDoc.Description = Description
        theDoc.LinkValue = LinkURL
        theDoc.Trashed = Trashed
        'insert.Permissions = Permissions
        'Todo: determine permissions implementation

        'Submit in DB
        d.SubmitChanges()
    End Sub

    ' Add file in DNN file system
    Private Shared Function AddFile(ByVal fileName As String, ByVal fileContent As IO.Stream) As Integer 'Returns the added fileId
        Dim addedFile = FileManager.Instance.AddFile(DocumentsController.GetPhysicalFolderForFiles(), fileName, fileContent)
        'file is not viewable by URL directory access
        FileManager.Instance.SetAttributes(addedFile, IO.FileAttributes.Hidden)
        Return addedFile.FileId
    End Function

    ' Update file for a resource (delete old file and create new file in DNN file system)
    Private Shared Function UpdateFile(ByVal fileId As Integer, ByVal fileName As String, ByVal fileContent As IO.Stream) As Integer 'Returns the new fileId
        'Try first to add the file (extension could be rejected)
        Dim newFile = FileManager.Instance.AddFile(DocumentsController.GetPhysicalFolderForFiles(), fileName, fileContent)

        'file is not viewable by URL directory access
        FileManager.Instance.SetAttributes(newFile, IO.FileAttributes.Hidden)

        'If file added, delete old one
        If Not newFile.FileId = fileId Then
            FileManager.Instance.DeleteFile(FileManager.Instance.GetFile(fileId))
        End If

        Return newFile.FileId
    End Function

#End Region

#Region "Search implementation"

    'Cleans a string by analysing each character
    Public Shared Function CleanString(ByVal inputString As String) As String

        If (inputString <> "") Then
            'Remove newlines, carriage returns, extra spaces and tabs
            Dim noExtraWhiteSpace As String = Regex.Replace(inputString, "\s+", " ")

            'Remove periods and replace with spaces
            noExtraWhiteSpace = Replace(noExtraWhiteSpace, ".", " ")

            'Remove all nonalphanumberic characters except dash, apostrophe and space
            Dim tempArr() As Char = noExtraWhiteSpace.Where(Function(c)
                                                                Return (Char.IsLetterOrDigit(c) Or c = "-" Or c = " " Or c = "'")
                                                            End Function).ToArray
            inputString = New String(tempArr)
        End If

        Return inputString
    End Function

    'Cuts the string into a list of distinct words that are of a minimum size
    Public Shared Function CutString(ByVal inputString As String, ByVal minSize As Integer) As List(Of String)
        Dim words As String() = inputString.Split(New Char() {" "})
        Dim wordList As List(Of String) = New List(Of String)

        For Each word In words
            If (word.Length >= minSize) Then
                wordList.Add(word)
            End If
        Next

        Return wordList.Distinct(StringComparer.InvariantCultureIgnoreCase).ToList
    End Function

    'Finds documents containing ALL the wordsToMatch within the documents title and description fields
    Public Shared Function GetSearchDocuments(ByVal wordsToMatch As List(Of String), ByVal minSize As Integer, ByVal TabModuleId As Integer) As IQueryable(Of AP_Documents_Doc)

        Dim folderId As Integer = DocumentsController.GetModuleFolderId(TabModuleId)
        Dim docs = DocumentsController.GetDocuments(folderId, False, False)

        ' Dim titlesAndDescriptions As New Dictionary(Of Integer, String)
        Dim searchDocuments As List(Of AP_Documents_Doc) = New List(Of AP_Documents_Doc)

        Dim titlesAndDescriptions As Dictionary(Of Long, String) =
            docs.ToDictionary(Function(doc) doc.DocId, _
                              Function(doc) DocumentsController.CleanString(doc.DisplayName) & " " _
                                  & DocumentsController.CleanString(doc.Description))

        For Each titleAndDescription In titlesAndDescriptions

            Dim matches As IEnumerable(Of String) = From word In wordsToMatch
                        Where GetAccentless(titleAndDescription.Value) _
                            .IndexOf(GetAccentless(word), 0, StringComparison.InvariantCultureIgnoreCase) > -1
                        Select word

            If matches.Count = wordsToMatch.Count Then
                searchDocuments.Add(DocumentsController.GetDocument(titleAndDescription.Key))
            End If
        Next

        Return searchDocuments.AsQueryable
    End Function

    'Needed so that resources can be searchable
    'Public Overrides Function GetModifiedSearchDocuments(moduleInfo As ModuleInfo, beginDateUtc As Date) As IList(Of Entities.SearchDocument)

    '    Dim searchDocuments As New List(Of Entities.SearchDocument)

    '    Dim d As New DocumentsDataContext
    '    Dim Docs = From c In d.AP_Documents_Docs Where c.AP_Documents_Folder.PortalId = moduleInfo.PortalID

    '    For Each row In Docs
    '        Dim tags As String = ""
    '        For Each tag In row.AP_Documents_TagMetas
    '            tags &= tag.AP_Documents_Tag.TagName & " "
    '        Next

    '        Dim SearchText = (row.DisplayName & " " & row.LinkType & " " & row.Keywords & " " & tags & " " & row.Description).Replace(".", " ").Replace(";", " ").Replace("-", " ").Replace(":", " ")

    '        Dim searchDoc As Entities.SearchDocument = New Entities.SearchDocument

    '        With searchDoc
    '            .Title = row.DisplayName
    '            .Description = row.Description
    '            .UniqueKey = "D" & row.DocId
    '            .ModuleId = moduleInfo.ModuleID
    '            '.AuthorUserId = 
    '            .Body = SearchText
    '            .IsActive = Not row.Trashed
    '            .PortalId = moduleInfo.PortalID
    '            .ModifiedTimeUtc = Today

    '        End With

    '        searchDocuments.Add(searchDoc)

    '    Next

    '    Return searchDocuments

    'End Function

#End Region 'Search implementation

#Region "Helper functions"

    Private Shared Function GetPortalId() As Integer
        Return CType(HttpContext.Current.Items(PortalSettingKey), PortalSettings).PortalId
    End Function

    'Recursive function that builds the full path from the inside out
    Private Shared Function BuildPath(ByVal Folder As AP_Documents_Folder, ByRef pathName As String) As String
        Dim d As New DocumentsDataContext()
        pathName = Folder.Name & pathName

        If Folder.ParentFolder > 0 Then
            Dim parent = From c In d.AP_Documents_Folders Where c.FolderId = Folder.ParentFolder And c.PortalId = GetPortalId()
            If parent.Count > 0 Then
                pathName = "/" & pathName
                BuildPath(parent.First, pathName)
            End If
        End If
        Return pathName
    End Function

    ' Check if the current user is authorized to view the document
    Public Shared Function IsAuthorized(ByVal userId As Integer, ByVal docId As Integer) As Boolean

        Dim user = DotNetNuke.Entities.Users.UserController.GetUserById(GetPortalId(), userId)

        'TODO: Check permission given for the particular ressource when handled

        'Return user.IsSuperUser Or user.IsInRole("Administrators") Or user.IsInRole("Staff")
        Return True 'Access to ViewDocument and DownloadDocument is already restricted depending on the module access permissions.

    End Function

    'Removes accents from the string value
    Private Shared Function GetAccentless(value As String) As String
        Return New String(value.Normalize(NormalizationForm.FormD) _
                               .Where(Function(c) CharUnicodeInfo.GetUnicodeCategory(c) <> _
                                                  UnicodeCategory.NonSpacingMark).ToArray())
    End Function

#End Region 'Helper functions

End Class
