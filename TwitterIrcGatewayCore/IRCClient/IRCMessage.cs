// $Id: IRCMessage.cs 561 2009-04-25 09:10:09Z tomoyo $
using System;
using System.Collections;
using System.Text;
using System.Diagnostics;

namespace Misuzilla.Net.Irc
{
	public abstract class 
	IRCMessage
	{
		private Boolean _isServerMessage = false;
		private String _rawMessage = "";
		private String _senderHost = "";
		private String _senderNick = "";
		private String _sender = "";
		private String _prefix = "";
		private String _command = "";
		private String _commandParam = "";
		private String[] _commandParams;
		
		// FIXME: プロパティを作ってサブクラスでオーバーライドする形にしたほうがいいのではないか?
		protected void
		SetCommand(String command)
		{
			_command = command;
		}
		private void
		SetPrefix()
		{
			if (_senderNick == "")
				_prefix = "";
			else if (_senderHost == "")
				_prefix = _senderNick;
			else
				_prefix = _senderNick + "!" + _senderHost;
			
		}
		public Boolean IsServerMessage
		{
			get { return _isServerMessage; }
			set { _isServerMessage = value; }
		}
		public String Sender
		{
			get { return _sender; }
			set
			{
				String[] part = Split(value, new char[] {'!'}, 2);
				if (part.Length == 2) {
					// nick!~user@host
					_senderNick = part[0];
					_senderHost = part[1] ?? "";
				} else {
					// nick
					_senderHost = "";
					_senderNick = value;
				}
				_sender = value;
				this.SetPrefix();
			}
		}
		public String SenderHost
		{
			get { return _senderHost; }
			set { _senderHost = value; this.SetPrefix(); }
		}
		public String SenderNick
		{
			get { return _senderNick; }
			set { _senderNick = value; this.SetPrefix(); }
		}
		
		public String Prefix
		{
			get { return _prefix; }
			set { this.Sender = value; }
		}
		public String Command
		{
			get { return _command; }
		}
		public String CommandParam
		{
			get {
				StringBuilder sb = new StringBuilder();
				for (Int32 i = 0; i < _commandParams.Length; i++) {
					if (i+1 == _commandParams.Length || _commandParams[i+1] == null || _commandParams[i+1] == "") {
						sb.Append(":");
						sb.Append(_commandParams[i]);
						sb.Append(" ");
						break;
					} else {
						sb.Append(_commandParams[i]);
						sb.Append(" ");
					}
				}
				sb.Remove(sb.Length-1, 1);
				_commandParam = sb.ToString();

				//return _commandParam;
				return sb.ToString();
			}
			set {
				String[] param = value.Split(new Char[]{' '});
				_commandParam = value;
				_commandParams = new String[15];
				for (Int32 i = 0; i < 15; i++) {
					if (i < param.Length && i < _commandParams.Length) {
						if (param[i].StartsWith(":")) {
							_commandParams[i] = String.Join(" ", param, i, param.Length - i).Substring(1);
							break;
						} else {
							_commandParams[i] = param[i];
						}
					} else {
						_commandParams[i] = "";
					}
				}
			}
		}
		public String[] CommandParams
		{
			get { return _commandParams; }
			set { _commandParams = value; }
		}
		
		public String RawMessage
		{
			get {
				StringBuilder sb = new StringBuilder();
				if (_prefix.Length != 0) {
					sb.Append(":");
					sb.Append(_prefix);
					sb.Append(" ");
				}
				sb.Append(_command);
				sb.Append(" ");
				sb.Append(this.CommandParam);
				
				//return _rawMessage;
				return sb.ToString();
			}
		}

		public new virtual String
		ToString()
		{
			return this.RawMessage;
		}

		public
		IRCMessage(String raw)
		{
			String[] parts;
			_rawMessage = raw;
			
			try {
				if (raw.StartsWith(":")) {
					parts = Split(raw, new Char[]{' '}, 3);
					_prefix = parts[0].Substring(1);
					_command = parts[1];
					_commandParam = parts[2];
				} else {
					parts = Split(raw, new Char[]{' '}, 2);
					_command = parts[0];
					_commandParam = parts[1];
					_prefix = "";
				}
			} catch (IndexOutOfRangeException) {
				throw new IRCInvalidMessageException(raw);
			}

            if (_commandParam == null)
            {
                _commandParam = "";
                //throw new IRCInvalidMessageException(raw);
            }

			this.CommandParam = _commandParam;
			this.Sender = _prefix;
			if (_prefix.Length == 0)
				this.IsServerMessage = false;
		}
		public
		IRCMessage()
		{
			_commandParams = new String[15];
		}
		
		public static IRCMessage
		CreateMessage(String raw)
		{
			//Trace.WriteLine(raw);
			OtherMessage om = new OtherMessage(raw);
			
			switch (om.Command.ToUpper()) {
			case "NOTICE":
				return new NoticeMessage(raw);
			case "PRIVMSG":
				return new PrivMsgMessage(raw);
			case "NICK":
				return new NickMessage(raw);
			case "JOIN":
				return new JoinMessage(raw);
			case "PART":
				return new PartMessage(raw);
			case "QUIT":
				return new QuitMessage(raw);
			case "TOPIC":
				return new TopicMessage(raw);
			case "USER":
				return new UserMessage(raw);
			case "MODE":
				return new ModeMessage(raw);
			default: //case "372": case "001": case "002":
				try {
					if (Int32.Parse(om.Command) > 0)
						return new NumericReplyMessage(raw);
				} catch (FormatException) {
				}
				return om;
			}
		}

		// TODO: .NET CF
		private String[] Split(String str, Char[] delim, Int32 count)
		{
			String[] parts = str.Split(delim);
			String[] partsNew = new String[count];
			if (count > parts.Length) 
			{
				count = parts.Length;
			}

			Int32 maxIndex = count-1;
			StringBuilder sb = new StringBuilder();
			for (Int32 i = 0; i < parts.Length; i++) 
			{
				if (i < maxIndex) 
				{
					partsNew[i] = parts[i];
				}
				else 
				{
					// TODO: delim[0] => new Char{ ... }
					sb.Append(parts[i]).Append(delim[0]);
				}
			}
			sb.Length = sb.Length - 1;
			partsNew[maxIndex] = sb.ToString();

			return partsNew;
		}
	}

	public class
	OtherMessage : IRCMessage
	{
		public
		OtherMessage(String raw) : base(raw)
		{
		}
		public override String
		ToString()
		{
			return this.RawMessage;
		}
	}

	public class
	NumericReplyMessage : IRCMessage
	{
		private Int32 _replyNumber = 0;
		private String _content;
		
		public
		NumericReplyMessage(NumericReply i)
		{
			this.SetCommand(((Int32)i).ToString("000"));
            _replyNumber = ((Int32)i);
		}
		public
		NumericReplyMessage(String raw) : base(raw)
		{
			_replyNumber = Int32.Parse(this.Command);
			//this.Content = this.CommandParams[1];
		}
		
		public Int32
		ReplyNumber
		{
			get { return _replyNumber; }
			set { _replyNumber = value; this.SetCommand(value.ToString("000")); }
		}
		
		[Obsolete]
		public String
		ReplyNumberName
		{
			get
			{
				//if (_replyNumber < 400) 
					//return Enum.GetName(typeof(NumericReply), _replyNumber);
				//else
					//return Enum.GetName(typeof(ErrorReply), _replyNumber);
				return String.Empty;
			}
		}
		
		public override String
		ToString()
		{
			try {
				return _replyNumber.ToString("000") + ": " + this.CommandParam;
			} catch (ArgumentException) {
				//Trace.WriteLine(e);
				return _replyNumber.ToString("000") + ": " + this.CommandParam;
			}
			//return _rawMessage;
		}
	}
	
	public class
	PrivMsgMessage : IRCMessage
	{
		private String _receiver;
		private String _content;
		
		public String Receiver
		{
			get { return _receiver; }
			set { this.CommandParams[0] = _receiver = value; }
		}
		public String Content
		{
			set { this.CommandParams[1] = _content = value; }
			get { return _content; }
		}

		public
		PrivMsgMessage() : base()
		{
			this.SetCommand("PRIVMSG");
		}
		public
		PrivMsgMessage(String raw) : base(raw)
		{
			this.Receiver = this.CommandParams[0];
			this.Content = this.CommandParams[1];
		}
		public
		PrivMsgMessage(String receiver, String message) : base()
		{
			this.SetCommand("PRIVMSG");
			this.Receiver = receiver;
			this.Content = message;
		}

		public override String
		ToString()
		{
			return String.Format("PrivMsg: ({0}->{1}) {2}", this.SenderNick, _receiver, this.Content);
		}
	}
	public class
	NickMessage : IRCMessage
	{
		private String _newNick;
		public String NewNick
		{
			get { return _newNick; }
			set { this.CommandParams[0] = _newNick = value; }
		}
		
		public
		NickMessage() : base()
		{
			this.SetCommand("NICK");
		}
		public
		NickMessage(String raw) : base(raw)
		{
			_newNick = this.CommandParams[0];
		}

		public override String
		ToString()
		{
			if (this.SenderNick.Length == 0)
				return String.Format("Nick: {0}", _newNick);
			else
				return String.Format("Nick: {0} -> {1}", this.SenderNick, _newNick);
		}
	}
	public class
	NoticeMessage : IRCMessage
	{
		private String _receiver;
		private String _content;
		public String Receiver
		{
			get { return _receiver; }
			set { this.CommandParams[0] = _receiver = value; }
		}
		public String Content
		{
			set { this.CommandParams[1] = _content = value; }
			get { return _content; }
		}
		
		public
		NoticeMessage() : base()
		{
			this.SetCommand("NOTICE");
		}
		public
		NoticeMessage(String raw) : base(raw)
		{
			this.Receiver = this.CommandParams[0];
			this.Content = this.CommandParams[1];
		}
		public
		NoticeMessage(String receiver, String message) : base()
		{
			this.SetCommand("NOTICE");
			this.Receiver = receiver;
			this.Content = message;
		}

		public override String
		ToString()
		{
			if (this.Sender.Length != 0)
				return String.Format("Notice: ({0}) [{1}] {2}", this.SenderNick, this.Receiver, this.Content);
			else 
				return String.Format("Notice: [{0}] {1}",  _receiver, this.Content);
		}
	}
	public class
	JoinMessage : IRCMessage
	{
		private String _channel;
		public String Channel
		{
			get { return _channel; }
			set { this.CommandParams[0] = _channel = value; }
		}
		
		public
		JoinMessage() : base()
		{
			this.SetCommand("JOIN");
		}
		public
		JoinMessage(String raw) : base(raw)
		{
			_channel = this.CommandParams[0];
		}
		public
		JoinMessage(String channel, String param)
		{
			this.SetCommand("JOIN");
            Channel = channel;
		}

		public override String
		ToString()
		{
			if (this.SenderNick.Length == 0)
				return String.Format("Join: {0}", _channel);
			else
				return String.Format("Join: {0} - {1}", this.SenderNick, _channel);
		}
	}
	public class
	PartMessage : IRCMessage
	{
		private String _channel;
		private String _message;
		
		public String Channel
		{
			get { return _channel; }
			set { _channel = value; }
		}
		public String Message
		{
			get { return _message; }
			set { _message = value; }
		}
		
		public
		PartMessage() : base()
		{
			this.SetCommand("PART");
		}
		public
		PartMessage(String raw) : base(raw)
		{
			_channel = this.CommandParams[0];
			_message = this.CommandParams[1];
		}
		public
		PartMessage(String channel, String message)
		{
			this.SetCommand("PART");
			_channel = this.CommandParams[0] = channel;
			_message = this.CommandParams[1] = message;
		}
		
		public override String
		ToString()
		{
			if (this.SenderNick.Length == 0)
				return String.Format("Part: {0} ({1})", _channel, _message);
			else
				return String.Format("Part: {0} - {1} ({2})", this.SenderNick, _channel, _message);
		}
	}
	public class
	QuitMessage : IRCMessage
	{
		private String _message;

		public String Message
		{
			get { return _message; }
			set { _message = value; }
		}
		
		public
		QuitMessage() : base()
		{
			this.SetCommand("QUIT");
		}
		public
		QuitMessage(String raw) : base(raw)
		{
			_message = this.CommandParams[0];
		}
		public
		QuitMessage(String msg, String dummy)
		{
			this.SetCommand("QUIT");
			this.CommandParams[0] = _message = msg;
		}

		public override String
		ToString()
		{
			return String.Format("Quit: {0} ({1})", this.SenderNick, _message);
		}
	}
	public class
	TopicMessage : IRCMessage
	{
		private String _topic;
		private String _channel;

        public String Topic
        {
			get { return _topic; }
			set { _topic = value; }
        }

        public String Channel
        {
            get { return _channel; }
            set { _channel = value; }
        }
        
		public
		TopicMessage() : base()
		{
			this.SetCommand("TOPIC");
		}
		public
		TopicMessage(String raw) : base(raw)
		{
			_channel = this.CommandParams[0];
			_topic = this.CommandParams[1];
		}
		public
		TopicMessage(String channel, String param)
		{
			this.SetCommand("TOPIC");
			this.CommandParams[0] = _channel = channel;
			this.CommandParams[1] = _topic = param;
		}

		public override String
		ToString()
		{
			return String.Format("Topic: [{0}] {1}", _channel, _topic);
		}
	}
	public class
	UserMessage : IRCMessage
	{
		private String _user = "user";
		private String _hostName = "*";
		private String _serverName = "*";
		private String _realName = "";

		public
		UserMessage() : base()
		{
			this.SetCommand("USER");
		}
		public
		UserMessage(String raw) : base(raw)
		{
			this.SetCommand("USER");
			_user = this.CommandParams[0];
			_hostName = this.CommandParams[1];
			_serverName = this.CommandParams[2];
			_realName = this.CommandParams[3];
		}
		public
		UserMessage(String user, String realname)
		{
			this.SetCommand("USER");
			_user = this.CommandParams[0] = user;
			_hostName = this.CommandParams[1] = "*";
			_serverName = this.CommandParams[2] = "*";
			_realName = this.CommandParams[3] = realname;
		}
		public override String
		ToString()
		{
			return String.Format("User: {0} [{1} {2}] ({3})", _user, _hostName, _serverName, _realName);
		}
	}
	public class
	ModeMessage : IRCMessage
	{
		private String _modeargs = "";
		private String _target = "";

        public String ModeArgs
        {
            get { return _modeargs; }
            set { _modeargs = value; }
        }

        public String Target
        {
            get { return _target; }
            set { _target = value; }
        }
        
		public
		ModeMessage() : base()
		{
			this.SetCommand("MODE");
		}
		public
		ModeMessage(String raw) : base(raw)
		{
			_target = this.CommandParams[0];

            StringBuilder sb = new StringBuilder();
            for (var i = 1; i < this.CommandParams.Length; i++)
            {
                if (String.IsNullOrEmpty(this.CommandParams[i]))
                    break;
                
                if (i != 1)
                    sb.Append(" ");

                sb.Append(this.CommandParams[i]);
            }
            _modeargs = sb.ToString();
		}
		public
		ModeMessage(String target, String param)
		{
			this.SetCommand("MODE");
			this.CommandParams[0] = _target = target;
			this.CommandParams[1] = _modeargs = param;
		}

		public override String
		ToString()
		{
			if (this.Sender.Length == 0)
				return String.Format("Mode: ({0}) {1}", _target, _modeargs);
			else
				return String.Format("Mode: ({0}->{1}) {2}", this.Sender, _target, _modeargs);
			
		}
	}
}