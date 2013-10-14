using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    public class ShortenUrlService : AddInBase
    {
        public Int32 Timeout { get; set; }
        public IShortenUrlProvider ShortenUrlProvider { get; set; }
        public ShortenUrlServiceConfig Config { get; set; }

        public static readonly Int32 DefaultTimeout = 3000;
        public static readonly IShortenUrlProvider DefaultShortenUrlProvider = new BitlyShortenUrlProvider("twitterircgateway", "R_968845d36d8350587f0f7d1045668fe3");

        public override void Initialize()
        {
            CurrentSession.UpdateStatusRequestReceived += CurrentSession_UpdateStatusRequestReceived;
            CurrentSession.AddInsLoadCompleted += (sender, e) =>
                                                      {
                                                          CurrentSession.AddInManager.GetAddIn<ConsoleAddIn>().RegisterContext<ShortenUrlServiceContext>();
                                                      };

            Config = CurrentSession.AddInManager.GetConfig<ShortenUrlServiceConfig>();
            Timeout = DefaultTimeout;
            ShortenUrlProvider = DefaultShortenUrlProvider;

            SetupProvider();
        }

        void CurrentSession_UpdateStatusRequestReceived(object sender, StatusUpdateEventArgs e)
        {
            e.Text = ShortenUrlInMessage(e.Text, Timeout);
        }

        public void SetupProvider()
        {
            ShortenUrlProvider = new NullShortenUrlProvider();
#if FALSE
            if (String.IsNullOrEmpty(Config.BitlyLogin) || String.IsNullOrEmpty(Config.BitlyApiKey))
            {
                ShortenUrlProvider = DefaultShortenUrlProvider;
            }
            else
            {
                ShortenUrlProvider = new BitlyShortenUrlProvider(Config.BitlyLogin, Config.BitlyApiKey);
            }
#endif
        }

        /// <summary>
        /// 文中の URL を短縮 URL に変換します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public String ShortenUrlInMessage(String message, Int32 timeOut)
        {
            return Regex.Replace(message, @"https?://[^ ]+", delegate(Match m)
            {
                return ShortenUrl(m.Value, timeOut);
            }, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// URL を短縮 URL に変換します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        /// <param name="timeOut">タイムアウトするまでの時間</param>
        /// <returns></returns>
        public String ShortenUrl(String url, Int32 timeOut)
        {
            if (ShortenUrlProvider != null)
                return ShortenUrlProvider.ShortenUrl(url, timeOut);
            else
                return url;
        }
    }

    [Description("URL短縮サービスの設定を行うコンテキストに切り替えます")]
    public class ShortenUrlServiceContext : Context
    {
        public override IConfiguration[] Configurations
        {
            get
            {
                return new[] { CurrentSession.AddInManager.GetAddIn<ShortenUrlService>().Config };
            }
        }

        protected override void OnConfigurationChanged(IConfiguration config, System.Reflection.MemberInfo memberInfo, object value)
        {
            CurrentSession.AddInManager.SaveConfig(config);
            CurrentSession.AddInManager.GetAddIn<ShortenUrlService>().SetupProvider();
        }
    }

    public class ShortenUrlServiceConfig : IConfiguration
    {
        [Description("bit.lyのAPI ログインIDを指定します。")]
        public String BitlyLogin { get; set; }
        [Description("bit.lyのAPI ログインIDを指定します。")]
        public String BitlyApiKey { get; set; }
    }

    public interface IShortenUrlProvider
    {
        String ShortenUrl(String url, Int32 timeOut);
    }

    public class NullShortenUrlProvider : IShortenUrlProvider
    {
        public string ShortenUrl(string url, int timeOut)
        {
            return url;
        }
    }

    public class BitlyShortenUrlProvider : IShortenUrlProvider
    {
        private String _login;
        private String _apiKey;

        public BitlyShortenUrlProvider(String login, String apiKey)
        {
            _login = login;
            _apiKey = apiKey;
        }

        #region IShortenUrlProvider メンバー

        public string ShortenUrl(string url, int timeOut)
        {
            if (url.StartsWith("http://bit.ly/") || url.StartsWith("http://j.mp/"))
                return url;

            try
            {
                HttpWebRequest webRequest = HttpWebRequest.Create(String.Format("http://api.bit.ly/v3/shorten?login={0}&apiKey={1}&uri={2}&format=txt", _login, _apiKey, Utility.UrlEncode(url))) as HttpWebRequest;
                webRequest.AllowAutoRedirect = false;
                webRequest.Timeout = timeOut;
                webRequest.Method = "GET";
                using (HttpWebResponse webResponse = webRequest.GetResponse() as HttpWebResponse)
                using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                {
                    return sr.ReadLine().Trim();
                }
            }
            catch (WebException)
            {
                return url;
            }
            catch (IOException)
            {
                return url;
            }
        }

        #endregion
    }
}
