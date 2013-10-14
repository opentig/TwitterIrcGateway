using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Misuzilla.Net.Irc;
using TypableMap;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public delegate Boolean ProcessCommand(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, String args);

    public class TypableMapCommandProcessor
    {
        private Int32 _typableMapKeySize;
        private ITypableMapStatusRepositoryFactory _typableMapFactory;
        
        public Session Session { get; private set; }
        public ITypableMapStatusRepository TypableMap { get; private set; }

        public Int32 TypableMapKeySize
        {
            get
            {
                return _typableMapKeySize;
            }
            set
            {
                if (value < 1)
                    value = 1;

                if (_typableMapKeySize != value)
                {
                    _typableMapKeySize = value;
                    TypableMap = _typableMapFactory.Create(_typableMapKeySize);
                }
            }
        }

        private Dictionary<String, ITypableMapCommand> _commands;
        public Dictionary<String, ITypableMapCommand> Commands { get { return _commands; } }
 
        private Regex _matchRE;

        public TypableMapCommandProcessor(ITypableMapStatusRepositoryFactory typableMapFactory, Session session, Int32 typableMapKeySize)
        {
            Session = session;

            _typableMapKeySize = typableMapKeySize;
            _commands = new Dictionary<string, ITypableMapCommand>(StringComparer.InvariantCultureIgnoreCase);

            _typableMapFactory = typableMapFactory;
            TypableMap = typableMapFactory.Create(typableMapKeySize);

            foreach (var t in typeof(TypableMapCommandProcessor).GetNestedTypes())
            {
                if (typeof(ITypableMapCommand).IsAssignableFrom(t) && typeof(GenericCommand) != t && t.IsClass)
                {
                    var cmd = Activator.CreateInstance(t) as ITypableMapCommand;
                    AddCommand(cmd);
                }
            }

            UpdateRegex();
        }

        public ITypableMapCommand AddCommand(ITypableMapCommand command)
        {
            _commands[command.CommandName] = command;
            UpdateRegex();
            return command;
        }
        public ITypableMapCommand AddCommand(String command, String description, ProcessCommand processCommand)
        {
            GenericCommand commandC = new GenericCommand(command, description, processCommand);
            return AddCommand(commandC);
        }
        public Boolean RemoveCommand(ITypableMapCommand command)
        {
            return RemoveCommand(command.CommandName);
        }
        public Boolean RemoveCommand(String command)
        {
            Boolean retVal = _commands.Remove(command);
            if (_commands.Count != 0)
            {
                UpdateRegex();
            }
            return retVal;
        }

        private void UpdateRegex()
        {
            List<String> keys = new List<string>();
            foreach (var key in _commands.Keys)
                keys.Add(Regex.Escape(key));

            //_matchRE = new Regex(@"^\s*(?<cmd>" + (String.Join("|", keys.ToArray())) + @")\s+(?<tid>([aiueokgsztdnhbpmyrwjvlq][aiueo])+)(\s*|\s+(?<args>.*))$", RegexOptions.IgnoreCase);
            _matchRE = new Regex(@"^\s*(?<cmd>" + (String.Join("|", keys.ToArray())) + @")\s+(?<tid>[^\s]+)(\s*|\s+(?<args>.*))$", RegexOptions.IgnoreCase);
        }

        public Boolean Process(PrivMsgMessage message)
        {
            if (_commands.Count == 0)
                return false;

            Match m = _matchRE.Match(message.Content);
            if (m.Success)
            {
                try
                {
                    Status status;
                    if (TypableMap.TryGetValue(m.Groups["tid"].Value, out status))
                    {
                        _commands[m.Groups["cmd"].Value].Process(this, message, status, m.Groups["args"].Value);
                    }
                    else
                    {
                        Session.SendServer(new NoticeMessage
                        {
                            Receiver = message.Receiver,
                            Content = "エラー: 指定された TypableMap の ID は存在しません。"
                        });
                    }
                }
                catch (Exception ex)
                {
                    Session.SendServer(new NoticeMessage
                    {
                        Receiver = message.Receiver,
                        Content = "エラー: TypableMap の処理中にエラーが発生しました。"
                    });
                    foreach (var line in ex.ToString().Split('\n'))
                    {
                        Session.SendServer(new NoticeMessage
                        {
                            Receiver = message.Receiver,
                            Content = line
                        });
                    }
                }

                return true; // 握りつぶす
            }

            return false;
        }

        public interface ITypableMapCommand
        {
            String CommandName { get; }
            Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, String args);
        }
        
        public class GenericCommand : ITypableMapCommand
        {
            private ProcessCommand _processCommandDelegate;
            private String _commandName;
            private String _description;
            
            public GenericCommand(String commandName, String description, ProcessCommand processCommand)
            {
                _commandName = commandName;
                _description = description;
                _processCommandDelegate = processCommand;
            }
            
            public String Description
            {
                get { return _description; }
            }
            
            #region ITypableMapCommand メンバ
            public string CommandName
            {
                get { return _commandName; }
            }

            public bool Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, string args)
            {
                return _processCommandDelegate(processor, msg, status, args);
            }
            #endregion
        }
        
        public class PermalinkCommand : ITypableMapCommand
        {
            #region ITypableMapCommand メンバ

            public string CommandName
            {
                get { return "u"; }
            }

            public bool Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, string args)
            {
                processor.Session.SendServer(new NoticeMessage
                {
                    Receiver = msg.Receiver,
                    Content = String.Format(
                        "http://twitter.com/{0}/statuses/{1}",
                        status.User.ScreenName,
                        status.Id)
                });
                return true;
            }
            #endregion
        }

        public class HomelinkCommand : ITypableMapCommand
        {
            #region ITypableMapCommand メンバ

            public string CommandName
            {
                get { return "h"; }
            }

            public bool Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, string args)
            {
                processor.Session.SendServer(new NoticeMessage
                {
                    Receiver = msg.Receiver,
                    Content = String.Format(
                        "http://twitter.com/{0}",
                        status.User.ScreenName)
                });
                return true;
            }

            #endregion
        }
        
        public class FavCommand : ITypableMapCommand
        {
            public virtual String CommandName { get { return "fav"; } }
            public Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, String args)
            {
                Boolean isUnfav = (String.Compare(CommandName, "unfav", true) == 0);
                processor.Session.RunCheck(() =>
                                               {
                                                   Status favStatus = (isUnfav
                                                                           ? processor.Session.TwitterService.DestroyFavorite(
                                                                                 status.Id)
                                                                           : processor.Session.TwitterService.CreateFavorite(
                                                                                 status.Id));
                                                   processor.Session.SendChannelMessage(msg.Receiver, processor.Session.CurrentNick, String.Format(
                                                                                            "ユーザ {0} のステータス \"{1}\"をFavorites{2}しました。",
                                                                                            favStatus.User.ScreenName,
                                                                                            favStatus.Text,
                                                                                            (isUnfav ? "から削除" : "に追加")), true, false, false, true);
                                               });
                return true;
            }
        }

        public class UnfavCommand : FavCommand
        {
            public override string CommandName
            {
                get
                {
                    return "unfav";
                }
            }
        }

        public class ReCommand : ITypableMapCommand
        {
            #region ITypableMapCommand メンバ

            public string CommandName
            {
                get { return "re"; }
            }

            public Boolean Process(TypableMapCommandProcessor processor, PrivMsgMessage msg, Status status, string args)
            {
                var session = processor.Session;
                if (args.Trim() == String.Empty)
                {
                    session.SendChannelMessage(msg.Receiver, Server.ServerNick, "返信に空メッセージの送信はできません。", true, false, false, true);
                    return true;
                }

                String replyMsg = String.Format("@{0} {1}", status.User.ScreenName, args);
                
                // 入力が発言されたチャンネルには必ずエコーバックする。
                // 先に出しておかないとundoがよくわからなくなる。
                session.SendChannelMessage(msg.Receiver, session.CurrentNick, replyMsg, true, false, false, false);
                session.UpdateStatusWithReceiverDeferred(msg.Receiver, replyMsg, status.Id, (updatedStatus) =>
                                                                                                {
                                                                                                });
                return true;
            }

            #endregion
        }

    }

}
