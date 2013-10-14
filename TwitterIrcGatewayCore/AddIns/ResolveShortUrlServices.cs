using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class ResolveShortUrlServices : AddInBase
    {
        public override void Initialize()
        {
            CurrentSession.PreFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PreFilterProcessTimelineStatus);
            //Session.PreSendUpdateStatus += new EventHandler<StatusUpdateEventArgs>(Session_PreSendUpdateStatus);
        }

        //void Session_PreSendUpdateStatus(object sender, StatusUpdateEventArgs e)
        //{
        //    e.Text = Utility.UrlToTinyUrlInMessage(e.Text);
        //}
        
        void Session_PreFilterProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // t.co (Twitter Url Shortener)
            if (e.Status.Entities != null && e.Status.Entities.Urls != null && e.Status.Entities.Urls.Length > 0)
            {
                foreach (var urlEntity in e.Status.Entities.Urls)
                {
                    if (!String.IsNullOrEmpty(urlEntity.ExpandedUrl))
                    {
                        e.Text = Regex.Replace(e.Text, Regex.Escape(urlEntity.Url), urlEntity.ExpandedUrl);
                    }
                }
            }

            // TinyURL
            e.Text = (CurrentSession.Config.ResolveTinyUrl) ? Utility.ResolveShortUrlInMessage(Utility.ResolveTinyUrlInMessage(e.Text))
                                                            : e.Text;
        }
    }
}
