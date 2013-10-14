using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    class IrcTraceListener : TraceListener
    {
        private Session _session;
        public IrcTraceListener(Session session)
        {
            _session = session;
#if FALSE
            if (_session.TcpClient.Connected)
            {
                PrivMsgMessage msg = new PrivMsgMessage("$ServerTraceLog", String.Format("(0x{0}) {1}", Thread.CurrentThread.ManagedThreadId.ToString("x"), "IrcTraceListener enabled."));
                msg.SenderNick = "trace";
                msg.SenderHost = "trace@" + Server.ServerName;
                msg.Receiver = _session.Nick;
                _session.Send(msg);
            }
#endif
        }
        public override void Write(string message)
        {
            this.WriteLine(message);
        }

        public override void WriteLine(string message)
        {
#if FALSE
            if (_session.TcpClient.Connected)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("(0x{0}) ", Thread.CurrentThread.ManagedThreadId.ToString("x"));
                sb.Append(' ', this.IndentLevel * this.IndentSize);

                foreach (String line in message.Split('\n'))
                {

                    NoticeMessage msg = new NoticeMessage("$ServerTraceLog", sb.ToString() + line);
                    msg.Sender = "trace!trace@internal";
                    msg.Receiver = _session.Nick;
                    _session.Send(msg);
                }
            }
#endif
        }
    }
}
