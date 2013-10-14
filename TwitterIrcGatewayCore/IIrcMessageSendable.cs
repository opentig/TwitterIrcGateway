using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public interface IIrcMessageSendable
    {
        /// <summary>
        /// IRCメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        void Send(IRCMessage msg);

        /// <summary>
        /// JOIN などクライアントに返すメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        void SendServer(IRCMessage msg);

        /// <summary>
        /// IRCサーバからのメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        void SendServerMessage(IRCMessage msg);

        /// <summary>
        /// Gatewayからのメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        void SendGatewayServerMessage(String message);

        /// <summary>
        /// サーバのエラーメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        void SendServerErrorMessage(String message);

        /// <summary>
        /// サーバからクライアントにエラーリプライを返します。
        /// </summary>
        /// <param name="errorNum">エラーリプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        void SendErrorReply(ErrorReply errorNum, params String[] commandParams);

        /// <summary>
        /// サーバからクライアントにニュメリックリプライを返します。
        /// </summary>
        /// <param name="numReply">リプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        void SendNumericReply(NumericReply numReply, params String[] commandParams);

    }
}
