using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class RecentLogItem
    {
        public String Sender { get; set; }
        public DateTime DateTime { get; set; }
        public String Text { get; set; }
    }
    
    public class RecentLog : AddInBase
    {
        private Dictionary<String, List<RecentLogItem>> _recentStatuses;
        private const Int32 MaxCount = 10;
        
        public override void Initialize()
        {
            base.Initialize();

            _recentStatuses = new Dictionary<string, List<RecentLogItem>>(StringComparer.InvariantCultureIgnoreCase);
            CurrentSession.ConnectionAttached += CurrentSession_ConnectionAttached;
            CurrentSession.PostSendGroupMessageTimelineStatus += new EventHandler<TimelineStatusGroupEventArgs>(CurrentSession_PreSendGroupMessageTimelineStatus);
        }

        void CurrentSession_PreSendGroupMessageTimelineStatus(object sender, TimelineStatusGroupEventArgs e)
        {
            if (!_recentStatuses.ContainsKey(e.Group.Name))
                _recentStatuses[e.Group.Name] = new List<RecentLogItem>();

            _recentStatuses[e.Group.Name].Add(new RecentLogItem()
                                                  {
                                                      Text = e.Text,
                                                      DateTime = e.Status.CreatedAt,
                                                      Sender = e.Status.User.ScreenName
                                                  });
            if (_recentStatuses[e.Group.Name].Count > MaxCount)
            {
                _recentStatuses[e.Group.Name].RemoveAt(0);
            }
        }

        public override void Uninitialize()
        {
            CurrentSession.ConnectionAttached -= CurrentSession_ConnectionAttached;
            base.Uninitialize();
        }

        void CurrentSession_ConnectionAttached(object sender, ConnectionAttachEventArgs e)
        {
            if (_recentStatuses.ContainsKey(CurrentSession.Config.ChannelName))
            {
                foreach (var item in _recentStatuses[CurrentSession.Config.ChannelName])
                {
                    foreach (var line in item.Text.Split('\n'))
                    {
                        e.Connection.Send(new NoticeMessage(CurrentSession.Config.ChannelName,
                                                            String.Format("{0}: {1}", item.DateTime.ToString("HH:mm"),
                                                                          line.Trim())) { SenderNick = item.Sender });
                    }
                }
            }
            foreach (Group group in CurrentSession.Groups.Values.Where(g => g.IsJoined && !g.IsSpecial && _recentStatuses.ContainsKey(g.Name)))
            {
                foreach (var item in _recentStatuses[group.Name])
                {
                    foreach (var line in item.Text.Split('\n'))
                    {
                        e.Connection.Send(new NoticeMessage(group.Name,
                                                            String.Format("{0}: {1}", item.DateTime.ToString("HH:mm"),
                                                                          line.Trim())) { SenderNick = item.Sender });
                    }
                }
            }
        }
    }
}
