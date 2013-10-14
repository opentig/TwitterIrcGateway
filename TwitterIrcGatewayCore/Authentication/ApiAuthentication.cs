using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.Authentication
{
    public class ApiAuthentication : IAuthentication
    {
        #region IAuthentication メンバ
        public AuthenticateResult Authenticate(Server server, Connection connection, UserInfo userInfo)
        {
            // ニックネームとパスワードのチェック
            if (String.IsNullOrEmpty(userInfo.Nick))
            {
                return new AuthenticateResult(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
            }
            if (String.IsNullOrEmpty(userInfo.Password))
            {
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }

            // ログインチェック
            // この段階でTwitterServiceは作っておく
            connection.SendGatewayServerMessage("* アカウント認証を確認しています...");
            TwitterService twitter = new TwitterService(userInfo.UserName, userInfo.Password);
            User twitterUser = null;
            try
            {
                twitterUser = twitter.VerifyCredential();
            }
            catch (WebException we)
            {
                // Twitter の接続に失敗
                connection.SendGatewayServerMessage("* アカウント認証に失敗しました。ユーザ名またはパスワードを確認してください。");
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }
            catch (Exception ex)
            {
                // Twitter の接続に失敗
                connection.SendGatewayServerMessage("* アカウント認証に失敗しました。ユーザ名またはパスワードを確認してください。内部的なエラーが発生しました。");
                Trace.TraceError(ex.ToString());
                return new AuthenticateResult(ErrorReply.ERR_PASSWDMISMATCH, "Password Incorrect");
            }
            connection.SendGatewayServerMessage(String.Format("* アカウント: {0} (ID:{1})", twitterUser.ScreenName, twitterUser.Id));

            return new TwitterAuthenticateResult(twitterUser); // 成功
        }

        #endregion
    }
}
