using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Misuzilla.Net.Irc
{
    public class IRCConnection : IDisposable
    {
        private Boolean _retryOnDisconnected = false;
        private TcpClient _tcpClient;
        private StreamReader _streamReader;
        private StreamWriter _streamWriter;

        private Boolean _connected = false;
        private Thread _runner;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public void OnMessageReceived(IRCMessage m)
        {
            if (MessageReceived != null)
            {
                MessageReceived(this, new MessageReceivedEventArgs(m));
            }
        }
        public event EventHandler Connected;
        protected void OnConnected()
        {
            if (Connected != null)
            {
                Connected(this, new EventArgs());
            }
        }
        public event EventHandler Connecting;
        protected void OnConnecting()
        {
            if (Connecting != null)
            {
                Connecting(this, new EventArgs());
            }
        }
        public event EventHandler Disconnected;
        protected void OnDisconnected()
        {
            if (Disconnected != null)
            {
                Disconnected(this, new EventArgs());
            }
        }

        public Boolean IsConnected
        {
            get { return _connected; }
        }
        public Boolean RetryOnDisconnected
        {
            get { return _retryOnDisconnected; }
        }


        public void Connect(String host, Int32 port, String userName, String password, String nickName, String userInfo)
        {
            if (_connected)
            {
                throw new ApplicationException("すでに接続が確立されています");
            }
           
            OnConnecting();
            _tcpClient = new TcpClient();
            try
            {
                _tcpClient.Connect(host, port);
                _connected = true;
                Encoding enc = Encoding.GetEncoding("ISO-2022-JP");
                _streamReader = new StreamReader(_tcpClient.GetStream(), enc);
                _streamWriter = new StreamWriter(_tcpClient.GetStream(), enc);
                Send(String.Format("USER {0} * * :{1}", userName, (userInfo.Length > 0 ? userInfo : "-")));
                if (password.Length > 0)
                {
                    Send(String.Format("PASS {0}", password));
                }
                Send(String.Format("NICK {0}", nickName));
            }
            catch (SocketException socketEx)
            {
                _connected = false;
                OnDisconnected();
                return;
            }
            catch (IOException)
            {
                _connected = false;
                OnDisconnected();
                return;
            }
            OnConnected();

            _runner = new Thread(new ThreadStart(Runner));
            _runner.Start();
        }

        public void Send(IRCMessage message)
        {
            Send(message.RawMessage);
        }

        public void Send(String rawMessage)
        {
            if (!_connected)
            {
                throw new ApplicationException("接続が確立されていません");
            }
            try
            {
                _streamWriter.WriteLine(rawMessage);
                _streamWriter.Flush();
            }
            catch (IOException)
            {
                Close();
            }
        }

        public void Disconnect(String quitMessage)
        {
            if (!_connected) return;
            // TODO: リトライしないように→Sendをつかうように
            _connected = false;
            _streamWriter.WriteLine("QUIT :"+quitMessage);

            try
            {
                if (_streamWriter.BaseStream.CanWrite)
                {
                    _streamWriter.Flush();
                }
            }
            catch (IOException) { }

            Close();
        }

        private delegate void MessageReceivedDelegate(String line);
        private void Runner()
        {
            String line;
            try
            {
                while ((line = _streamReader.ReadLine()) != null)
                {
                    try
                    {
                        OnMessageReceived(IRCMessage.CreateMessage(line));
                    }
                    catch (IRCInvalidMessageException e)
                    {
                        foreach (String l in e.ToString().Split(new Char[] { '\n' })) {
                            NoticeMessage n = new NoticeMessage();
                            n.Content = l;
                            n.Sender = "_Internal";
                            n.IsServerMessage = true;
                            OnMessageReceived(n);
                        }
                    }
                }
            }
            catch (IOException) { }
        }

        public void Close()
        {
            if (_tcpClient != null)
            {
                _runner.Abort();
                _streamReader.Close();
                _streamWriter.Close();
                _tcpClient.Close();
                _tcpClient = null;
                _connected = false;
           }
           OnDisconnected();
        }


        #region IDisposable メンバ

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        #endregion

        public class MessageReceivedEventArgs : EventArgs
        {
            private IRCMessage _m;
            public MessageReceivedEventArgs(IRCMessage m)
            {
                _m = m;
            }

            public IRCMessage Message
            {
                get { return _m; }
                set { _m = value; }
            }
        }
    }
}
