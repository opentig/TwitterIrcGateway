using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Misuzilla.Applications.TwitterIrcGateway;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.UserStream
{
    public class UserStreamAddIn : AddInBase
    {
        private HashSet<Int64> _friendIds;

        private Thread _workerThread;
        private Boolean _isRunning;
        private HttpWebRequest _webRequest;

        public UserStreamConfig Config { get; set; }

        public override void Initialize()
        {
            // XXX:
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePoints = 0;

            CurrentSession.AddInsLoadCompleted += (sender, e) =>
            {
                CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<UserStreamContext>();
                Config = CurrentSession.AddInManager.GetConfig<UserStreamConfig>();
                Setup(Config.Enabled);
            };
        }
        public override void Uninitialize()
        {
            Setup(false);
        }

        internal void Setup(Boolean isStart)
        {
            if (_workerThread != null)
            {
                _isRunning = false;

                if (_webRequest != null)
                {
                    _webRequest.Abort();
                    _webRequest = null;
                }

                _workerThread.Abort();
                _workerThread.Join(200);
                _workerThread = null;
            }

            if (isStart)
            {
                _friendIds = new HashSet<Int64>();
                _workerThread = new Thread(WorkerProcedureEntry);
                _workerThread.Start();
                _isRunning = true;
            }
        }

        private void WorkerProcedureEntry()
        {
            while (true)
            {
                try
                {
                    WorkerProcedure();
                }
                catch (ThreadAbortException)
                {
                    _isRunning = false;
                    // rethrow
                }
                catch (Exception e)
                {
                    CurrentSession.SendServerErrorMessage("UserStream: " + e.Message);
                }

                if (!Config.AutoRestart)
                    break;

                // 適当に 60 秒待機
                Thread.Sleep(60 * 1000);
            }

            _isRunning = false;
        }

        private IEnumerable<JObject> EnumerateJObject(String url)
        {
            _webRequest = CurrentSession.TwitterService.OAuthClient.CreateRequest(
                new Uri(url),
                TwitterOAuth.HttpMethod.GET);
            _webRequest.ServicePoint.ConnectionLimit = 1000;
            _webRequest.Timeout = 30 * 1000;

            using (var response = _webRequest.GetResponse())
            using (var stream = response.GetResponseStream())
            {
                StreamReader sr = new StreamReader(stream, Encoding.UTF8);
                while (!sr.EndOfStream && _isRunning)
                {
                    var line = sr.ReadLine();
                    if (!String.IsNullOrEmpty(line))
                        yield return JsonConvert.DeserializeObject<JObject>(line);
                }
            }
        }

        private void WorkerProcedure()
        {
            var options = new Dictionary<String, String>();

            if (Config.AllAtMode)
                options["replies"] = "all";

            String optionsString = String.Join("&", options.Select(kv => String.Format("{0}={1}", kv.Key, kv.Value)).ToArray());

            String url = "https://userstream.twitter.com/1.1/user.json";
            if (!String.IsNullOrEmpty(optionsString))
                url += "?" + optionsString;

            var isFirst = true;
            foreach (var jsonObject in EnumerateJObject(url))
            {
                if (isFirst)
                {
                    isFirst = false;
                    var friendsObject = jsonObject.ToObject<FriendsObject>();
                    _friendIds.UnionWith(friendsObject.Friends);
                }
                else
                {
                    if (jsonObject["user"] != null)
                    {
                        var statusObject = jsonObject.ToObject<Status>();
                        OnTweet(statusObject);
                    }
                    else if (jsonObject["event"] != null)
                    {
                        var eventObject = jsonObject.ToObject<EventObject>();
                        OnEvent(eventObject);
                    }
                }
            }
        }

        private void OnTweet(Status status)
        {
            if (Config.IsThroughMyPostFromUserStream && status.Id == CurrentSession.TwitterUser.Id)
                return;

            Boolean friendCheckRequired = false;
            CurrentSession.TwitterService.ProcessStatus(status,
                    (s) => CurrentSession.ProcessTimelineStatus(s, ref friendCheckRequired, false, false));
        }

        private void OnEvent(EventObject eventObject)
        {
            // CurrentSession.SendGatewayServerMessage(String.Format("OnEvent: {0}", eventObject.Event));

            switch (eventObject.Event)
            {
                case "favorite":
                    OnFavoriteEvent(eventObject);
                    break;

                case "follow":
                    OnFollowEvent(eventObject);
                    break;
            }
        }

        private void OnFavoriteEvent(EventObject eventObject)
        {
            var source = eventObject.Source.ToObject<User>();
            var target = eventObject.Target.ToObject<User>();
            var status = eventObject.TargetObject.ToObject<Status>();

            if (target.Id == CurrentSession.TwitterUser.Id)
            {
                if (Config.ShowEvent)
                {
                    var prefix = SetColor(String.Format("★ Fav @{0}:", target.ScreenName), Config.FavoriteColor);
                    status.User = source;
                    status.Text = String.Format("{0} {1}", prefix, status.Text);

                    Boolean friendCheckRequired = false;
                    CurrentSession.ProcessTimelineStatus(status, ref friendCheckRequired, true, false);
                }
            }
        }

        private void OnFollowEvent(EventObject eventObject)
        {
            var source = eventObject.Source.ToObject<User>();
            var target = eventObject.Target.ToObject<User>();

            if (target.Id == CurrentSession.TwitterUser.Id)
            {
                _friendIds.Add(source.Id);
            }
        }

        private String SetColor(String s, Int32? color)
        {
            if (color.HasValue)
            {
                return String.Format("\x03{0}{1}\x03", color.Value, s);
            }
            else
            {
                return s;
            }
        }
    }

    [Description("User Stream設定コンテキストに切り替えます")]
    public class UserStreamContext : Context
    {
        public override IConfiguration[] Configurations
        {
            get
            {
                return new[] { CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Config };
            }
        }

        [Description("User Stream を有効にします")]
        public void Enable()
        {
            var config = CurrentSession.AddInManager.GetConfig<UserStreamConfig>();
            config.Enabled = true;
            CurrentSession.AddInManager.SaveConfig(config);
            CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Setup(config.Enabled);
            Console.NotifyMessage("User Stream を有効にしました。");
        }

        [Description("User Stream を無効にします")]
        public void Disable()
        {
            var config = CurrentSession.AddInManager.GetConfig<UserStreamConfig>();
            config.Enabled = false;
            CurrentSession.AddInManager.SaveConfig(config);
            CurrentSession.AddInManager.GetAddIn<UserStreamAddIn>().Setup(config.Enabled);
            Console.NotifyMessage("User Stream を無効にしました。");
        }

        protected override void OnConfigurationChanged(IConfiguration config, MemberInfo memberInfo, object value)
        {
            CurrentSession.AddInManager.SaveConfig(config);
        }
    }

    public class UserStreamConfig : IConfiguration
    {
        [Browsable(false)]
        public Boolean Enabled { get; set; }

        [Description("all@と同じ挙動になるかどうかを指定します。")]
        public Boolean AllAtMode { get; set; }

        [Description("自分のポストをUser Streamから拾わないようにするかどうかを指定します。")]
        public Boolean IsThroughMyPostFromUserStream { get; set; }

        [Description("切断された際に自動的に再接続を試みるかどうかを指定します。")]
        public Boolean AutoRestart { get; set; }

        [Description("イベントを表示するかどうかを指定します。")]
        public Boolean ShowEvent { get; set; }

        [Description("お気に入りイベントの文字色を指定します。")]
        public Int32? FavoriteColor { get; set; }
    }

    class FriendsObject
    {
        [JsonProperty("friends")]
        public Int64[] Friends { get; set; }
    }

    class EventTarget
    {
        [JsonProperty("id")]
        public Int64 Id { get; set; }
    }

    class EventObject
    {
        [JsonProperty("event")]
        public String Event { get; set; }

        [JsonProperty("target")]
        public JObject Target { get; set; }

        [JsonProperty("source")]
        public JObject Source { get; set; }

        [JsonProperty("target_object")]
        public JObject TargetObject { get; set; }
    }
}
