using System;
using System.ComponentModel;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.CamouflageClient
{
    public class CamouflageClientAddIn : AddInBase
    {
        public override void Initialize()
        {
            UpdateClientSource();
            Session.AddInsLoadCompleted += (sender, e) => Session.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<CamouflageClientContext>();
        }
    
        public void UpdateClientSource()
        {
            Configuration config = Session.AddInManager.GetConfig<Configuration>();
            if (String.IsNullOrEmpty(config.ClientSource))
            {
                Session.TwitterService.ClientName = "twitterircgateway";
            }
            else
            {
                Session.TwitterService.ClientName = config.ClientSource;
            }
        }
    }

    
    [Description("クライアント偽装設定のコンテキストに切り替えます")]
    public class CamouflageClientContext : Context
    {
        [Description("偽装するクライアントのsourceを設定します")]
        public void ClientSource(String value)
        {
            Configuration config = Session.AddInManager.GetConfig<Configuration>();
            if (!String.IsNullOrEmpty(value))
                config.ClientSource = value;
            Console.NotifyMessage("ClientSource = " + config.ClientSource);
            Session.AddInManager.SaveConfig(config);

            Session.AddInManager.GetAddIn<CamouflageClientAddIn>().UpdateClientSource();
        }
        
        [Description("偽装を解除します")]
        public void ResetClientSource()
        {
            Configuration config = Session.AddInManager.GetConfig<Configuration>();
            config.ClientSource = String.Empty;
            Console.NotifyMessage("ClientSource = " + config.ClientSource);
            Session.AddInManager.SaveConfig(config);
            Session.AddInManager.GetAddIn<CamouflageClientAddIn>().UpdateClientSource();
        }
    }

    public class Configuration : IConfiguration
    {
        public String ClientSource;
    }
}
