﻿Imports System.IO
Imports GR_NET

Partial Class DesktopModules_AgapeConnect_gr_mapping_gr_sub
    Inherits System.Web.UI.Page

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load

        Response.Clear()

        Try


            If Request.HttpMethod = "POST" Then
                Dim json = New StreamReader(Request.InputStream).ReadToEnd
                AgapeLogger.WriteEventLog(0, "GR sub  received:" & json)
                Dim jss = New System.Web.Script.Serialization.JavaScriptSerializer()
                Dim resp = jss.Deserialize(Of Dictionary(Of String, Object))(json)

                If resp("action") = "confirm" Then
                    'GET 
                    Dim web As New System.Net.WebClient()

                    AgapeLogger.WriteEventLog(0, "GR sub confirm received:" & resp("confirmation_url"))
                    Dim q = web.DownloadString(resp("confirmation_url"))
                    Response.Write(q)




                    Response.End()
                ElseIf resp("action") = "merge" Then

                    Dim old_id = resp("old_id")
                    Dim new_id = resp("new_id")
                    AgapeLogger.WriteEventLog(0, "Merge From: " & old_id & ", to: " & new_id)
                    'Dim d As New StaffBroker.StaffBrokerDataContext
                    'Dim PS = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
                    'Dim q = From c In d.UserProfiles Where c.ProfilePropertyDefinition.PortalID = PS.PortalId And c.ProfilePropertyDefinition.PropertyName = "gr_person_id" And c.PropertyValue = old_id
                    'If q.Count > 0 Then
                    '    q.First.PropertyValue = new_id


                    '    'The relationship to ministry may have changed.
                    '    Dim r = From c In q.First.User.UserProfiles Where c.ProfilePropertyDefinition.PropertyName = "gr_ministry_membership_id"
                    '    If r.Count > 0 Then
                    '        r.First.PropertyValue = ""

                    '    End If
                    '    d.SubmitChanges()


                    '    AgapeLogger.WriteEventLog(0, "Merged user: " & q.First.User.DisplayName)
                    'End If




                ElseIf resp("action") = "updated" Then
                    AgapeLogger.WriteEventLog(0, "Updated Id:" & resp("id"))


                    Dim PS = CType(HttpContext.Current.Items("PortalSettings"), PortalSettings)
                    Dim gr As New GR(StaffBrokerFunctions.GetSetting("gr_api_key", PS.PortalId), StaffBrokerFunctions.GetSetting("gr_api_url", PS.PortalId))

                    Dim ent = gr.GetEntity(resp("id"))

                    If ent.EntityType = "person" Then

                        Dim d As New gr_mapping.gr_mappingDataContext
                        '                       Dim theUser = From c In d.UserProfiles Where c.ProfilePropertyDefinition.PropertyName = "gr_person_id" And c.PropertyValue = resp("id") And c.User.AP_StaffBroker_Staffs.PortalId = PS.PortalId Select c.User

                        Dim User = UserController.GetUserById(PS.PortalId, resp("client_integration_id"))
                        If Not User Is Nothing Then
                            User.Profile.SetProfileProperty("gr_person_id", resp("id"))
                            AgapeLogger.WriteEventLog(0, "Processing User: " & User.DisplayName)
                            Dim Staff = StaffBrokerFunctions.GetStaffMember(User.UserID)

                            For Each link In d.gr_mappings.Where(Function(c) c.PortalId = PS.PortalId And c.LocalSource = "U" And c.can_be_updated)
                                Dim value = ent.GetPropertyValue(link.gr_dot_notated_name.Replace("person.", "").Replace("person:{in_this_min}", "ministry:relationship"))
                                If Not String.IsNullOrEmpty(value) Then
                                    Select Case link.LocalName
                                        Case "FirstName"

                                            User.FirstName = value
                                            AgapeLogger.WriteEventLog(0, "FirsName : " & User.FirstName)
                                        Case "LastName"
                                            User.LastName = value
                                        Case "DisplayName"
                                            User.DisplayName = value
                                        Case "Email"
                                            User.Email = value

                                    End Select
                                End If
                            Next
                            For Each link In d.gr_mappings.Where(Function(c) c.PortalId = PS.PortalId And c.LocalSource = "UP" And c.can_be_updated)

                                Dim value = ent.GetPropertyValue(link.gr_dot_notated_name.Replace("person.", "").Replace("person:{in_this_min}", "ministry:relationship"))
                                If Not String.IsNullOrEmpty(value) Then
                                    User.Profile.SetProfileProperty(link.LocalName, value)
                                End If
                            Next
                            For Each link In d.gr_mappings.Where(Function(c) c.PortalId = PS.PortalId And c.LocalSource = "S" And c.can_be_updated)
                                Dim value = ent.GetPropertyValue(link.gr_dot_notated_name.Replace("person.", "").Replace("person:{in_this_min}", "ministry:relationship"))
                                If Not String.IsNullOrEmpty(value) Then
                                    Select Case link.LocalName
                                        Case "R/C"
                                            Staff.CostCenter = value
                                        Case "DisplayName"
                                            Staff.DisplayName = value

                                    End Select
                                End If
                            Next
                            For Each link In d.gr_mappings.Where(Function(c) c.PortalId = PS.PortalId And c.LocalSource = "SP" And c.can_be_updated)

                                Dim value = ent.GetPropertyValue(link.gr_dot_notated_name.Replace("person.", "").Replace("person:{in_this_min}", "ministry:relationship"))
                                If Not String.IsNullOrEmpty(value) Then
                                    StaffBrokerFunctions.AddProfileValue(PS.PortalId, Staff.StaffId, link.LocalName, value)
                                End If
                            Next

                            UserController.UpdateUser(PS.PortalId, User)


                        End If


                    End If

                    'AgapeLogger.WriteEventLog(0, resp("client_integration_id") & " has changed. " & ent.GetPropertyValue(""))



                End If

            End If


        Catch ex As Exception

            AgapeLogger.WriteEventLog(0, "Error on subscription callback: " & ex.ToString)
            Response.End()
        End Try


    End Sub
End Class
