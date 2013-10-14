using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Misuzilla.Applications.TwitterIrcGateway.Authentication;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public class OAuthSettingSession : SessionBase
    {
        private Server _server;
        private TwitterOAuth _twitterOAuth;
        private String authToken;
        private TwitterIdentity _identity;
        private Boolean _isFinished;

        public OAuthSettingSession(String id, Server server)
            : base(id, server)
        {
            _server = server;
            _twitterOAuth = new TwitterOAuth(_server.OAuthClientKey, _server.OAuthSecretKey);
        }

        protected override void OnAttached(ConnectionBase connection)
        {
            String authoizeUrl = _twitterOAuth.GetAuthorizeUrl(out authToken);
            SendMessage("次のURLをブラウザで表示してアプリケーションのアクセスを許可してください。また、許可のあと表示される暗証番号(PINコード)を入力してください。");
            SendMessage(authoizeUrl);
        }

        protected override void OnDetached(ConnectionBase connection)
        {
        }

        protected override void OnMessageReceivedFromClient(MessageReceivedEventArgs e)
        {
            if (_isFinished) return;

            PrivMsgMessage privMsg = e.Message as PrivMsgMessage;
            if (privMsg != null)
            {
                if (_identity == null)
                {
                    // step 1
                    try
                    {
                        _identity = _twitterOAuth.RequestAccessToken(authToken, privMsg.Content.Trim());
                    }
                    catch (WebException we)
                    {
                        SendMessage("アプリケーションのアクセスは許可されませんでした。再接続してやり直してください。");
                        return;
                    }
                    SendMessage(String.Format("ユーザー {0} (ID:{1})として認証されました。", _identity.ScreenName, _identity.UserId));
                    SendMessage("IRCクライアントに設定するためのパスワードを入力してください。");
                }
                else
                {
                    // step 2
                    String password = privMsg.Content.Trim();
                    try
                    {
#if HOSTING
                        var config = Config.LoadConfig(_identity.UserId.ToString());
#else
                        var config = Config.LoadConfig(_identity.ScreenName.ToString());
#endif
                        config.OAuthAccessToken = _identity.Token;
                        config.OAuthTokenSecret = _identity.TokenSecret;
                        config.OAuthUserPasswordHash = Utility.GetMesssageDigest(password);
#if HOSTING
                        Config.SaveConfig(_identity.UserId.ToString(), config);
                        SendMessage(
                            String.Format(
                                "OAuth用のパスワードを設定しました。IRCクライアントの接続設定のユーザID(ログイン名)に {0} を、パスワードに設定したパスワードを指定して再接続してください。",
                                _identity.UserId));
#else
                        Config.SaveConfig(_identity.ScreenName.ToString(), config);
                        SendMessage("OAuth用のパスワードを設定しました。IRCクライアントの接続設定のパスワードに設定したパスワードを指定して再接続してください。");
#endif
                    }
                    catch (IOException ie)
                    {
                        SendMessage("設定ファイルにアクセスする際にエラーが発生しました。(" + ie.Message + ")");
                        return;
                    }

                    Session session = _server.GetSession(_identity.UserId.ToString()) as Session;
                    if (session != null)
                    {
                        session.LoadConfig(); // すでにセッションがある場合には設定を再読込
                    }

                    _isFinished = true;
                }
            }
        }

        private void SendMessage(String s)
        {
            Send(new PrivMsgMessage(CurrentNick, s) { SenderNick = "$OAuth", SenderHost = "twitter@" + Server.ServerName });
        }
    }
}
