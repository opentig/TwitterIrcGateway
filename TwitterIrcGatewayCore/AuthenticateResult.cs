using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// 認証結果を保持します。
    /// </summary>
    public class AuthenticateResult : MarshalByRefObject
    {
        /// <summary>
        /// ユーザのアクセスが許可されているかどうかを取得・設定します。
        /// </summary>
        public Boolean IsAuthenticated { get; set; }

        /// <summary>
        /// 認証が失敗した理由のリプライを返します。
        /// </summary>
        public ErrorReply ErrorReply { get; set; }

        /// <summary>
        /// 認証が失敗した理由を返します。
        /// </summary>
        public String ErrorMessage { get; set; }

        /// <summary>
        /// 認証が成功した状態で初期化します。
        /// </summary>
        public AuthenticateResult()
        {
            IsAuthenticated = true;
        }

        /// <summary>
        /// 認証に失敗しその理由を指定して初期化します。
        /// </summary>
        /// <param name="errorReply"></param>
        /// <param name="message"></param>
        public AuthenticateResult(ErrorReply errorReply, String message)
        {
            IsAuthenticated = false;
            ErrorReply = errorReply;
            ErrorMessage = message;
        }
    }

    /// <summary>
    /// Twitterを利用した認証結果を保持します。
    /// </summary>
    public class TwitterAuthenticateResult : AuthenticateResult
    {
        /// <summary>
        /// Twitterのユーザを取得・設定します。
        /// </summary>
        public User User { get; set; }

        /// <summary>
        /// Twitterのログイン情報を取得・設定します。OAuthを利用しているときのみ利用できます。
        /// </summary>
        public TwitterIdentity Identity { get; set; }

        public TwitterAuthenticateResult(User user) : this(user, null)
        {
        }

        public TwitterAuthenticateResult(User user, TwitterIdentity identity) : base()
        {
            User = user;
            Identity = identity;
        }
    }
}
