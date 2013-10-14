using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class ConsoleAddIn : Console, IAddIn
    {
        #region IAddIn メンバ
        public void Initialize(Server server, Session session)
        {
            Attach("#Console", server, session, typeof(RootContext), false);

            RegisterContext<RootContext>();
            RegisterContext<ConfigContext>();
            RegisterContext<FilterContext>();
            RegisterContext<GroupContext>();
            RegisterContext<SystemContext>();
        }

        public void Uninitialize()
        {
            Detach();
        }
        #endregion
    }
}
