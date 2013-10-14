using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Misuzilla.Net.Irc;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class Console : MarshalByRefObject, IDisposable
    {
        public Boolean IsAttached { get; private set; }
        public String ConsoleChannelName { get; private set; }
        public Context CurrentContext { get; set; }
        public Stack<Context> ContextStack { get; set; }

        public GeneralConfig Config { get; private set; }

        internal IDictionary<Type, ContextInfo> Contexts { get; private set; }

        private Session CurrentSession { get; set; }
        private Server CurrentServer { get; set; }

        private Type _rootContextType;

        /// <summary>
        /// コンソールを有効にします。
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="server"></param>
        /// <param name="session"></param>
        /// <param name="rootContextType"></param>
        public void Attach(String channelName, Server server, Session session, Type rootContextType) 
        {
            Attach(channelName, server, session, rootContextType, true);
        }

        /// <summary>
        /// コンソールを有効にします。
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="server"></param>
        /// <param name="session"></param>
        /// <param name="rootContextType"></param>
        /// <param name="autoJoin"></param>
        public void Attach(String channelName, Server server, Session session, Type rootContextType, Boolean autoJoin) 
        {
            if (IsAttached)
                throw new InvalidOperationException("コンソールはすでにアタッチされています。");
            
            if (!channelName.StartsWith("#") && channelName.Length > 0)
                throw new ArgumentException("チャンネル名は # から始まる2文字以上の文字列を指定する必要があります。", "channelName");
            
            ConsoleChannelName = channelName;
            CurrentServer = server;
            CurrentSession = session;
            
            CurrentSession.PreMessageReceived += new EventHandler<MessageReceivedEventArgs>(Session_PreMessageReceived);
            CurrentSession.PostMessageReceived += new EventHandler<MessageReceivedEventArgs>(Session_PostMessageReceived);

            // Default Context
            _rootContextType = rootContextType;
            CurrentContext = GetContext(rootContextType, CurrentServer, CurrentSession);
            if (CurrentContext == null)
                throw new ArgumentException("指定されたコンテキストは登録されていません。", "rootContextType");

            ContextStack = new Stack<Context>();
            Config = CurrentSession.AddInManager.GetConfig<GeneralConfig>();
            Contexts = new Dictionary<Type, ContextInfo>();
        
            LoadAliases();

            // チャンネル
            Group group;
            if (!CurrentSession.Groups.TryGetValue(ConsoleChannelName, out group))
            {
                group = new Group(ConsoleChannelName);
                CurrentSession.Groups.Add(ConsoleChannelName, group);
            }
            group.IsSpecial = true;
            if (autoJoin)
            {
                group.IsJoined = true;
                CurrentSession.SendServer(new JoinMessage(ConsoleChannelName, ""));
            }

            IsAttached = true;
        }
        
        /// <summary>
        /// コンソールを終了します。
        /// </summary>
        public void Detach()
        {
            if (!IsAttached)
                throw new InvalidOperationException("コンソールはアタッチされていません。");
            
            IsAttached = false;

            CurrentSession.PreMessageReceived -= new EventHandler<MessageReceivedEventArgs>(Session_PreMessageReceived);
            CurrentSession.PostMessageReceived -= new EventHandler<MessageReceivedEventArgs>(Session_PostMessageReceived);
        
            try { CurrentContext.Dispose(); } catch {} 
            foreach (Context ctx in ContextStack)
                try { ctx.Dispose(); } catch {}

            CurrentContext = null;
        }

        /// <summary>
        /// IRCメッセージを受け取ってTIG本体に処理が渡る前の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Session_PreMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            PrivMsgMessage privMsg = e.Message as PrivMsgMessage;
            if (privMsg == null || String.Compare(privMsg.Receiver, ConsoleChannelName, true) != 0)
                return;

            ProcessMessage(privMsg);

            // 後続のAddIn,TIG本体には渡さない
            e.Cancel = true;
        }
        
        /// <summary>
        /// IRCメッセージを受け取ってTIG本体が処理を終えた後の処理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Session_PostMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            JoinMessage joinMsg = e.Message as JoinMessage;
            if (joinMsg == null || String.Compare(joinMsg.Channel, ConsoleChannelName, true) != 0)
                return;
            
            // ここに来るのは初回#Consoleを作成してJOINしたときのみ。
            // 二回目以降はサーバ側がJOINを送り出すのでこない。

            // IsSpecial を True にすることでチャンネルにタイムラインが流れないようにする
            CurrentSession.Groups[ConsoleChannelName].IsSpecial = true;

            ShowCommandsAsUsers();
        }
    
        void ProcessMessage(PrivMsgMessage privMsg)
        {
            String msgText = privMsg.Content;//.Trim();
            try
            {
                if (!CurrentContext.OnPreProcessInput(msgText))
                    ProcessInput(msgText, true);
            }
            catch (Exception e)
            {
                NotifyMessage("エラー: " + e.Message);
                CurrentSession.Logger.Error(e.ToString());
            }
        }
        
        /// <summary>
        /// 入力を処理してコマンドやコンテキスト変更などを実行する
        /// </summary>
        /// <param name="inputLine">ユーザが入力した一行</param>
        /// <param name="resolveAlias">エイリアス解決処理をするかどうか</param>
        void ProcessInput(String inputLine, Boolean resolveAlias)
        {            
            String[] args = Regex.Split(inputLine.Trim(), @"(?<!\\)\s");
            if (args.Length == 0)
                return;

            // エイリアスの処理
            if (resolveAlias)
            {
                ProcessInput(ResolveAlias(args[0], String.Join(" ", args, 1, args.Length - 1)), false);
                return;
            }

            // コンテキスト
            foreach (var ctxInfo in CurrentContext.Contexts)
            {
                if (ctxInfo.Type == _rootContextType)
                    continue;

                if (String.Compare(ctxInfo.DisplayName.Replace("Context", ""), args[0], true) == 0)
                {
                    PushContext(GetContext(ctxInfo.Type, CurrentServer, CurrentSession));

                    // 続く文字列をもう一度処理し直す
                    if (args.Length > 1)
                    {
                        ProcessInput(String.Join(" ", args, 1, args.Length - 1), true);
                    }
                    return;
                }
            }
            
            // コマンドを探す
            MethodInfo methodInfo = CurrentContext.GetCommand(args[0].Replace(":", ""));
            if (methodInfo == null)
            {
                // OnCallMissingCommand で処理できるかどうか試す
                if (!CurrentContext.OnCallMissingCommand(args[0].Replace(":", ""), inputLine))
                {
                    NotifyMessage("指定された名前はこのコンテキストのコマンド、またはサブコンテキストにも見つかりません。");
                }
                return;
            }

            try
            {
                ParameterInfo[] paramInfo = methodInfo.GetParameters();
                if (paramInfo.Length == 1 && paramInfo[0].ParameterType == typeof(String))
                {
                    methodInfo.Invoke(CurrentContext, new [] { inputLine.Substring(args[0].Length).Trim() });
                }
                else if (paramInfo.Length == 1 && paramInfo[0].ParameterType == typeof(String[]))
                {
                    String[] shiftedArgs = new string[args.Length - 1];
                    Array.Copy(args, 1, shiftedArgs, 0, shiftedArgs.Length);
                    
                    methodInfo.Invoke(CurrentContext, (shiftedArgs.Length == 0 ? null : new [] { shiftedArgs }));
                }
                else
                {
                    List<Object> convertedArgs = new List<object>();
                    for (var i = 0; i < paramInfo.Length && i < (args.Length - 1); i++)
                    {
                        var pi = paramInfo[i];
                        
                        TypeConverter typeConv = TypeDescriptor.GetConverter(pi.ParameterType);
                        if (i == paramInfo.Length-1)
                        {
                            // 最後のパラメータ(受け取る引数が2個とかで3つ指定されていたら合体させて押し込む)
                            convertedArgs.Add(typeConv.ConvertFromString(String.Join(" ", args, i + 1, (args.Length - (i + 1)))));
                        }
                        else
                        {
                            convertedArgs.Add(typeConv.ConvertFromString(args[i+1]));
                        }
                    }
                    methodInfo.Invoke(CurrentContext, ((convertedArgs.Count != 0) ? convertedArgs.ToArray() : null));
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    ex = ex.InnerException;
                
                NotifyMessage("コマンドを実行時にエラーが発生しました:");
                foreach (var line in ex.Message.Split('\n'))
                {
                    NotifyMessage(line);
                }
            }
        }


        /// <summary>
        /// クライアントにメッセージをコンテキスト名からのNOTICEで送信します。
        /// </summary>
        /// <param name="message">メッセージ</param>
        public void NotifyMessage(String message)
        {
            if (!IsAttached) return;
            StringBuilder sb = new StringBuilder();
            foreach (Context ctx in ContextStack)
                sb.Insert(0, ctx.ContextName.Replace("Context", "") + "\\");

            sb.Append(CurrentContext.ContextName.Replace("Context", ""));

            NotifyMessage(sb.ToString(), message);
        }
        /// <summary>
        /// クライアントにメッセージをNOTICEで送信します。
        /// </summary>
        /// <param name="senderNick">送信者のニックネーム</param>
        /// <param name="message">メッセージ</param>
        public void NotifyMessage(String senderNick, String message)
        {
            if (!IsAttached) return;
            foreach (var line in message.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                CurrentSession.Send(new NoticeMessage(ConsoleChannelName, line) { SenderHost = "twitter@" + Server.ServerName, SenderNick = senderNick });
            }
        }

        /// <summary>
        /// クライアントに対しコマンドをユーザとしてみせます
        /// </summary>
        public void ShowCommandsAsUsers()
        {
            MethodInfo[] methodInfoArr = CurrentContext.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
            Type t = typeof(Context);
            List<String> users = new List<string>();

            foreach (var methodInfo in methodInfoArr)
            {
                if (t.IsAssignableFrom(methodInfo.DeclaringType) && !methodInfo.IsConstructor && !methodInfo.IsFinal && !methodInfo.IsSpecialName)
                {
                    Object[] attrs = methodInfo.GetCustomAttributes(typeof(BrowsableAttribute), true);
                    if (attrs.Length != 0 && !((BrowsableAttribute)attrs[0]).Browsable)
                        continue;

                    users.Add(methodInfo.Name);
                }
            }

            CurrentSession.SendNumericReply(NumericReply.RPL_NAMREPLY, "=", ConsoleChannelName, "@"+CurrentSession.Nick+" "+ String.Join(" ", users.ToArray()));
            CurrentSession.SendNumericReply(NumericReply.RPL_ENDOFNAMES, ConsoleChannelName, "End of NAMES list");
        }
        
        /// <summary>
        /// コンテキストを追加します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void RegisterContext<T>() where T : Context, new()
        {
            RegisterContext(typeof(T));
        }
        
        /// <summary>
        /// コンテキストを追加します。
        /// </summary>
        /// <param name="contextType"></param>
        public void RegisterContext(Type contextType)
        {
            RegisterContext(contextType, contextType.Name, AttributeUtil.GetDescription(contextType));
        }

        /// <summary>
        /// コンテキストを追加します。
        /// </summary>
        /// <param name="contextType"></param>
        public void RegisterContext(Type contextType, String contextName, String description)
        {
            if (!typeof(Context).IsAssignableFrom(contextType))
                throw new ArgumentException("指定された型は Context クラスを継承していません。", "contextType");
            if (contextType.IsAbstract)
                throw new ArgumentException("指定された型は抽象型です。", "contextType");
            
            if (!Contexts.ContainsKey(contextType))
                Contexts.Add(contextType, new ContextInfo() { Type = contextType, Description = description, DisplayName = contextName });
        }
        /// <summary>
        /// コンテキストを削除します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void UnregisterContext<T>() where T : Context, new()
        {
            UnregisterContext(typeof(T));
        }
        
        /// <summary>
        /// コンテキストを削除します。
        /// </summary>
        /// <param name="contextType"></param>
        public void UnregisterContext(Type contextType)
        {
            if (!Contexts.ContainsKey(contextType))
                Contexts.Remove(contextType);
        }
        
        /// <summary>
        /// コンテキストをインスタンス化して返します。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="server"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public Context GetContext<T>(Server server, Session session) where T : Context, new()
        {
            Context ctx = new T { CurrentServer = server, CurrentSession = session, Console = this };
            ctx.Initialize();
            return ctx;
        }
        
        /// <summary>
        /// コンテキストをインスタンス化して返します。
        /// </summary>
        /// <param name="t"></param>
        /// <param name="server"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        public Context GetContext(Type t, Server server, Session session)
        {
            Context ctx = Activator.CreateInstance(t) as Context;
            ctx.CurrentServer = server;
            ctx.CurrentSession = session;
            ctx.Console = this;
            ctx.Initialize();
            return ctx;
        }

        #region Alias Helpers
        private Dictionary<String, Dictionary<String, String>> _aliases = new Dictionary<String, Dictionary<String, String>>();
        private String ResolveAlias(String aliasName, String afterString)
        {
            String tFullName = CurrentContext.GetType().FullName;
            String command = aliasName;
            if (_aliases.ContainsKey(tFullName) && _aliases[tFullName].ContainsKey(aliasName))
            {
                command = _aliases[tFullName][aliasName];
            }

            return command + ((afterString.Length) > 0 ? " " + afterString : "");
        }
        
        public Dictionary<String, String> GetAliasesByType(Type contextType)
        {
            if (_aliases.ContainsKey(contextType.FullName))
            {
                return _aliases[contextType.FullName];
            }
            else
            {
                return new Dictionary<string, string>();
            }
        }
        
        /// <summary>
        /// 指定したタイプのコマンドのエイリアスを登録します。
        /// </summary>
        /// <param name="contextType"></param>
        /// <param name="aliasName"></param>
        /// <param name="aliasCommand"></param>
        public void RegisterAliasByType(Type contextType, String aliasName, String aliasCommand)
        {
            if (!_aliases.ContainsKey(contextType.FullName))
                _aliases[contextType.FullName] = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _aliases[contextType.FullName][aliasName] = aliasCommand;
            
            SaveAliases();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="contextType"></param>
        /// <param name="aliasName"></param>
        public void UnregisterAliasByType(Type contextType, String aliasName)
        {
            if (_aliases.ContainsKey(contextType.FullName))
            {
                _aliases[contextType.FullName].Remove(aliasName);
            }
            
            SaveAliases();
        }
        private void SaveAliases()
        {
            List<String> configAliases = new List<string>();

            foreach (var aliasesByType in _aliases)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(aliasesByType.Key).Append('\n');
                foreach (var alias in aliasesByType.Value)
                {
                    sb.Append(alias.Key).Append('\t').Append(alias.Value).Append('\n');
                }
                configAliases.Add(sb.ToString());
            }

            Config.ConsoleAliases = configAliases;
            CurrentSession.AddInManager.SaveConfig(Config);
        }
        private void LoadAliases()
        {
            _aliases = new Dictionary<string, Dictionary<string, string>>();
            foreach (var entry in Config.ConsoleAliases)
            {
                String[] parts = entry.Split('\n');
                if (parts.Length > 0)
                {
                    // 一行目がType.FullName
                    _aliases[parts[0]] = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    // 二行目以降<Alias>\t<CommandString>
                    for (var i = 1; i < parts.Length; i++)
                    {
                        String[] alias = parts[i].Split(new char[]{'\t'}, 2);
                        if (alias.Length == 2)
                            _aliases[parts[0]][alias[0]] = alias[1];
                    }
                }
            }
        }
        #endregion

        #region Context Helpers
        public void ChangeContext(Context ctx)
        {
            ContextStack.Clear();
            CurrentContext = ctx;
            NotifyMessage("コンテキストを変更しました。");
            ShowCommandsAsUsers();
        }

        public void PushContext(Context ctx)
        {
            ContextStack.Push(CurrentContext);
            CurrentContext = ctx;
            NotifyMessage("コンテキストを変更しました。");
            ShowCommandsAsUsers();
        }

        public void PopContext()
        {
            if (ContextStack.Count > 0)
            {
                CurrentContext.Dispose();
                CurrentContext = ContextStack.Pop();
                NotifyMessage("コンテキストを変更しました。");
                ShowCommandsAsUsers();

            }
        }
        #endregion

        #region IDisposable メンバ

        public void Dispose()
        {
            if (CurrentSession != null)
            {
                CurrentSession.Logger.Information((ConsoleChannelName ?? "(AnonConsoleChannnelName)") + ": Dispose");
            }
            if (ContextStack != null)
            {
                foreach (var ctx in ContextStack)
                {
                    try
                    {
                        ctx.Dispose();
                    }
                    catch
                    {
                    }
                }
                ContextStack = null;
            }

            if (CurrentContext != null)
            {
                try
                {
                    CurrentContext.Dispose();
                }
                catch
                {
                }
                CurrentContext = null;
            }
        
            GC.SuppressFinalize(this);
        }

        #endregion
    
        ~Console()
        {
            Dispose();
        }
    }

}
