using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Xml;
using OAuth;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class TwitterIdentity : MarshalByRefObject, IIdentity
    {
        public String ScreenName { get; set; }
        public Int32 UserId { get; set; }
        public String Token { get; set; }
        public String TokenSecret { get; set; }
        
        #region IIdentity メンバ
        public string AuthenticationType
        {
            get { return "OAuth"; }
        }

        public bool IsAuthenticated
        {
            get { return true; }
        }

        public string Name
        {
            get { return ScreenName; }
        }
        #endregion
    }

    /// <summary>
    /// リクエストのレートリミット情報のクラスです。
    /// </summary>
    public class RateLimitStatus
    {
        /// <summary>
        /// リセットされる時刻。
        /// </summary>
        public DateTime Reset { get; set; }
        /// <summary>
        /// 残り回数
        /// </summary>
        public Int32 Remaining { get; set; }
    }

    /// <summary>
    /// TwitterへのOAuthアクセスを提供するクラスです。
    /// </summary>
    public class TwitterOAuth : OAuthBase
    {
        public enum HttpMethod
        {
            GET, POST
        }

        private Dictionary<String, RateLimitStatus> _rateLimitStatuses = new Dictionary<String, RateLimitStatus>();
        private String _consumerKey;
        private String _consumerSecret;
        private static readonly Uri RequestTokenUrl = new Uri("https://api.twitter.com/oauth/request_token");
        private static readonly Uri AuthorizeUrl = new Uri("https://api.twitter.com/oauth/authorize");
        private static readonly Uri AccessTokenUrl = new Uri("https://api.twitter.com/oauth/access_token");

        /// <summary>
        /// リクエストに利用するOAuthトークンを取得・設定します。
        /// </summary>
        public String Token { get; set; }
        /// <summary>
        /// リクエストに利用するOAuthシークレットトークンを取得・設定します。
        /// </summary>
        public String TokenSecret { get; set; }
        /// <summary>
        /// gzip圧縮を有効にするかどうかを取得・設定します。
        /// </summary>
        public Boolean EnableCompression { get; set; }

        public TwitterOAuth(String consumerKey, String consumerSecret)
        {
            _consumerKey = consumerKey;
            _consumerSecret = consumerSecret;
        }

        #region Step 1 (Request Unauthorized Token)
        public String GetAuthorizeUrl()
        {
            return AuthorizeUrl + "?oauth_token=" + RequestUnauthorizedToken();
        }
        public String GetAuthorizeUrl(out String authToken)
        {
            authToken = RequestUnauthorizedToken();
            return AuthorizeUrl + "?oauth_token=" + authToken;
        }
    
        public String RequestUnauthorizedToken()
        {
            String result = Request(RequestTokenUrl, HttpMethod.GET);
            NameValueCollection returnValues = new NameValueCollection();
            foreach (var keyValue in result.Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(p => p.Split(new[] { '=' }, 2))
                                           .Where(p => p.Length == 2))
            {
                returnValues[keyValue[0]] = keyValue[1];
            }
            return returnValues["oauth_token"];
        }
        #endregion

        #region Step 2 (Request Access Token & Setup TwitterOAuth Client)
        public TwitterIdentity RequestAccessToken(String authToken, String verifier)
        {
            Verifier = verifier;
            String result = ReadResponse(RequestInternal(AccessTokenUrl, HttpMethod.GET, authToken, String.Empty, String.Empty));
            NameValueCollection returnValues = new NameValueCollection();
            foreach (var keyValue in result.Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(p => p.Split(new[] { '=' }, 2))
                                           .Where(p => p.Length == 2))
            {
                returnValues[keyValue[0]] = keyValue[1];
            }

            TwitterIdentity identity = new TwitterIdentity()
                                           {
                                               Token = returnValues["oauth_token"],
                                               TokenSecret = returnValues["oauth_token_secret"],
                                               ScreenName = returnValues["screen_name"],
                                               UserId = Int32.Parse(returnValues["user_id"])
                                           };
            return identity;
        }

        public TwitterIdentity RequestAccessToken(String authToken, String verifier, Dictionary<String, String> parameters)
        {
            UriBuilder newUri = new UriBuilder(AccessTokenUrl);
            newUri.Query = ((newUri.Query.Length > 0) ? "&" : "") + String.Join("&", parameters.Select(kv => String.Concat(Utility.UrlEncode(kv.Key), "=", Utility.UrlEncode(kv.Value))).ToArray());

            Verifier = verifier;
            String result = ReadResponse(RequestInternal(newUri.Uri, HttpMethod.POST, authToken, String.Empty, String.Empty));
            NameValueCollection returnValues = new NameValueCollection();
            foreach (var keyValue in result.Split(new[] { '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                           .Select(p => p.Split(new[] { '=' }, 2))
                                           .Where(p => p.Length == 2))
            {
                returnValues[keyValue[0]] = keyValue[1];
            }

            TwitterIdentity identity = new TwitterIdentity()
            {
                Token = returnValues["oauth_token"],
                TokenSecret = returnValues["oauth_token_secret"],
                ScreenName = returnValues["screen_name"],
                UserId = Int32.Parse(returnValues["user_id"])
            };
            return identity;
        }
        #endregion

        /// <summary>
        /// リソースへのアクセスが可能かどうかを返します。
        /// </summary>
        /// <param name="resourceEndpoint"></param>
        /// <returns></returns>
        public Boolean CanRequest(String resourceEndpoint)
        {
            if (_rateLimitStatuses.ContainsKey(resourceEndpoint))
            {
                // リセット時間を過ぎているか残り回数が0以上だったら。
                return (_rateLimitStatuses[resourceEndpoint].Reset <= DateTime.Now ||
                        _rateLimitStatuses[resourceEndpoint].Remaining > 0);
            }
            return true;
        }

        /// <summary>
        /// リソースへアクセスし、レスポンスボディを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public String Request(Uri requestUrl, HttpMethod method)
        {
            return ReadResponse(RequestInternal(requestUrl, method, Token, TokenSecret, String.Empty));
        }

        /// <summary>
        /// パラメータを指定してリソースへアクセスし、レスポンスボディを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public String Request(Uri requestUrl, HttpMethod method, Dictionary<String, String> parameters)
        {
            return Request(requestUrl, method, parameters, null);
        }

        /// <summary>
        /// パラメータを指定してリソースへアクセスし、レスポンスボディを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <param name="resourceEndpoint">Rate Limitを認識するためのリソースのエンドポイント</param>
        /// <returns></returns>
        public String Request(Uri requestUrl, HttpMethod method, Dictionary<String, String> parameters, String resourceEndpoint)
        {
            return Request(requestUrl, method, String.Join("&", parameters.Select(kv => String.Concat(Utility.UrlEncode(kv.Key), "=", Utility.UrlEncode(kv.Value))).ToArray()), resourceEndpoint);
        }

        /// <summary>
        /// パラメータを指定してリソースへアクセスし、レスポンスボディを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <param name="resourceEndpoint">Rate Limitを認識するためのリソースのエンドポイント</param>
        /// <returns></returns>
        public String Request(Uri requestUrl, HttpMethod method, String parameters, String resourceEndpoint)
        {
            if (!String.IsNullOrEmpty(resourceEndpoint))
            {
                if (!CanRequest(resourceEndpoint))
                {
                    throw new Exception(String.Format("APIのリクエスト制限回数に達しました。({0}; リセット:{1})", resourceEndpoint, _rateLimitStatuses[resourceEndpoint].Reset));
                }
            }

            UriBuilder newUri = new UriBuilder(requestUrl);
            newUri.Query = newUri.Query.TrimStart('?') + ((newUri.Query.Length > 0) ? "&" : "") + parameters;

            var request = RequestInternal(newUri.Uri, method, Token, TokenSecret, parameters);

            Action<WebResponse> setRateLimit = (WebResponse response) =>
                                   {
                                       if (!String.IsNullOrEmpty(resourceEndpoint))
                                       {
                                           if (!_rateLimitStatuses.ContainsKey(resourceEndpoint))
                                           {
                                               _rateLimitStatuses[resourceEndpoint] = new RateLimitStatus() { Remaining = 15 };
                                           }
                                           Int32 remaining;
                                           Int64 reset;
                                           if (Int32.TryParse(response.Headers["X-Rate-Limit-Remaining"], out remaining))
                                           {
                                               _rateLimitStatuses[resourceEndpoint].Remaining = remaining;
                                           }
                                           if (Int64.TryParse(response.Headers["X-Rate-Limit-Reset"], out reset))
                                           {
                                               _rateLimitStatuses[resourceEndpoint].Reset = new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime().AddSeconds(reset);
                                           }
                                       }
                                   };

            try
            {
                var response = request.GetResponse();
                setRateLimit(response);
            }
            catch (WebException webE)
            {
                setRateLimit(webE.Response);
                throw;
            }

            return ReadResponse(request);
        }

        /// <summary>
        /// リソースへアクセス開始し、HttpWebRequestを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <returns></returns>
        public HttpWebRequest CreateRequest(Uri requestUrl, HttpMethod method)
        {
            return RequestInternal(requestUrl, method, Token, TokenSecret, String.Empty);
        }
        
        /// <summary>
        /// リソースへアクセス開始し、HttpWebRequestを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public HttpWebRequest CreateRequest(Uri requestUrl, HttpMethod method, Dictionary<String, String> parameters)
        {
            return CreateRequest(requestUrl, method, String.Join("&", parameters.Select(kv => String.Concat(Utility.UrlEncode(kv.Key), "=", Utility.UrlEncode(kv.Value))).ToArray()));
        }

        /// <summary>
        /// リソースへアクセス開始し、HttpWebRequestを返します。
        /// </summary>
        /// <param name="requestUrl"></param>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public HttpWebRequest CreateRequest(Uri requestUrl, HttpMethod method, String parameters)
        {
            UriBuilder newUri = new UriBuilder(requestUrl);
            if (method == HttpMethod.GET)
                newUri.Query = ((newUri.Query.Length > 0) ? "&" : "") + parameters;

            return RequestInternal(newUri.Uri, method, Token, TokenSecret, parameters);
        }

        public static String GetMessageFromException(Exception e)
        {
            if (e is WebException)
            {
                using (HttpWebResponse webResponse = (e as WebException).Response as HttpWebResponse)
                {
                    if (webResponse != null &&
                        webResponse.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        try
                        {
                            String body = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
                            return body;
                        }
                        catch (IOException)
                        {
                        }
                    }
                }
            }
            return e.Message;
        }

        #region Internal Implementation
        private HttpWebRequest RequestInternal(Uri requestUrl, HttpMethod method, String token, String tokenSecret, String parameters)
        {
            String normalizedUrl, queryString;

            String signature = GenerateSignature(requestUrl,
                                                 _consumerKey,
                                                 _consumerSecret,
                                                 token,
                                                 tokenSecret,
                                                 method.ToString(),
                                                 GenerateTimeStamp(),
                                                 GenerateNonce(),
                                                 out normalizedUrl,
                                                 out queryString);

            queryString += "&oauth_signature=" + UrlEncode(signature);

            if (method == HttpMethod.GET)
            {
                return RequestInternalGet(normalizedUrl + "?" + queryString);
            }
            else
            {
                return RequestInternalPost(normalizedUrl, queryString, parameters);
            }
        }

        private String ReadResponse(HttpWebRequest webRequest)
        {
            using (var response = webRequest.GetResponse() as HttpWebResponse)
            {
                var isGzipped = String.Compare(response.ContentEncoding, "gzip", true) == 0;
                var reader = new StreamReader(isGzipped ? new GZipStream(response.GetResponseStream(), CompressionMode.Decompress) : response.GetResponseStream());
                return reader.ReadToEnd();
            }
        }

        private HttpWebRequest RequestInternalGet(String uri)
        {
            HttpWebRequest webRequest = WebRequest.Create(uri) as HttpWebRequest;
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.Timeout = 30 * 1000;
            webRequest.Method = "GET";
            if (EnableCompression)
                webRequest.Headers["Accept-Encoding"] = "gzip";
            return webRequest;
        }

        private HttpWebRequest RequestInternalPost(String uri, String authKeys, String postData)
        {
            HttpWebRequest webRequest = WebRequest.Create(uri) as HttpWebRequest;
            webRequest.ServicePoint.Expect100Continue = false;
            webRequest.Timeout = 30 * 1000;
            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.Headers["Authorization"] = "OAuth realm=\"\", " +
                                                  String.Join(", ",
                                                    authKeys.Split('&')
                                                            .Select(x => { var parts = x.Split(new[] { '=' }, 2); return parts[0] + "=\"" + parts[1] + "\""; })
                                                            .ToArray());
            using (Stream stream = webRequest.GetRequestStream())
            {
                Byte[] bytes = new UTF8Encoding(false).GetBytes(postData);
                stream.Write(bytes, 0, bytes.Length);
            }
            return webRequest;
        }
        #endregion
    }
}
