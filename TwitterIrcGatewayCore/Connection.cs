using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Misuzilla.Applications.TwitterIrcGateway.Authentication;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// TwitterIrcGatewayへのクライアントの接続を表すクラスです。
    /// </summary>
    public class Connection : ConnectionBase
    {
        /// <summary>
        /// Twitter上のユーザを取得します。
        /// </summary>
        public User TwitterUser { get; private set; }
        public TwitterIdentity Identity { get; private set; }

        public Boolean IsOAuthSettingMode { get; set; }

        public Connection(Server server, TcpClient tcpClient) : base(server, tcpClient)
        {
        }

        protected override AuthenticateResult OnAuthenticate(UserInfo userInfo)
        {
            try
            {
                AuthenticateResult authResult = CurrentServer.Authentication.Authenticate(CurrentServer, this, userInfo);
                TwitterAuthenticateResult twitterAuthResult = authResult as TwitterAuthenticateResult;
                if (twitterAuthResult != null && authResult.IsAuthenticated)
                {
                    TwitterUser = twitterAuthResult.User;
                    Identity = twitterAuthResult.Identity;
                }

                if (authResult is OAuthContinueAuthenticationResult)
                    IsOAuthSettingMode = true;

                return authResult;
            }
            catch (Exception ex)
            {
                SendServerErrorMessage(ex.Message);
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }
        }

        protected override void OnAuthenticateSucceeded()
        {
            SessionBase session;
            if (IsOAuthSettingMode)
            {
                // OAuth Setting Mode
                session = CurrentServer.GetOrCreateSession(Guid.NewGuid().ToString(), (server, sessionId) => new OAuthSettingSession(sessionId, server));
            }
            else
            {
                // Authenticated
                session = CurrentServer.GetOrCreateSession(TwitterUser);
            }
            session.Attach(this);
        }

        protected override void OnAuthenticateFailed(AuthenticateResult authenticateResult)
        {
        }
    }
}
