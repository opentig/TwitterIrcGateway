using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Misuzilla.Net.Irc;
using System.Security;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// ユーザからの接続を管理するクラス
    /// </summary>
    public abstract class ConnectionBase : MarshalByRefObject, IDisposable, IIrcMessageSendable
    {
        private StreamWriter _writer;

        #region Events
        /// <summary>
        /// IRCメッセージ受信時、TwitterIrcGatewayが処理する前のイベント
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> PreMessageReceived;
        /// <summary>
        /// IRCメッセージ受信時のイベント
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        /// <summary>
        /// IRCメッセージ受信時、TwitterIrcGatewayが処理した後のイベント
        /// </summary>
        public event EventHandler<MessageReceivedEventArgs> PostMessageReceived;

        /// <summary>
        /// セッション開始時のイベント
        /// </summary>
        public event EventHandler<SessionStartedEventArgs> ConnectionStarted;
        /// <summary>
        /// セッション終了時のイベント
        /// </summary>
        public event EventHandler<EventArgs> ConnectionEnded;

        #endregion

        public ConnectionBase(Server server, TcpClient tcpClient)
        {
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_USER);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_NICK);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PASS);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_QUIT);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PING);

            CurrentServer = server;
            this.Encoding = CurrentServer.Encoding;
            this.TcpClient = tcpClient;
            this.UserInfo = new UserInfo() {EndPoint = (IPEndPoint) (tcpClient.Client.RemoteEndPoint)};
        }

        ~ConnectionBase()
        {
            Dispose();
        }

        /// <summary>
        /// 接続しているクライアントを取得します
        /// </summary>
        public TcpClient TcpClient
        {
            get; private set;
        }

        /// <summary>
        /// 関連づけられているサーバを取得します
        /// </summary>
        public Server CurrentServer
        {
            get; private set;
        }
        
        /// <summary>
        /// クライアント情報を取得します
        /// </summary>
        public UserInfo UserInfo
        {
            get; private set;
        }
        
        /// <summary>
        /// クライアントが認証済みかどうかを取得します
        /// </summary>
        public Boolean IsAuthenticated
        {
            get; private set;
        }

        /// <summary>
        /// 
        /// </summary>
        public Encoding Encoding
        {
            get; set;
        }
        
        /// <summary>
        /// セッションを開始します。
        /// </summary>
        public void Start()
        {
            CheckDisposed();
            try
            {
                using (Stream stream = TcpClient.GetStream())
                {
                    Stream targetStream = stream;
                    if (CurrentServer.IsSslConnection)
                    {
                        SslStream sslStream = new SslStream(stream);
                        sslStream.AuthenticateAsServer(CurrentServer.Certificate, false, SslProtocols.Default, false);
                        targetStream = sslStream;
                    }

                    using (StreamReader sr = new StreamReader(targetStream, Encoding))
                    using (StreamWriter sw = new StreamWriter(targetStream, Encoding))
                    {
                        _writer = sw;

                        String line;
                        PermissionSet permissionSet = null;
                        while (TcpClient.Connected && (line = sr.ReadLine()) != null)
                        {
                            try
                            {
                                IRCMessage msg = IRCMessage.CreateMessage(line);
                                OnMessageReceived(msg);
                            }
                            catch (IRCException ircE)
                            {
                                Trace.TraceWarning(ircE.ToString());
                            }

                        }
                    }
                }
            }
            //catch (IOException)
            //{
            //}
            //catch (NullReferenceException)
            //{
            //}
            //catch (AuthenticationException) // SSL 
            //{
            //}
            catch (Exception ex)
            {
                TraceLogger.Server.Error("Error: {0}", ex.ToString());
            }
            finally
            {
                Close();
            }
        }
        
        #region イベント実行メソッド
        protected virtual void OnMessageReceived(IRCMessage msg)
        {
            Debug.WriteLine(msg.ToString());
            if (FireEvent(PreMessageReceived, new MessageReceivedEventArgs(msg, _writer, TcpClient)))
            {
                if (FireEvent(MessageReceived, new MessageReceivedEventArgs(msg, _writer, TcpClient)))
                {
                    FireEvent(PostMessageReceived, new MessageReceivedEventArgs(msg, _writer, TcpClient));
                }
            }
        }
        protected virtual void OnConnectionStarted(String username)
        {
            FireEvent(ConnectionStarted, new SessionStartedEventArgs(username, null, (IPEndPoint)TcpClient.Client.RemoteEndPoint));
        }
        protected virtual void OnConnectionEnded()
        {
            FireEvent(ConnectionEnded, EventArgs.Empty);
        }
        #endregion

        #region 認証
        private void DoAuthenticate()
        {
            if (String.IsNullOrEmpty(UserInfo.Nick) || String.IsNullOrEmpty(UserInfo.UserName))
                return;
            
            AuthenticateResult result = OnAuthenticate(UserInfo);
            if (result.IsAuthenticated)
            {
                Type t = typeof (Server);
                SendNumericReply(NumericReply.RPL_WELCOME
                                 , String.Format("Welcome to the Internet Relay Network {0}", UserInfo.ClientHost));
                SendNumericReply(NumericReply.RPL_YOURHOST,
                                 String.Format("Your host is {0}, running version {1}", t.FullName,
                                               t.Assembly.GetName().Version));
                SendNumericReply(NumericReply.RPL_CREATED
                                 , String.Format("This server was created {0}", CurrentServer.StartTime));
                SendNumericReply(NumericReply.RPL_MYINFO,
                                 String.Format("{0} {1}-{2} {3} {4}", Environment.MachineName, t.FullName,
                                               t.Assembly.GetName().Version, "", ""));

                IsAuthenticated = true;

                // イベントハンドラを外す
                MessageReceived -= MessageReceived_USER;
                MessageReceived -= MessageReceived_PASS;
                MessageReceived -= MessageReceived_NICK;

                OnAuthenticateSucceeded();
            }
            else
            {
                SendErrorReply(result.ErrorReply, result.ErrorMessage);
                OnAuthenticateFailed(result);
                Thread.Sleep(10 * 1000);
                Close();
            }
        }

        protected abstract AuthenticateResult OnAuthenticate(UserInfo userInfo);
        protected abstract void OnAuthenticateSucceeded();
        protected abstract void OnAuthenticateFailed(AuthenticateResult authenticateResult);
        #endregion

        #region メッセージ処理イベント
        private void MessageReceived_USER(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is UserMessage)) return;

            UserInfo.UserName = e.Message.CommandParams[0];
            UserInfo.RealName = e.Message.CommandParams[3];
            DoAuthenticate();
        }

        private void MessageReceived_NICK(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is NickMessage)) return;

            UserInfo.Nick = ((NickMessage)(e.Message)).NewNick;
            DoAuthenticate();
        }

        private void MessageReceived_PASS(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PASS", true) != 0) return;

            if (e.Message.CommandParam.Length != 0)
            {
                UserInfo.Password = e.Message.CommandParam.Substring(1);
            }
        }

        private void MessageReceived_QUIT(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is QuitMessage)) return;

            try
            {
                e.Client.Close();
            }
            catch { }
        }

        private void MessageReceived_PING(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PING", true) != 0) return;
            Send(IRCMessage.CreateMessage("PONG :" + e.Message.CommandParam));
        }
        #endregion

        /// <summary>
        /// IRCメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void Send(IRCMessage msg)
        {
            //CheckDisposed();
            lock (_writer)
            {
                try
                {
                    if (TcpClient != null && TcpClient.Connected && _writer.BaseStream.CanWrite)
                    {
                        _writer.WriteLine(msg.RawMessage);
                        _writer.Flush();
                    }
                }
                catch (IOException)
                {
                    Close();
                }
            }
        }

        /// <summary>
        /// JOIN などクライアントに返すメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServer(IRCMessage msg)
        {
            msg.Sender = UserInfo.ClientHost;
            Send(msg);
        }

        /// <summary>
        /// IRCサーバからのメッセージを送信します
        /// </summary>
        /// <param name="msg"></param>
        public void SendServerMessage(IRCMessage msg)
        {
            msg.Prefix = Server.ServerName;
            Send(msg);
        }

        /// <summary>
        /// Gatewayからのメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendGatewayServerMessage(String message)
        {
            NoticeMessage noticeMsg = new NoticeMessage();
            noticeMsg.Sender = "";
            noticeMsg.Receiver = UserInfo.Nick;
            noticeMsg.Content = message.Replace("\n", " ");
            Send(noticeMsg);
        }

        /// <summary>
        /// サーバのエラーメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendServerErrorMessage(String message)
        {
            SendGatewayServerMessage("エラー: " + message);
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
            if (commandParams.Length > 14 || commandParams.Length < 0)
                throw new ArgumentOutOfRangeException("commandParams");

            NumericReplyMessage numMsg = new NumericReplyMessage(numReply);
            numMsg.CommandParams[0] = UserInfo.Nick;
            for (Int32 i = 0; i < commandParams.Length; i++)
                numMsg.CommandParams[i+1] = commandParams[i];

            SendServerMessage(numMsg);
        }
       
        /// <summary>
        /// 
        /// </summary>
        public void Close()
        {
            if (TcpClient != null && TcpClient.Connected)
            {
                TcpClient.Close();
            }
            OnConnectionEnded();

            Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(this.GetType().Name);
        }

        public override string ToString()
        {
            return String.Format("Session: User={0}, Client={1}", UserInfo.UserName, UserInfo.EndPoint);
        }

        #region ヘルパーメソッド
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEventArgs"></typeparam>
        /// <param name="handlers"></param>
        /// <param name="e"></param>
        /// <returns>キャンセルされた場合にはfalseが返ります。</returns>
        [DebuggerStepThrough]
        private Boolean FireEvent<TEventArgs>(EventHandler<TEventArgs> handlers, TEventArgs e) where TEventArgs:EventArgs
        {
            if (handlers != null)
            {
                foreach (EventHandler<TEventArgs> eventHandler in handlers.GetInvocationList())
                {
                    try
                    {
                        eventHandler(this, e);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }
                }
            }
            if (e is CancelableEventArgs)
            {
                return !((e as CancelableEventArgs).Cancel);
            }
            
            return true;
        }
        #endregion

        #region IDisposable メンバ
        private Boolean _isDisposed = false;

        public void Dispose()
        {
            if (!_isDisposed)
            {
                if (TcpClient != null && TcpClient.Connected)
                {
                    TcpClient.Close();
                    TcpClient = null;
                }
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        #endregion
    }
}
