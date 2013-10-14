using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// IRCの接続ユーザ情報を表すクラスです。
    /// </summary>
    public class UserInfo : MarshalByRefObject
    {
        /// <summary>
        /// ニックネームを取得・設定します。
        /// </summary>
        public String Nick { get; set; }
        /// <summary>
        /// ユーザ名を取得・設定します。
        /// </summary>
        public String UserName { get; set; }
        /// <summary>
        /// ユーザの本名を取得・設定します。
        /// </summary>
        public String RealName { get; set; }
        /// <summary>
        /// パスワードを取得・設定します。
        /// </summary>
        public String Password { get; set; }
        /// <summary>
        /// クライアントのアドレスを取得・設定します。
        /// </summary>
        public IPEndPoint EndPoint { get; set; }
        /// <summary>
        /// クライアントホスト文字列を取得します。
        /// </summary>
        public String ClientHost
        {
            get
            {
                return String.Format("{0}!{1}@{2}", Nick, UserName, EndPoint.Address);
            }
        }

        public UserInfo()
        {
        }

        public UserInfo(String nick, String userName, IPEndPoint endPoint, String realName, String password)
        {
            Nick = nick;
            UserName = userName;
            EndPoint = endPoint;
            RealName = realName;
            Password = password;
        }

        public override string ToString()
        {
            return String.Format("UserInfo: Nick={0}; UserName={1}; HostName={2}; RealName={3}", Nick, UserName, ClientHost, RealName);
        }
    }
}
