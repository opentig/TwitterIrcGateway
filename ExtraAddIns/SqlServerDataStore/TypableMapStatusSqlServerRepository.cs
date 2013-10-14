using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap;
using TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.SqlServerDataStore
{
    public class TypableMapStatusSqlServerRepositoryFactory : ITypableMapStatusRepositoryFactory
    {
        #region ITypableMapStatusRepositoryFactory メンバ
        public ITypableMapStatusRepository Create(int size)
        {
            return new TypableMapStatusSqlServerRepository(size);
        }
        #endregion
    }
    public class TypableMapStatusSqlServerRepository : ITypableMapStatusRepository
    {
        private TypableMap<Int64> _typableMap;
        
        public TypableMapStatusSqlServerRepository(Int32 size)
        {
            _typableMap = new TypableMap<long>(size);
        }
        #region ITypableMapStatusRepository メンバ

        public void SetSize(int size)
        {
            _typableMap = new TypableMap<long>(size);
        }

        public string Add(Misuzilla.Applications.TwitterIrcGateway.Status status)
        {
            return _typableMap.Add(status.Id);
        }

        public bool TryGetValue(string typableMapId, out Misuzilla.Applications.TwitterIrcGateway.Status status)
        {
            Int64 statusId;
            status = null;
            
            if (_typableMap.TryGetValue(typableMapId, out statusId))
            {
                using (TwitterIrcGatewayDataContext ctx = new TwitterIrcGatewayDataContext())
                {
                    var dbStatus = ctx.Status.Where(s => s.Id == statusId).FirstOrDefault();
                    if (dbStatus != null)
                    {
                        status = new TwitterIrcGateway.Status();
                        status.Id = dbStatus.Id;
                        status.Text = dbStatus.Text;
                        status.InReplyToStatusId = dbStatus.InReplyToId.HasValue ? dbStatus.InReplyToId.Value.ToString() : null;
                        status.CreatedAt = dbStatus.CreatedAt;
                        status.User = new TwitterIrcGateway.User();
                        status.User.ScreenName = dbStatus.ScreenName;
                        if (dbStatus.User != null)
                        {
                            status.User.Id = dbStatus.User.Id;
                            status.User.ScreenName = dbStatus.User.ScreenName;
                            status.User.Name = dbStatus.User.Name;
                            status.User.Protected = dbStatus.User.IsProtected;
                            status.User.ProfileImageUrl = dbStatus.User.ProfileImageUrl;
                        }
                    }
                }
            }

            return (status != null);
        }

        #endregion
    }
}
