using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace TwitterIrcGatewayService
{
    public partial class TwitterIrcGatewayService : ServiceBase
    {
        private Server _server;
        private Server _sslServer;

        public TwitterIrcGatewayService()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            EventLog.WriteEntry("ハンドルしていない例外が発生しました:\n\n" + e.ExceptionObject.ToString(), EventLogEntryType.Error, 9100);
        }

        protected override void OnStart(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 1000;

            _server = new Server();
            _server.Encoding = new UTF8Encoding(false);
            //_server.Encoding = encoding;
            _server.ConnectionAttached += new EventHandler<ConnectionAttachEventArgs>(_server_ConnectionAttached);
            //_server.Proxy = proxy;

            // gzip を有効に
            Config.Default.EnableCompression = true;

            StringWriter sw = new StringWriter();
            sw.WriteLine("TwitterIrcGateway Server v{0} を開始しました。", typeof(Server).Assembly.GetName().Version);
            sw.WriteLine();
            //sw.WriteLine(" BindAddress: {0}, Port: {1}", bindAddress, options.Port);
            sw.WriteLine("EnableTrace: {0}", Config.Default.EnableTrace);
            sw.WriteLine("IgnoreWatchError: {0}", Config.Default.IgnoreWatchError);
            sw.WriteLine("Interval: {0}", Config.Default.Interval);
            sw.WriteLine("ResolveTinyUrl: {0}", Config.Default.ResolveTinyUrl);
            sw.WriteLine("Encoding: {0}", _server.Encoding.EncodingName);
            sw.WriteLine("SetTopicOnStatusChanged: {0}", Config.Default.SetTopicOnStatusChanged);
            sw.WriteLine("EnableDropProtection: {0}", Config.Default.EnableDropProtection);
            sw.WriteLine("IntervalDirectMessage: {0}", Config.Default.IntervalDirectMessage);
            //sw.WriteLine("CookieLoginMode: {0}", Config.Default.CookieLoginMode);
            sw.WriteLine("ChannelName: {0}", Config.Default.ChannelName);
            sw.WriteLine("EnableRepliesCheck: {0}", Config.Default.EnableRepliesCheck);
            sw.WriteLine("IntervalReplies: {0}", Config.Default.IntervalReplies);
            sw.WriteLine("DisableUserList: {0}", Config.Default.DisableUserList);
            sw.WriteLine("BroadcastUpdate: {0}", Config.Default.BroadcastUpdate);
            sw.WriteLine("ClientMessageWait: {0}", Config.Default.ClientMessageWait);
            sw.WriteLine("BroadcastUpdateMessageIsNotice: {0}", Config.Default.BroadcastUpdateMessageIsNotice);
            sw.WriteLine("EnableCompression: {0}", Config.Default.EnableCompression);
//            sw.WriteLine("Proxy: {0}", options.Proxy);
//            sw.WriteLine("PostFetchMode: {0}", options.PostFetchMode);

            Settings settings = new Settings();
            _server.Start(IPAddress.Parse(settings.BindAddress), settings.Port);
            if (settings.SslPort > 0)
            {
                _sslServer = new Server(true);
                _sslServer.ConnectionAttached += _server_ConnectionAttached;
                _sslServer.Encoding = _server.Encoding;
                _sslServer.Certificate = new X509Certificate2(settings.CertFilename, "");
                _sslServer.Start(IPAddress.Parse(settings.BindAddress), settings.SslPort);
            }

            EventLog.WriteEntry(sw.ToString(), EventLogEntryType.Information, 0);
        }

        void _server_ConnectionAttached(object sender, ConnectionAttachEventArgs e)
        {
            StringWriter sw = new StringWriter();
            User twitterUser = ((Connection)(e.Connection)).TwitterUser;
            sw.WriteLine("ユーザ {0} が接続しました。", twitterUser.ScreenName);
            sw.WriteLine();
            sw.WriteLine("IP: {0}", e.Connection.UserInfo.EndPoint);
            sw.WriteLine("Twitter User: {0} (ID:{1})", twitterUser.ScreenName, twitterUser.Id);
            EventLog.WriteEntry(sw.ToString(), EventLogEntryType.Information, 1000);
        }


        protected override void OnStop()
        {
            EventLog.WriteEntry("TwitterIrcGateway を停止しています。", EventLogEntryType.Information, 9000);

            try
            {
                if (_sslServer != null)
                    _sslServer.StopListen();

                _server.Stop();

                if (_sslServer != null)
                    _sslServer.Stop();
            }
            catch (Exception e)
            {
                EventLog.WriteEntry("停止中にエラーが発生しました:\n\n" + e.ToString(), EventLogEntryType.Error, 9100);
//                throw;
            }

            EventLog.WriteEntry("TwitterIrcGateway を停止しました。", EventLogEntryType.Information, 9001);
        }
    }
}
