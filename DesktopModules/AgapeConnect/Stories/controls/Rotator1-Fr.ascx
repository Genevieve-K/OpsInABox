﻿<%@ Control Language="VB" AutoEventWireup="false" CodeFile="Rotator1-Fr.ascx.vb" Inherits="DotNetNuke.Modules.AgapeConnect.Stories.Rotator_Fr" %>
<script src="/js/jquery.nivo.slider.js" type="text/javascript"></script>
<link href="/js/nivo-slider.css" rel="stylesheet" type="text/css" media="screen"  />
<link href="/DesktopModules/AgapeConnect/Stories/themes/default/france.css" rel="stylesheet" type="text/css" media="screen"  />

 <script type="text/javascript">
     (function ($, Sys) {
         function setUpMyTabs() {
             $('#slider<%= hfChannelId.Value %>').css({
                 'visibility':'visible',
                 'height': <%= hfDivHeight.Value %>,
                 'max-height': <%= hfDivHeight.Value %>}).nivoSlider({
                 effect: 'fade',
                 pauseTime: <%= hfPauseTime.Value %>,
                 width: <%= hfDivWidth.Value %>,
                 manualAdvance: <%= hfManualAdvance.Value %>,
             });

             var w= $('#rotatorContainer<%= hfChannelId.Value %>').width();
             var scale = w/<%= hfDivWidth.Value %>;
             var offset = (1.0- scale) *50;
             var newH = <%= hfDivHeight.Value %> * scale;
             $('#rotatorContainer<%= hfChannelId.Value %>').css({
                 'transform': 'translate(-' + offset + '%, -' + offset + '%) scale(' + scale + ')' ,
                 '-ms-transform': 'translate(-' + offset + '%, -' + offset + '%) scale(' + scale + ')' ,
                 '-moz-transform': 'translate(-' + offset + '%, -' + offset + '%) scale(' + scale + ')' ,
                 '-webkit-transform': 'translate(-' + offset + '%, -' + offset + '%) scale(' + scale + ')' ,
                 '-o-transform': 'translate(-' + offset + '%, -' + offset + '%) scale(' + scale + ')' ,
                 'width':<%= hfDivWidth.Value %> +'px', 'height':(newH-8) + 'px' });

             $('#fr_video_popup_close').click(function() {
                 popclosevideo();
             });
             
             
         }

         


         $(document).ready(function () {
             setUpMyTabs();
             Sys.WebForms.PageRequestManager.getInstance().add_endRequest(function () {
                 setUpMyTabs();
                 setupvideo();
             });
         });
     } (jQuery, window.Sys));
   
   function registerClick(c)
   {
        $.ajax({ type: 'POST', url: "<%= NavigateURL() %>",
                        data: ({ StoryLink: c })
                    });
   }
     //set up video
     var tag = document.createElement('script');

     tag.src = "https://www.youtube.com/iframe_api";
     var firstScriptTag = document.getElementsByTagName('script')[0];
     firstScriptTag.parentNode.insertBefore(tag, firstScriptTag);

     // 3. This function creates an <iframe> (and YouTube player)
     //    after the API code downloads.
     var player;
     function onYouTubeIframeAPIReady() {
         player = new YT.Player('popplayer', {
             height: '432',
             width: '768',
             videoId: 'LSW9XgU0xC8',
             playerVars: { 'showinfo' : 0 }
         });
     }
     function pauseVideo() {
         player.pauseVideo();
     }
     function playVideo() {
         player.playVideo()
     }

     function popclosevideo() {
         $('#fr_video_popup').fadeOut();
         pauseVideo();
         setTimeout("$('#slider').data('nivoslider').start()",10000); //restart the slider after the video is closed (not working)
     }

     function popupvideo(){
         $('#fr_video_popup').fadeIn();
         $('#fr_video_popup').css("display","flex");
         $('#slider').data('nivoslider').stop(); //stop the slider while the video is open (not working)
     }
     $(document).keyup(function(e) {
         if (e.which == 27) { 
             popclosevideo();
         }
     });
     
</script>
<asp:HiddenField ID="hfManualAdvance" runat="server" />
<asp:HiddenField ID="hfPauseTime" runat="server" />
<asp:HiddenField ID="hfDivWidth" runat="server" />
<asp:HiddenField ID="hfDivHeight" runat="server" />
<asp:HiddenField ID="hfChannelId" runat="server" />

<div id="rotatorContainer<%= hfChannelId.Value %>" class="theme-default">
    <div id="slider<%= hfChannelId.Value %>" class="nivoSlider">
        <asp:Repeater ID="SliderImageList" runat="server">
            <ItemTemplate>
            <asp:HyperLink 
                href=<%# Eval(RotatorConstants.SLIDELINK) %>
                ID="hlImageSlider"
                CssClass = <%# Eval(RotatorConstants.SLIDEIMAGECSS) %>
                runat="server">
                <asp:Image
                    src=<%# Eval(RotatorConstants.SLIDEIMAGE) %> 
                    alt=<%# Eval(RotatorConstants.SLIDEIMAGEALTTEXT) %> 
                    title=<%# Eval(RotatorConstants.SLIDEIMAGETITLE) %> 
                    style=<%# Eval(RotatorConstants.SLIDEIMAGESTYLE) %> 
                    runat="server" />
            </asp:HyperLink>
            </ItemTemplate>
        </asp:Repeater>
    </div>
</div>
<div id="fr_video_popup">
<div id="playerspace">
<div style="text-align: right;">
<a id="fr_video_popup_close" class="fr_video_popup_close">&#x2716</a>
</div>
<div id="popplayer"></div>
</div>
</div>