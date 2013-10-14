using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    [Description("システムに関連するコンテキストに切り替えます")]
    public class SystemContext : Context
    {
        [Description("アドインの一覧を表示します")]
        public void ShowAddIns()
        {
            foreach (Type addInType in CurrentSession.AddInManager.AddInTypes)
            {
                Assembly addinAsm = addInType.Assembly;
                if (addinAsm == Assembly.GetExecutingAssembly())
                    continue;

                Console.NotifyMessage(String.Format("{0} {1} {2}",
                                                         addInType.FullName,
                                                         addinAsm.GetName().Version,
                                                         (CurrentSession.AddInManager.GetAddIn(addInType) == null
                                                              ? "(Disabled)"
                                                              : "")
                                               ));
            }
        }

        [Description("アドインを無効にします")]
        public void DisableAddIn(String addInName)
        {
            if (String.IsNullOrEmpty(addInName))
            {
                Console.NotifyMessage("アドインの名前を指定する必要があります。");
                return;
            }
            foreach (var t in CurrentSession.AddInManager.AddInTypes)
            {
                if ((String.Compare(t.FullName, addInName, true) == 0) && typeof(IAddIn).IsAssignableFrom(t) &&
                    !t.IsAbstract && t.IsClass)
                {
                    if (!Session.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        CurrentSession.Config.DisabledAddInsList.Add(t.FullName);
                        CurrentSession.SaveConfig();
                    }
                    Console.NotifyMessage(String.Format("アドイン \"{0}\" は無効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    return;
                }
            }

            Console.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
        }

        [Description("アドインを有効にします")]
        public void EnableAddIn(String addInName)
        {
            if (String.IsNullOrEmpty(addInName))
            {
                Console.NotifyMessage("アドインの名前を指定する必要があります。");
                return;
            }

            foreach (var t in CurrentSession.AddInManager.AddInTypes)
            {
                if ((String.Compare(t.FullName, addInName, true) == 0) && typeof(IAddIn).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                {
                    if (CurrentSession.Config.DisabledAddInsList.Contains(t.FullName))
                    {
                        CurrentSession.Config.DisabledAddInsList.Remove(t.FullName);
                        CurrentSession.SaveConfig();
                    }
                    Console.NotifyMessage(String.Format("アドイン \"{0}\" は有効化されました。次回接続時まで設定は反映されません。", t.FullName));
                    return;
                }
            }
            Console.NotifyMessage(String.Format("アドイン \"{0}\" は読み込まれていません。", addInName));
        }
#if FALSE
        [Description("アドインを再読込します")]
        public void ReloadAddIns()
        {
            CurrentSession.AddInManager.RestartAddIns();
        }
#endif
        [Description("バージョン情報を表示します")]
        public void Version()
        {
            Assembly asm = typeof(Server).Assembly;
            AssemblyName asmName = asm.GetName();
            Console.NotifyMessage(String.Format("TwitterIrcGateway {0}", asmName.Version));
        }

        [Description("システム情報を表示します")]
        public void ShowInfo()
        {
            Assembly asm = typeof(Server).Assembly;
            AssemblyName asmName = asm.GetName();

            Console.NotifyMessage("[Core]");
            Console.NotifyMessage(String.Format("TwitterIrcGateway {0}", asmName.Version));
            Console.NotifyMessage(String.Format("Location: {0}", asm.Location));
            Console.NotifyMessage(String.Format("BaseDirectory: {0}", Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)));

            Console.NotifyMessage("[System]");
            Console.NotifyMessage(String.Format("Operating System: {0}", Environment.OSVersion));
            Console.NotifyMessage(String.Format("Runtime Version: {0}", Environment.Version));

            Console.NotifyMessage("[Session]");
            Console.NotifyMessage(String.Format("ConfigDirectory: {0}", CurrentSession.UserConfigDirectory));
            if (CurrentSession.TwitterUser != null)
            {
                Console.NotifyMessage(String.Format("TwitterUser: {0} ({1})", CurrentSession.TwitterUser.ScreenName,
                                                         CurrentSession.TwitterUser.Id));
            }

            Console.NotifyMessage("[AddIns]");
            foreach (IAddIn addIn in CurrentSession.AddInManager.AddIns)
            {
                Assembly addinAsm = addIn.GetType().Assembly;
                if (addinAsm != asm)
                {
                    Console.NotifyMessage(String.Format("{0} {1}", addIn.GetType().FullName,
                                                             addinAsm.GetName().Version));
                }
            }
        }
    
        [Description("TwitterIrcGatewayへの接続を切断します")]
        public void Disconnect()
        {
            CurrentSession.Close();
        }
        
        [Description("Twitterへの情報取得管理を再起動します")]
        public void RestartTwitterService()
        {
            CurrentSession.TwitterService.Stop();
            CurrentSession.TwitterService.Start();
            Console.NotifyMessage("TwitterService を再起動しました。");
        }
    }
}
