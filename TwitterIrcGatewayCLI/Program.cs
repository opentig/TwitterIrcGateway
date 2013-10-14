using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Reflection;
using System.Threading;
using System.ComponentModel;

using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Utilities;

namespace TwitterIrcGatewayCLI
{
    class Program
    {
        static CommandLineParser<CommandLineOptions> CommandLineParser = new CommandLineParser<CommandLineOptions>();
        static void Main(string[] args)
        {
            IPAddress bindAddress = IPAddress.Loopback;
            Encoding encoding = Encoding.GetEncoding("ISO-2022-JP");
            IWebProxy proxy = WebRequest.DefaultWebProxy;

            CommandLineOptions options;
            if (CommandLineParser.TryParse(args, out options))
            {
                // Encoding
                if (String.Compare(options.Encoding, "UTF-8", true) == 0)
                    encoding = new UTF8Encoding(false);
                else
                    encoding = Encoding.GetEncoding(options.Encoding);

                // Listening IP
                if (!IPAddress.TryParse(options.BindAddress, out bindAddress))
                {
                    ShowUsage();
                    return;
                }

                // Proxy
                try
                {
                    if (!String.IsNullOrEmpty(options.Proxy))
                        proxy = new WebProxy(options.Proxy);
                }
                catch (UriFormatException)
                {
                    ShowUsage();
                    return;
                }
            }
            else
            {
                ShowUsage();
                return;
            }

            Config.Default.EnableTrace = options.EnableTrace;
            Config.Default.IgnoreWatchError = options.IgnoreWatchError;
            Config.Default.Interval = options.Interval;
            Config.Default.ResolveTinyUrl = options.ResolveTinyurl;
            Config.Default.EnableDropProtection = options.EnableDropProtection;
            Config.Default.SetTopicOnStatusChanged = options.SetTopicOnstatuschanged;
            Config.Default.IntervalDirectMessage = options.IntervalDirectmessage;
            //Config.Default.CookieLoginMode = options.CookieLoginMode;
            Config.Default.ChannelName = "#"+options.ChannelName;
            Config.Default.EnableRepliesCheck = options.EnableRepliesCheck;
            Config.Default.IntervalReplies = options.IntervalReplies;
            Config.Default.DisableUserList = options.DisableUserlist;
            Config.Default.BroadcastUpdate = options.BroadcastUpdate;
            Config.Default.ClientMessageWait = options.ClientMessageWait;
            Config.Default.BroadcastUpdateMessageIsNotice = options.BroadcastUpdateMessageIsNotice;
            Config.Default.POSTFetchMode = options.PostFetchMode;
            Config.Default.EnableCompression = options.EnableCompression;
            Config.Default.DisableNoticeAtFirstTime = options.DisableNoticeAtFirstTime;

            Server _server = new Server();
            _server.Encoding = encoding;
            _server.ConnectionAttached += new EventHandler<ConnectionAttachEventArgs>(_server_ConnectionAttached);
            _server.Proxy = proxy;
            if (!String.IsNullOrEmpty(options.OAuthClientKey))
                _server.OAuthClientKey = options.OAuthClientKey;
            if (!String.IsNullOrEmpty(options.OAuthSecretKey))
                _server.OAuthSecretKey = options.OAuthSecretKey;

            Console.WriteLine("Start TwitterIrcGateway Server v{0}", typeof(Server).Assembly.GetName().Version);
            Console.WriteLine("[Configuration] BindAddress: {0}, Port: {1}", bindAddress, options.Port);
            Console.WriteLine("[Configuration] EnableTrace: {0}", Config.Default.EnableTrace);
            Console.WriteLine("[Configuration] IgnoreWatchError: {0}", Config.Default.IgnoreWatchError);
            Console.WriteLine("[Configuration] Interval: {0}", Config.Default.Interval);
            Console.WriteLine("[Configuration] ResolveTinyUrl: {0}", Config.Default.ResolveTinyUrl);
            Console.WriteLine("[Configuration] Encoding: {0}", _server.Encoding.EncodingName);
            Console.WriteLine("[Configuration] SetTopicOnStatusChanged: {0}", Config.Default.SetTopicOnStatusChanged);
            Console.WriteLine("[Configuration] EnableDropProtection: {0}", Config.Default.EnableDropProtection);
            Console.WriteLine("[Configuration] IntervalDirectMessage: {0}", Config.Default.IntervalDirectMessage);
            //Console.WriteLine("[Configuration] CookieLoginMode: {0}", Config.Default.CookieLoginMode);
            Console.WriteLine("[Configuration] ChannelName: {0}", Config.Default.ChannelName);
            Console.WriteLine("[Configuration] EnableRepliesCheck: {0}", Config.Default.EnableRepliesCheck);
            Console.WriteLine("[Configuration] IntervalReplies: {0}", Config.Default.IntervalReplies);
            Console.WriteLine("[Configuration] DisableUserList: {0}", Config.Default.DisableUserList);
            Console.WriteLine("[Configuration] BroadcastUpdate: {0}", Config.Default.BroadcastUpdate);
            Console.WriteLine("[Configuration] ClientMessageWait: {0}", Config.Default.ClientMessageWait);
            Console.WriteLine("[Configuration] BroadcastUpdateMessageIsNotice: {0}", Config.Default.BroadcastUpdateMessageIsNotice);
            Console.WriteLine("[Configuration] Proxy: {0}", options.Proxy);
            Console.WriteLine("[Configuration] PostFetchMode: {0}", options.PostFetchMode);
            Console.WriteLine("[Configuration] EnableCompression: {0}", options.EnableCompression);
            Console.WriteLine("[Configuration] DisableNoticeAtFirstTime: {0}", options.DisableNoticeAtFirstTime);
            Console.WriteLine("[Configuration] OAuthClientKey: {0}", options.OAuthClientKey);
            Console.WriteLine("[Configuration] OAuthSecretKey: {0}", options.OAuthSecretKey);

            _server.Start(bindAddress, options.Port);

            while (true)
                Thread.Sleep(1000);
        }

        static void _server_ConnectionAttached(object sender, ConnectionAttachEventArgs e)
        {
        }

        private static void ShowUsage() 
        {
            Console.WriteLine("TwitterIrcGateway Server v{0}", typeof(Server).Assembly.GetName().Version);
            Console.WriteLine(@"Usage:");
            CommandLineParser.ShowHelp();
        }
    }

    class CommandLineOptions
    {
        [DefaultValue(16668)]
        [Description("IRC server listen port")]
        public Int32 Port { get; set; }

        [DefaultValue("127.0.0.1")]
        [Description("IRC server bind IP address")]
        public String BindAddress { get; set; }

        [DefaultValue(90)]
        [Description("interval of checking Timeline")]
        public Int32 Interval { get; set; }

        [DefaultValue(true)]
        [Description("enable TinyURL resolver")]
        public Boolean ResolveTinyurl { get; set; }

        [DefaultValue("ISO-2022-JP")]
        [Description("IRC message text character encoding")]
        public String Encoding { get; set; }

        [DefaultValue(false)]
        [Description("ignore API error messages")]
        public Boolean IgnoreWatchError { get; set; }

        [DefaultValue(true)]
        [Description("enable drop protection")]
        public Boolean EnableDropProtection { get; set; }

        [DefaultValue(false)]
        [Description("set status as topic on status changed")]
        public Boolean SetTopicOnstatuschanged { get; set; }

        [DefaultValue(false)]
        [Description("enable trace")]
        public Boolean EnableTrace { get; set; }

        [DefaultValue(180)]
        [Description("interval of checking directmessage")]
        public Int32 IntervalDirectmessage { get; set; }

        [DefaultValue(false)]
        [Description("enable cookie-login mode (Obsolete / not working)")]
        public Boolean CookieLoginMode { get; set; }

        [DefaultValue("Twitter")]
        [Description("channel name of Twitter timeline")]
        public String ChannelName { get; set; }

        [DefaultValue(false)]
        [Description("enable replies check")]
        public Boolean EnableRepliesCheck { get; set; }

        [DefaultValue(300)]
        [Description("interval of checking Replies")]
        public Int32 IntervalReplies { get; set; }

        [DefaultValue(false)]
        [Description("disable nick/user (following) list")]
        public Boolean DisableUserlist { get; set; }

        [DefaultValue(false)]
        [Description("broadcast status message on updated")]
        public Boolean BroadcastUpdate { get; set; }

        [DefaultValue(0)]
        [Description("wait of send messages to client (milliseconds)")]
        public Int32 ClientMessageWait { get; set; }

        [DefaultValue(false)]
        [Description("broadcast status message type is NOTICE")]
        public Boolean BroadcastUpdateMessageIsNotice { get; set; }

        [DefaultValue("")]
        [Description("HTTP proxy server URL (http://host:port)")]
        public String Proxy { get; set; }

        [DefaultValue(false)]
        [Description("fetch data by POST method")]
        public Boolean PostFetchMode { get; set; }

        [DefaultValue(false)]
        [Description("Use gzip compression at Web request")]
        public Boolean EnableCompression { get; set; }

        [DefaultValue(false)]
        [Description("Disable using NOTICE at first time")]
        public Boolean DisableNoticeAtFirstTime { get; set; }

        [DefaultValue("")]
        [Description("OAuth Client Key")]
        public String OAuthClientKey { get; set; }

        [DefaultValue("")]
        [Description("OAuth Secret Key")]
        public String OAuthSecretKey { get; set; }
    }
}
