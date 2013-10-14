using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public class InsertRetweetMark : AddInBase
    {
        public override void Initialize()
        {
            Type encodingType = Server.Encoding.GetType();
            if (encodingType == typeof(UTF8Encoding) || encodingType == typeof(UTF32Encoding) || encodingType == typeof(UnicodeEncoding))
            {
                CurrentSession.PreProcessTimelineStatus += (sender, e) =>
                                                                     {
                                                                         if (e.Status.RetweetedStatus != null)
                                                                         {
                                                                             e.Text = String.Format("♻ RT @{0}: {1}", e.Status.RetweetedStatus.User.ScreenName, e.Status.RetweetedStatus.Text);
                                                                             e.Status.Entities = e.Status.RetweetedStatus.Entities; // 詰め替え
                                                                         }
                                                                     };
            }
        }
    }
}
