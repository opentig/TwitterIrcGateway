using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class ExtensionCommands : AddInBase
    {
        public override void Initialize()
        {
            CurrentSession.MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGGC);
            CurrentSession.MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGLOADFILTER);
        }

        void MessageReceived_TIGGC(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGGC", true) != 0) return;
            Int64 memUsage = GC.GetTotalMemory(false);
            GC.Collect();
            CurrentSession.SendTwitterGatewayServerMessage(String.Format("Garbage Collect: {0:###,##0} bytes -> {1:###,##0} bytes", memUsage, GC.GetTotalMemory(false)));
        }

        void MessageReceived_TIGLOADFILTER(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TIGLOADFILTER", true) != 0) return;
            CurrentSession.LoadFilters();
        }
    }
}
