using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;
using System.Data.Linq;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.SqlServerDataStore
{
    public class Connector : AddInBase
    {
        private TwitterIrcGatewayDataContext _dataContext = new TwitterIrcGatewayDataContext();
        private Dictionary<String, Group> _cacheGroup;
        public override void Initialize()
        {
            base.Initialize();

            lock (_dataContext)
            {
                _dataContext.Connection.Open();

                // メインチャンネル
                if ((from g1 in _dataContext.Group
                     where g1.UserId == CurrentSession.TwitterUser.Id && g1.Name == CurrentSession.Config.ChannelName
                     select g1).Count() == 0)
                {
                    using (var ctx = new TwitterIrcGatewayDataContext())
                    {
                        Group g = new Group
                                      {Name = CurrentSession.Config.ChannelName, UserId = CurrentSession.TwitterUser.Id};
                        ctx.Group.InsertOnSubmit(g);
                        ctx.SubmitChanges();
                    }
                }

                UpdateGroupCache();
            }
            CurrentSession.AddInsLoadCompleted += new EventHandler<EventArgs>(CurrentSession_AddInsLoadCompleted);
            CurrentSession.PreProcessTimelineStatuses += new EventHandler<TimelineStatusesEventArgs>(CurrentSession_PreProcessTimelineStatuses);
            CurrentSession.PreProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(CurrentSession_PreProcessTimelineStatus);
            CurrentSession.PostSendGroupMessageTimelineStatus += new EventHandler<TimelineStatusGroupEventArgs>(CurrentSession_PostSendGroupMessageTimelineStatus);
            CurrentSession.PostProcessTimelineStatuses += new EventHandler<TimelineStatusesEventArgs>(CurrentSession_PostProcessTimelineStatuses);
        }

        void CurrentSession_AddInsLoadCompleted(object sender, EventArgs e)
        {
            CurrentSession.AddInManager.GetAddIn<TypableMapSupport>().TypableMapFactory = new TypableMapStatusSqlServerRepositoryFactory();
        }
        
        private void UpdateGroupCache()
        {
            _cacheGroup = (from g in _dataContext.Group
                           where g.UserId == CurrentSession.TwitterUser.Id
                           select g).ToDictionary(v => v.Name, StringComparer.InvariantCultureIgnoreCase);
        }
        
        void CurrentSession_PreProcessTimelineStatuses(object sender, TimelineStatusesEventArgs e)
        {
            lock (_dataContext)
            {
                // FIXME: この辺はあとで何とかする
                var notFoundCount = (from g in CurrentSession.Groups.Keys
                                     where !_cacheGroup.ContainsKey(g)
                                     select g).Count();
                if (notFoundCount > 0)
                {
                    var newGroups = from g in CurrentSession.Groups.Keys
                                    let count = (from g1 in _dataContext.Group
                                                 where g1.UserId == CurrentSession.TwitterUser.Id && g1.Name == g
                                                 select g1).Count()
                                    where count == 0
                                    select new Group() {Name = g, UserId = CurrentSession.TwitterUser.Id};
                    using (var ctx = new TwitterIrcGatewayDataContext())
                    {
                        ctx.Group.InsertAllOnSubmit(newGroups);
                        ctx.SubmitChanges();
                    }
                    UpdateGroupCache();
                }
                // メインチャンネル
                if ((from g1 in _dataContext.Group
                     where g1.UserId == CurrentSession.TwitterUser.Id && g1.Name == CurrentSession.Config.ChannelName
                     select g1).Count() == 0)
                {
                    using (var ctx = new TwitterIrcGatewayDataContext())
                    {
                        Group g = new Group { Name = CurrentSession.Config.ChannelName, UserId = CurrentSession.TwitterUser.Id };
                        ctx.Group.InsertOnSubmit(g);
                        ctx.SubmitChanges();
                    }
                }

                var newTwitterUsers =
                    (from status in e.Statuses.Status where status.User.Id != 0 select status.User).Distinct();
                foreach (var user in newTwitterUsers)
                {
                    using (var ctx = new TwitterIrcGatewayDataContext())
                    {
                        try
                        {
                            User newUser = new User
                                           {
                                               Id = user.Id,
                                               Name = user.Name,
                                               IsProtected = user.Protected,
                                               ProfileImageUrl = user.ProfileImageUrl,
                                               ScreenName = user.ScreenName
                                           };
                            if (!ctx.User.Contains(newUser))
                            {
                                ctx.User.InsertOnSubmit(newUser);
                                ctx.SubmitChanges();
                            }
                        }
                        catch (DuplicateKeyException dupE)
                        {
                            continue;
                        }
                        catch (SqlException sqlE)
                        {
                            // キー制約
                            if (sqlE.Number == 2627)
                                continue;

                            throw;
                        }
                    }
                }
            }
        }

        void CurrentSession_PreProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            lock (_dataContext)
            {
                var status = new Status
                                          {
                                              Id = e.Status.Id,
                                              CreatedAt = e.Status.CreatedAt,
                                              ScreenName = e.Status.User.ScreenName,
                                              Text = e.Status.Text,
                                              UserId = (e.Status.User.Id == 0) ? null : (Int32?)e.Status.User.Id
                                          };

                if (!_dataContext.Status.Contains(status))
                {
                    using (var ctx = new TwitterIrcGatewayDataContext())
                    {
                        try
                        {
                            ctx.Status.InsertOnSubmit(status);
                            ctx.SubmitChanges();
                        }
                        catch (DuplicateKeyException dupE)
                        {
                        }
                        catch (SqlException sqlE)
                        {
                            // キー制約
                            if (sqlE.Number == 2627)
                                return;

                            throw;
                        }
                    }
                }
            }
        }

        void CurrentSession_PostSendGroupMessageTimelineStatus(object sender, TimelineStatusGroupEventArgs e)
        {
            lock (_dataContext)
            {
                using (var ctx = new TwitterIrcGatewayDataContext())
                {
                    try
                    {
                        Timeline timeline = new Timeline
                                                {
                                                    GroupId = _cacheGroup[e.Group.Name].Id,
                                                    StatusId = e.Status.Id,
                                                    UserId = CurrentSession.TwitterUser.Id
                                                };
                        if (ctx.Timeline.Contains(timeline))
                            return;
                        ctx.Timeline.InsertOnSubmit(timeline);
                        ctx.SubmitChanges();
                    }
                    catch (Exception)
                    {
                        CurrentSession.Logger.Error("Group not found in _cacheGroup: {0}", e.Group.Name);
                        throw;
                    }
                }
            }
        }

        void CurrentSession_PostProcessTimelineStatuses(object sender, TimelineStatusesEventArgs e)
        {
        }

        public override void Uninitialize()
        {
            _dataContext.Dispose();
            base.Uninitialize();
        }
    }
}
