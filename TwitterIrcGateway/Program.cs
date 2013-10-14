using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using Misuzilla.Net.Irc;
using System.Web;
using System.Threading;
using System.Xml;
using System.Diagnostics;
using System.Windows.Forms;
using System.Reflection;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    class Program : ApplicationContext
    {
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Program program = new Program();
            if (program.Initialize())
            {
                Application.Run(program);
            }
        }

        private Settings _settings = new Settings();
        private Server _server;
        private NotifyIcon _notifyIcon;
        private const String Name = "Twitter IRC Gateway Server";

        public Boolean Initialize()
        {
            Application.ApplicationExit += new EventHandler(Application_ApplicationExit);
            Int32 port = _settings.Port;
            IPAddress ipAddr = _settings.LocalOnly ? IPAddress.Loopback : IPAddress.Any;

            ContextMenu ctxMenu = new ContextMenu();
            ctxMenu.MenuItems.Add(new MenuItem("終了(&X)", ContextMenuItemExit));
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = String.Format("{0} (IP: {1} / ポート {2})", Name, ipAddr, port);
            _notifyIcon.ContextMenu = ctxMenu;
            _notifyIcon.Icon = Resource.ApplicationIcon;

            Config.Default.EnableTrace = _settings.EnableTrace;
            Config.Default.IgnoreWatchError = _settings.IgnoreWatchError;
            Config.Default.Interval = _settings.Interval;
            Config.Default.ResolveTinyUrl = _settings.ResolveTinyUrl;
            Config.Default.EnableDropProtection = _settings.EnableDropProtection;
            Config.Default.SetTopicOnStatusChanged = _settings.SetTopicOnStatusChanged;
            Config.Default.IntervalDirectMessage = _settings.IntervalDirectMessage;
            //Config.Default.CookieLoginMode = _settings.CookieLoginMode;
            Config.Default.ChannelName = "#"+_settings.TwitterChannelName;
            Config.Default.EnableRepliesCheck = _settings.EnableRepliesCheck;
            Config.Default.IntervalReplies = _settings.IntervalReplies;
            Config.Default.DisableUserList = _settings.DisableUserList;
            Config.Default.BroadcastUpdate = _settings.BroadcastUpdate;
            Config.Default.ClientMessageWait = _settings.ClientMessageWait;
            Config.Default.BroadcastUpdateMessageIsNotice = _settings.BroadcastUpdateMessageIsNotice;
            Config.Default.POSTFetchMode = _settings.POSTFetchMode;
            Config.Default.EnableCompression = _settings.EnableCompression;
            Config.Default.DisableNoticeAtFirstTime = _settings.DisableNoticeAtFirstTime;

            _server = new Server();
            _server.ConnectionAttached += new EventHandler<ConnectionAttachEventArgs>(_server_ConnectionAttached);
            if (!String.IsNullOrEmpty(_settings.OAuthClientKey))
                _server.OAuthClientKey = _settings.OAuthClientKey;
            if (!String.IsNullOrEmpty(_settings.OAuthSecretKey))
                _server.OAuthSecretKey = _settings.OAuthSecretKey;
            try
            {
                _server.Encoding = (String.Compare(_settings.Charset, "UTF-8", true) == 0)
                    ? new UTF8Encoding(false) // BOM なし
                    : Encoding.GetEncoding(_settings.Charset);
            }
            catch (ArgumentException)
            {
                _server.Encoding = Encoding.GetEncoding("ISO-2022-JP");
            }

            // start
            try
            {
                _server.Start(ipAddr, port);
                _notifyIcon.Visible = true;
                _notifyIcon.ShowBalloonTip(1000 * 10, Name, String.Format("IRCサーバがポート {0} で開始されました。", port), ToolTipIcon.Info);
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message, Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        void Application_ApplicationExit(object sender, EventArgs e)
        {
            _server.Stop();
            _notifyIcon.Visible = false;
        }

        void _server_ConnectionAttached(object sender, ConnectionAttachEventArgs e)
        {
            _notifyIcon.ShowBalloonTip(1000 * 10, Name, String.Format("ユーザ {0} がサーバに接続しました。", ((Connection)(e.Connection)).TwitterUser.ScreenName), ToolTipIcon.Info);
        }


        private void ContextMenuItemExit(Object sender, EventArgs e)
        {
            Application.Exit();
        }

        /// <summary>
        /// ハンドルされていない例外をキャッチして、スタックトレースを保存してデバッグに役立てる
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception; // Exception 以外が飛んでくるのは超特殊な場合のみ。

            if (MessageBox.Show(
                String.Format("アプリケーションの実行中に予期しない重大なエラーが発生しました。\n\nエラー内容:\n{0}\n\nエラー情報をファイルに保存し、報告していただくことで不具合の解決に役立つ可能性があります。エラー情報をファイルに保存しますか?",
                ((Exception)(e.ExceptionObject)).Message)
                , Application.ProductName
                , MessageBoxButtons.YesNo
                , MessageBoxIcon.Error
                , MessageBoxDefaultButton.Button1
                ) == DialogResult.Yes)
            {
                using (SaveFileDialog saveFileDialog = new SaveFileDialog())
                {
                    saveFileDialog.DefaultExt = "txt";
                    saveFileDialog.Filter = "テキストファイル|*.txt";
                    saveFileDialog.FileName = String.Format("twitterircgateway_stacktrace_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now);
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        using (Stream stream = saveFileDialog.OpenFile())
                        using (StreamWriter sw = new StreamWriter(stream))
                        {
                            Assembly asm = Assembly.GetExecutingAssembly();

                            sw.WriteLine("発生時刻: {0}", DateTime.Now);
                            sw.WriteLine();
                            sw.WriteLine("TwitterIrcGateway:");
                            sw.WriteLine("========================");
                            sw.WriteLine("バージョン: {0}", Assembly.GetExecutingAssembly().GetName().Version);
                            sw.WriteLine("アセンブリ: {0}", Assembly.GetExecutingAssembly().FullName);
                            sw.WriteLine();
                            sw.WriteLine("環境情報:");
                            sw.WriteLine("========================");
                            sw.WriteLine("オペレーティングシステム: {0}", Environment.OSVersion);
                            sw.WriteLine("Microsoft .NET Framework: {0}", Environment.Version);
                            sw.WriteLine(); 
                            sw.WriteLine("ハンドルされていない例外: ");
                            sw.WriteLine("=========================");
                            sw.WriteLine(ex.ToString());
                        }
                    }
                }

            }
        }

    }
}
