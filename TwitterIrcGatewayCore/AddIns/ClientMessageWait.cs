using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public class ClientMessageWait : AddInBase
    {
        public override void Initialize()
        {
            CurrentSession.PostSendMessageTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostSendMessageTimelineStatus);
        }

        void Session_PostSendMessageTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // ウェイト
            if (CurrentSession.Config.ClientMessageWait > 0)
                Thread.Sleep(CurrentSession.Config.ClientMessageWait);
        }
    }
}
