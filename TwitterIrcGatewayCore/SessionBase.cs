using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public abstract class SessionBase : MarshalByRefObject, IIrcMessageSendable
    {
        private Server _server;
        private List<ConnectionBase> _connections = new List<ConnectionBase>();

        /// <summary>
        /// セッションが終了している途中かどうかを取得します。
        /// </summary>
        public Boolean IsClosing { get; private set; }

        /// <summary>
        /// セッションは接続がなくなっても引き続き保持するかどうかを取得・設定します。
        /// </summary>
        public Boolean IsKeepAlive { get; set; }
        /// <summary>
        /// セッションの固有のIDを取得します。接続してきたユーザを結びつけるために利用されます。
        /// </summary>
        public String Id { get; private set; }
        /// <summary>
        /// 現在のニックネームを取得・設定します。
        /// </summary>
        public String CurrentNick { get; set; }
        /// <summary>
        /// 現在セッションにある接続のコレクションを取得します。
        /// </summary>
        public IList<ConnectionBase> Connections { get { return _connections.AsReadOnly(); } }

        /// <summary>
        /// セッションに接続が開始された際に発生するイベントです。
        /// </summary>
        public event EventHandler<ConnectionAttachEventArgs> ConnectionAttached;
        /// <summary>
        /// セッションから接続が切断された際に発生するイベントです。
        /// </summary>
        public event EventHandler<ConnectionAttachEventArgs> ConnectionDetached;

        /// <summary>
        /// セッションが終了する際に発生するイベントです。
        /// </summary>
        public event EventHandler BeforeClosing;
        /// <summary>
        /// セッションが終了中に発生するイベントです。
        /// </summary>
        public event EventHandler Closing;
        /// <summary>
        /// セッションが終了する処理を終えた際に発生するイベントです。
        /// </summary>
        public event EventHandler AfterClosing;

        public SessionBase(String id, Server server)
        {
            Id = id;
            _server = server;
            TraceLogger.Server.Information("Session Started: "+Id);
        }

        /// <summary>
        /// 接続をセッションに結びつけます。
        /// </summary>
        /// <param name="connection"></param>
        public void Attach(ConnectionBase connection)
        {
            lock (_connections)
                lock (_server.Sessions)
                {
                    _connections.Add(connection);
                    connection.ConnectionEnded += ConnectionEnded;
                    connection.MessageReceived += MessageReceived;
                    SendGatewayServerMessage("Connection Attached: " + connection.ToString());

                    // ニックネームを合わせる
                    if (String.IsNullOrEmpty(CurrentNick))
                    {
                        CurrentNick = connection.UserInfo.Nick;
                    }
                    else
                    {
                        connection.SendServer(new NickMessage() {NewNick = CurrentNick});
                        connection.UserInfo.Nick = CurrentNick;
                    }

                    OnAttached(connection);
                    OnConnectionAttached(new ConnectionAttachEventArgs {Connection = connection});
                }
        }

        /// <summary>
        /// 接続をセッションから切り離します。キープアライブが有効な場合を除き接続数が0となるとセッションは終了します。
        /// </summary>
        /// <param name="connection"></param>
        public void Detach(ConnectionBase connection)
        {
            lock (_connections)
                lock (_server.Sessions)
                {
                    connection.ConnectionEnded -= ConnectionEnded;
                    connection.MessageReceived -= MessageReceived;
                    _connections.Remove(connection);
                    SendGatewayServerMessage("Connection Detached: " + connection.ToString());

                    OnDetached(connection);
                    OnConnectionDetached(new ConnectionAttachEventArgs {Connection = connection});

                    // 接続が0になったらセッション終了
                    if (_connections.Count == 0 && !IsKeepAlive)
                    {
                        Close();
                    }
                }
        }

        public override string ToString()
        {
            return String.Format("{0}: Id={1}, Connections=[{2}]", this.GetType().Name, Id, String.Join(", ", (from conn in Connections select conn.UserInfo.EndPoint.ToString()).ToArray()));
        }

        #region オーバーライドして使うメソッド
        /// <summary>
        /// 接続が結びつけられたときの処理です。
        /// </summary>
        /// <param name="connection"></param>
        protected abstract void OnAttached(ConnectionBase connection);
        /// <summary>
        /// 接続が切り離されたときの処理です。
        /// </summary>
        /// <param name="connection"></param>
        protected abstract void OnDetached(ConnectionBase connection);
        /// <summary>
        /// クライアントからIRCメッセージを受け取ったときの処理です。
        /// </summary>
        /// <param name="e"></param>
        protected abstract void OnMessageReceivedFromClient(MessageReceivedEventArgs e);
        #endregion

        #region イベントハンドラ
        private void MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            // クライアントからきた PRIVMSG/NOTICE は他のクライアントにも投げる
            if (e.Message is PrivMsgMessage || e.Message is NoticeMessage)
            {
                lock (_connections)
                {
                    foreach (ConnectionBase connection in _connections)
                    {
                        // 送信元には送らない
                        if (connection != sender)
                            connection.SendServer(e.Message);
                    }
                }
            }
            
            // セッションの方で処理する
            OnMessageReceivedFromClient(e);
        }
        private void ConnectionEnded(object sender, EventArgs e)
        {
            Detach((ConnectionBase)sender);
        }
        #endregion
        
        protected virtual void OnConnectionAttached(ConnectionAttachEventArgs e)
        {
            if (ConnectionAttached != null)
                ConnectionAttached(this, e);
        }
        protected virtual void OnConnectionDetached(ConnectionAttachEventArgs e)
        {
            if (ConnectionDetached != null)
                ConnectionDetached(this, e);
        }
        /// <summary>
        /// セッションが終了しようとしているときの処理です。
        /// </summary>
        protected virtual void OnBeforeClosing()
        {
            if (BeforeClosing != null)
                BeforeClosing(this, EventArgs.Empty);
        }
        /// <summary>
        /// セッションが終了中の処理です。
        /// </summary>
        protected virtual void OnClosing()
        {
            if (Closing != null)
                Closing(this, EventArgs.Empty);
        }
        /// <summary>
        /// セッションが終了処理が完了したあとの処理です。
        /// </summary>
        protected virtual void OnAfterClosing()
        {
            if (AfterClosing != null)
                AfterClosing(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// すべての接続を切断してセッションを終了します。
        /// </summary>
        public virtual void Close()
        {
            // Detachするので二重でここに来ないように。
            if (IsClosing)
                return;

            OnBeforeClosing();
            IsClosing = true;

            OnClosing();

            lock (_connections)
            {
                lock (_server.Sessions)
                {
                    TraceLogger.Server.Information("Session Closing: " + Id);
                    List<ConnectionBase> connections = new List<ConnectionBase>(_connections);
                    foreach (ConnectionBase connection in connections)
                    {
                        Detach(connection);
                        connection.Close();
                    }
                    _server.Sessions.Remove(Id);
                }
            }

            OnAfterClosing();
        }

        #region IRC メッセージ処理
        /// <summary>
        /// IRCメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void Send(IRCMessage msg)
        {
            if (IsClosing)
                return;
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.Send(msg);
        }

        /// <summary>
        /// JOIN などクライアントに返すメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServer(IRCMessage msg)
        {
            if (IsClosing)
                return;
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendServer(msg);
        }

        /// <summary>
        /// IRCサーバからのメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServerMessage(IRCMessage msg)
        {
            if (IsClosing)
                return;
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendServerMessage(msg);
        }

        /// <summary>
        /// Gatewayからのメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendGatewayServerMessage(String message)
        {
            if (IsClosing)
                return;
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendGatewayServerMessage(message);
        }

        /// <summary>
        /// サーバのエラーメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendServerErrorMessage(String message)
        {
//            if (!_config.IgnoreWatchError)
            {
                SendGatewayServerMessage("エラー: " + message);
            }
        }

        /// <summary>
        /// サーバからクライアントにエラーリプライを返します。
        /// </summary>
        /// <param name="errorNum">エラーリプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        public void SendErrorReply(ErrorReply errorNum, params String[] commandParams)
        {
            SendNumericReply((NumericReply)errorNum, commandParams);
        }

        /// <summary>
        /// サーバからクライアントにニュメリックリプライを返します。
        /// </summary>
        /// <param name="numReply">リプライ番号</param>
        /// <param name="commandParams">リプライコマンドパラメータ</param>
        public void SendNumericReply(NumericReply numReply, params String[] commandParams)
        {
            if (IsClosing)
                return;
            lock (_connections)
                foreach (ConnectionBase connection in _connections)
                    connection.SendNumericReply(numReply, commandParams);
        }
        #endregion
    }
}
