using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// IRCメッセージ受信時イベントのデータを提供します。
    /// </summary>
    public class MessageReceivedEventArgs : CancelableEventArgs
    {
        /// <summary>
        /// 受信したIRCメッセージを取得します
        /// </summary>
        public IRCMessage Message { get; set; }
        /// <summary>
        /// クライアントへの接続を取得します
        /// </summary>
        public TcpClient Client { get; private set; }
        /// <summary>
        /// クライアントへの出力のためのStreamWriterを取得します
        /// </summary>
        public StreamWriter Writer { get; private set; }

        public MessageReceivedEventArgs(IRCMessage msg, StreamWriter sw, TcpClient tcpClient)
        {
            Writer = sw;
            Client = tcpClient;
            Message = msg;
        }
    }

    /// <summary>
    /// セッションが開始時イベントのデータを提供します。
    /// </summary>
    public class SessionStartedEventArgs : EventArgs
    {
        /// <summary>
        /// 接続してきたユーザの名前を取得します。
        /// </summary>
        public String UserName { get; set; }
        /// <summary>
        /// Twitterのユーザを取得します。
        /// </summary>
        public User User { get; set; }
        /// <summary>
        /// 接続してきたユーザのエンドポイントを取得します。
        /// </summary>
        public IPEndPoint EndPoint { get; set; }
        public SessionStartedEventArgs(String userName, User user, IPEndPoint endPoint)
        {
            UserName = userName;
            User = user;
            EndPoint = endPoint;
        }
    }
    
    /// <summary>
    /// 接続をセッションにアタッチしたイベントのデータを提供します
    /// </summary>
    public class ConnectionAttachEventArgs : EventArgs
    {
        /// <summary>
        /// 新たにアタッチされた接続
        /// </summary>
        public ConnectionBase Connection { get; set; }
    }

    /// <summary>
    /// キャンセル可能なイベントのデータを提供します。
    /// </summary>
    public abstract class CancelableEventArgs : EventArgs
    {
        /// <summary>
        /// 処理をキャンセルするかどうかを取得・設定します
        /// </summary>
        public Boolean Cancel { get; set; }
    }

    /// <summary>
    /// タイムラインステータス一覧を取得したイベントのデータを提供します。
    /// </summary>
    public class TimelineStatusesEventArgs : CancelableEventArgs
    {
        /// <summary>
        /// ステータス一覧を取得します。
        /// </summary>
        public Statuses Statuses { get; private set; }
        /// <summary>
        /// 初回アクセスかどうかを取得します。
        /// </summary>
        public Boolean IsFirstTime { get; set; }
        
        public TimelineStatusesEventArgs(Statuses statuses, Boolean isFirstTime)
        {
            Statuses = statuses;
            IsFirstTime = isFirstTime;
        }
    }
    
    /// <summary>
    /// タイムラインステータスを処理するイベントのデータを提供します。
    /// </summary>
    public class TimelineStatusEventArgs : CancelableEventArgs
    {
        /// <summary>
        /// 受け取ったステータスを取得します
        /// </summary>
        public Status Status { get; private set; }
        /// <summary>
        /// これからクライアントに送ろうとしている本文を取得・設定します
        /// </summary>
        public String Text { get; set; }
        /// <summary>
        /// クライアントに送信するIRCメッセージの種類を取得・設定します
        /// </summary>
        public String IRCMessageType { get; set; }
        
        public TimelineStatusEventArgs(Status status) : this(status, status.Text, "")
        {
        }
        public TimelineStatusEventArgs(Status status, String text, String ircMessageType)
        {
            Status = status;
            Text = text;
            IRCMessageType = ircMessageType;
        }
    }

    /// <summary>
    /// ステータスをクライアントから更新したイベントのデータを提供します。
    /// </summary>
    public class StatusUpdateEventArgs : CancelableEventArgs
    {
        /// <summary>
        /// クライアントから受け取ったIRCメッセージを取得します。タイミングや呼び出し元によってはnullになります。
        /// </summary>
        public PrivMsgMessage ReceivedMessage { get; set; }
        /// <summary>
        /// 更新するのに利用するテキストを取得・設定します
        /// </summary>
        public String Text { get; set; }
        /// <summary>
        /// 返信先のステータスのIDを指定します。0を指定すると返信先を指定しなかったことになります。
        /// </summary>
        public Int64 InReplyToStatusId { get; set; }
        /// <summary>
        /// ステータスを更新してその結果のステータスを取得します。更新完了時のイベントでのみ利用できます。
        /// </summary>
        public Status CreatedStatus { get; set; }

        public StatusUpdateEventArgs(String text, Int64 inReplyToStatusId)
        {
            Text = text;
            InReplyToStatusId = inReplyToStatusId;
        }
        
        public StatusUpdateEventArgs(PrivMsgMessage receivedMessage, String text)
        {
            ReceivedMessage = receivedMessage;
            Text = text;
        }
    }

    /// <summary>
    /// メッセージの送信先を決定したイベントのデータを提供します。
    /// </summary>
    public class TimelineStatusRoutedEventArgs : EventArgs
    {
        /// <summary>
        /// ステータスを取得します
        /// </summary>
        public Status Status { get; private set; }
        /// <summary>
        /// メッセージの本文を取得します
        /// </summary>
        public String Text { get; private set; }
        /// <summary>
        /// 決定された送信先のリストを取得します。このリストに追加または削除することで送信先を変更できます。
        /// </summary>
        public List<RoutedGroup> RoutedGroups { get; private set; }
        
        public TimelineStatusRoutedEventArgs(Status status, String text, List<RoutedGroup> routedGroups)
        {
            Status = status;
            Text = text;
            RoutedGroups = routedGroups;
        }
    }

    /// <summary>
    /// メッセージを各グループに送信するイベントのデータを提供します。
    /// </summary>
    public class TimelineStatusGroupEventArgs : TimelineStatusEventArgs
    {
        /// <summary>
        /// 送信対象となるグループを取得します
        /// </summary>
        public Group Group { get; private set; }

        public TimelineStatusGroupEventArgs(Status status, String text, String ircMessageType, Group group) : base(status, text, ircMessageType)
        {
            Group = group;
        }
    }
}
