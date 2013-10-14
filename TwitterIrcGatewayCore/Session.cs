using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.AccessControl;
using System.Text.RegularExpressions;
using System.Threading;
using Misuzilla.Applications.TwitterIrcGateway.Filter;
using Misuzilla.Net.Irc;
using System.Security.Permissions;
using System.Security;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// ユーザからの接続を管理するクラス
    /// </summary>
    public partial class Session : SessionBase, IDisposable
    {
        private readonly static String ConfigBasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Configs");

        private Server _server;
        private TwitterService _twitter;
        private LinkedList<Int64> _lastStatusIdsFromGateway;
        private Dictionary<String, Int64> _lastStatusIdsByScreenName;
        private Groups _groups;
        private Filters _filter;
        private Config _config;
        private AddInManager _addinManager;

        private HashSet<User> _followingUsers = new HashSet<User>();
        private Boolean _isFirstTime = true;

        private User _twitterUser;

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
        public event EventHandler<SessionStartedEventArgs> SessionStarted;
        /// <summary>
        /// セッション終了時のイベント
        /// </summary>
        public event EventHandler<EventArgs> SessionEnded;

        /// <summary>
        /// 設定変更時のイベント
        /// </summary>
        public event EventHandler<EventArgs> ConfigChanged;

        /// <summary>
        /// アドインをすべて読み込んで Initialize 完了時のイベント
        /// </summary>
        public event EventHandler<EventArgs> AddInsLoadCompleted;

        /// <summary>
        /// 受信したタイムラインステータスのセットを処理前のイベント
        /// </summary>
        public event EventHandler<TimelineStatusesEventArgs> PreProcessTimelineStatuses;
        /// <summary>
        /// タイムラインステータスを処理前のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PreProcessTimelineStatus;
        /// <summary>
        /// フィルタ処理前のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PreFilterProcessTimelineStatus;
        /// <summary>
        /// フィルタ処理後のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PostFilterProcessTimelineStatus;
        /// <summary>
        /// タイムラインステータスの送信前のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PreSendMessageTimelineStatus;
        /// <summary>
        /// 受信したステータスメッセージの送信先グループ決定時のイベント
        /// </summary>
        public event EventHandler<TimelineStatusRoutedEventArgs> MessageRoutedTimelineStatus;
        /// <summary>
        /// 受信したステータスメッセージをグループに送信前のイベント
        /// </summary>
        public event EventHandler<TimelineStatusGroupEventArgs> PreSendGroupMessageTimelineStatus;
        /// <summary>
        /// 受信したステータスメッセージをグループに送信後のイベント
        /// </summary>
        public event EventHandler<TimelineStatusGroupEventArgs> PostSendGroupMessageTimelineStatus;
        /// <summary>
        /// タイムラインステータスをすべてのグループに送信後のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PostSendMessageTimelineStatus;
        /// <summary>
        /// タイムラインステータスを処理後のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> PostProcessTimelineStatus;
        /// <summary>
        /// 受信したタイムラインステータスのセットを処理後のイベント
        /// </summary>
        public event EventHandler<TimelineStatusesEventArgs> PostProcessTimelineStatuses;

        /// <summary>
        /// IRCクライアントからのステータス更新要求を受信時のイベント
        /// </summary>
        public event EventHandler<StatusUpdateEventArgs> UpdateStatusRequestReceived;
        /// <summary>
        /// Twitterのステータス更新を行う直前時のイベント
        /// </summary>
        public event EventHandler<StatusUpdateEventArgs> PreSendUpdateStatus;
        /// <summary>
        /// Twitterのステータス更新を行った直後時のイベント
        /// </summary>
        public event EventHandler<StatusUpdateEventArgs> PostSendUpdateStatus;
        /// <summary>
        /// IRCクライアントからのステータス更新要求が完了時のイベント
        /// </summary>
        public event EventHandler<TimelineStatusEventArgs> UpdateStatusRequestCommited;
        #endregion

        public Session(User user, Server server) : base(user.Id.ToString(), server)
        {
            _twitterUser = user;
            
            _groups = new Groups();
            _filter = new Filters();
            _config = new Config();

            _server = server;
            _lastStatusIdsFromGateway = new LinkedList<Int64>();
            _lastStatusIdsByScreenName = new Dictionary<string, Int64>(StringComparer.InvariantCultureIgnoreCase);
            
            _addinManager = new AddInManager(_server, this);
            //_addinManager = AddInManager.CreateInstanceWithAppDomain(_server, this);

            Logger = new SessionTraceLogger(this);
            PostWaitList = new List<Deferred.DeferredState<Boolean>>();
        }

        ~Session()
        {
            //_Counter.Decrement(ref _Counter.Session);
            Dispose();
        }

        /// <summary>
        /// 開始されているかどうかを返します。
        /// </summary>
        public Boolean IsStarted
        {
            get;
            private set;
        }
        
        /// <summary>
        /// 現在のニックネームを取得します
        /// </summary>
        public String Nick
        {
            get { return CurrentNick; }
        }
        
        /// <summary>
        /// セッションに結びつけられたTwitterへのAPIアクセスのためのサービスを取得します
        /// </summary>
        public TwitterService TwitterService
        {
            get { return _twitter; }
        }
        
        /// <summary>
        /// セッションに結びつけられた設定を取得します
        /// </summary>
        public Config Config
        {
            get { return _config; }
        }
        
        /// <summary>
        /// セッションが持つグループのコレクションを取得します
        /// </summary>
        public Groups Groups
        {
            get { return _groups;  }
        }
        
        /// <summary>
        /// セッションが持つフィルタのコレクションを取得します
        /// </summary>
        public Filters Filters
        {
            get { return _filter;  }
        }
        
        /// <summary>
        /// セッションのアドインマネージャを取得します
        /// </summary>
        public AddInManager AddInManager
        {
            get { return _addinManager;  }
        }        
        
        /// <summary>
        /// ユーザの設定が保存されているディレクトリのパスを取得します
        /// </summary>
        public String UserConfigDirectory
        {
#if HOSTING
            get { return Path.Combine(ConfigBasePath, _twitterUser.Id.ToString()); }
#else
            get { return Path.Combine(ConfigBasePath, _twitterUser.ScreenName); }
#endif
        }

        /// <summary>
        /// 接続に利用しているTwitterのアカウントのユーザ情報を取得します
        /// </summary>
        public User TwitterUser
        {
            get { return _twitterUser; }
        }

        /// <summary>
        /// ログを出力するためのクラスのインスタンスを取得します
        /// </summary>
        public Logger Logger
        {
            get; private set;
        }
        
        /// <summary>
        /// フォローしているユーザの一覧(DisableUserListが有効の場合には空になります)
        /// </summary>
        public HashSet<User> FollowingUsers
        {
            get { return _followingUsers; }
        }

        /// <summary>
        /// 送信予定のステータスメッセージキューを取得します。
        /// </summary>
        public List<Deferred.DeferredState<Boolean>> PostWaitList
        {
            get; private set;
        }
        
        /// <summary>
        /// セッションを開始します。
        /// </summary>
        private void Start()
        {
            CheckDisposed();

            // アドインの読み込み
            SendTwitterGatewayServerMessage("* アドインを読み込んでいます...");
            _addinManager.Load();
            FireEvent(AddInsLoadCompleted, EventArgs.Empty);
            SendTwitterGatewayServerMessage("* アドインを読み込みました。");

            _twitter.Start();

            IsStarted = true;
        }
        
        #region イベント実行メソッド
        internal virtual void OnAddInsLoadCompleted()
        {
            FireEvent(AddInsLoadCompleted, EventArgs.Empty);
        }
        
        protected virtual void OnMessageReceived(IRCMessage msg, MessageReceivedEventArgs e)
        {
            Debug.WriteLine(msg.ToString());

            if (FireEvent(PreMessageReceived, e))
            {
                if (FireEvent(MessageReceived, e))
                {
                    FireEvent(PostMessageReceived, e);
                }
            }
        }
        protected virtual void OnSessionStarted(String username)
        {
            FireEvent(SessionStarted, new SessionStartedEventArgs(username, _twitterUser, (IPEndPoint)Connections[0].TcpClient.Client.RemoteEndPoint));
        }
        protected virtual void OnSessionEnded()
        {
            FireEvent(SessionEnded, EventArgs.Empty);
        }
        internal virtual void OnConfigChanged()
        {
            if (ConfigChanged != null)
                ConfigChanged(this, EventArgs.Empty);

#if FALSE
            if (_traceListener == null && _config.EnableTrace)
            {
                _traceListener = new IrcTraceListener(this);
                Trace.Listeners.Add(_traceListener);
            }
            else if ((_traceListener != null) && !_config.EnableTrace)
            {
                Trace.Listeners.Remove(_traceListener);
                _traceListener = null;
            }
#endif
        }

        public void LoadSettings()
        {
            LoadConfig();
            OnConfigChanged();

            LoadGroups();
            LoadFilters();
        }
        
        public String GetSettingPath(String fileName)
        {
            return Path.Combine(UserConfigDirectory, fileName);
        }

        /// <summary>
        /// 
        /// </summary>
        public void LoadFilters()
        {
            // filters 読み取り
            String path = GetSettingPath("Filters.xml");
            try
            {
                _filter = Filters.Load(path);
            }
            catch (IOException ie)
            {
                SendTwitterGatewayServerMessage("エラー: " + ie.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void SaveFilters()
        {
            lock (_filter)
            {
                String path = GetSettingPath("Filters.xml");
                try
                {
                    _filter.Save(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void LoadGroups()
        {
            // group 読み取り
            lock (_groups)
            {
                String path = GetSettingPath("Groups.xml");
                try
                {
                    _groups = Groups.Load(path);

                    // 下位互換性FIX: グループに自分自身のNICKは存在しないようにします
                    foreach (Group g in _groups.Values)
                    {
                        g.Members.Remove(CurrentNick);
                    }
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void SaveGroups()
        {
            // group 読み取り
            lock (_groups)
            {
                String path = GetSettingPath("Groups.xml");
                try
                {
                    _groups.Save(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        public void LoadConfig()
        {
            lock (_config)
            {
                String path = GetSettingPath("Config.xml");
                try
                {
                    _config = Config.Load(path);
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }        
        /// <summary>
        /// 
        /// </summary>
        public void SaveConfig()
        {
            lock (_config)
            {
                String path = GetSettingPath("Config.xml");
                try
                {
                    _config.Save(path);
                    OnConfigChanged();
                }
                catch (IOException ie)
                {
                    SendTwitterGatewayServerMessage("エラー: " + ie.Message);
                }
            }
        }
 
        #endregion

        private Group GetGroupByChannelName(String channelName)
        {
            // グループを取得/作成
            Group group;
            if (!_groups.TryGetValue(channelName, out group))
            {
                group = new Group(channelName);
                _groups.Add(channelName, group);
            }
            return group;
        }


        private void InitializeSession()
        {
            // 設定を読み込む
            SendTwitterGatewayServerMessage("* 設定を読み込んでいます...");
            LoadSettings();
            
            //
            // Twitte Service Setup
            //
            if (((Connection)Connections[0]).Identity != null)
            {
                _twitter = new TwitterService(_server.OAuthClientKey, _server.OAuthSecretKey, ((Connection)Connections[0]).Identity);
            }
            else
            {
                _twitter = new TwitterService(Connections[0].UserInfo.UserName, Connections[0].UserInfo.Password);
            }
            _twitter.EnableCompression = Config.Default.EnableCompression; // TODO: なんとかする
            _twitter.BufferSize = _config.BufferSize;
            _twitter.Interval = _config.Interval;
            _twitter.IntervalDirectMessage = _config.IntervalDirectMessage;
            _twitter.IntervalReplies = _config.IntervalReplies;
            _twitter.EnableRepliesCheck = _config.EnableRepliesCheck;
            _twitter.POSTFetchMode = _config.POSTFetchMode;
            _twitter.FetchCount = _config.FetchCount;
            _twitter.FriendsPerPageThreshold = _config.FriendsPerPageThreshold;
            _twitter.RepliesReceived += new EventHandler<StatusesUpdatedEventArgs>(twitter_RepliesReceived);
            _twitter.TimelineStatusesReceived += new EventHandler<StatusesUpdatedEventArgs>(twitter_TimelineStatusesReceived);
            _twitter.CheckError += new EventHandler<ErrorEventArgs>(twitter_CheckError);
            _twitter.DirectMessageReceived += new EventHandler<DirectMessageEventArgs>(twitter_DirectMessageReceived);
            if (_server.Proxy != null)
                _twitter.Proxy = _server.Proxy;

            // IRC メッセージ
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PRIVMSG);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_WHOIS);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_INVITE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_JOIN);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PART);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_KICK);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_LIST);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TOPIC);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_MODE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_PING);
#if ENABLE_IM_SUPPORT
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGIMENABLE);
            MessageReceived += new EventHandler<MessageReceivedEventArgs>(MessageReceived_TIGIMDISABLE);
            if (!String.IsNullOrEmpty(_config.IMServiceServerName))
            {
                ConnectToIMService(true);
            }
#endif
            SendTwitterGatewayServerMessage("* セッションを開始しました。");
            OnSessionStarted(_twitterUser.ScreenName);
            Logger.Information("SessionStarted: UserName={0}; Nickname={1}", Connections[0].UserInfo.UserName, CurrentNick);
            Logger.Information("User: Id={0}, ScreenName={1}, Name={2}", _twitterUser.Id, _twitterUser.ScreenName, _twitterUser.Name);
        }

        #region メッセージ処理イベント
        private void MessageReceived_JOIN(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is JoinMessage)) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            JoinMessage joinMsg = e.Message as JoinMessage;
            Logger.Information(String.Format("Join: {0} -> {1}", joinMsg.Sender, joinMsg.Channel));
            String[] channelNames = joinMsg.Channel.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String channelName in channelNames)
            {
                // メインチャンネルはスキップ
                if (String.Compare(channelName, _config.ChannelName, true) == 0)
                    continue;
                
                if (!channelName.StartsWith("#") || channelName.Length < 3)
                {
                    Debug.WriteLine(String.Format("No nick/such channel: {0}", channelName));
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                    continue;
                }

                // グループを取得/作成
                Group group = GetGroupByChannelName(channelName);
                if (!group.IsJoined)
                {
                    JoinChannel(this, group);
                }
            }
       }
        


        private void MessageReceived_PART(object sender, MessageReceivedEventArgs e)
        {
            if (!(e.Message is PartMessage)) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            PartMessage partMsg = e.Message as PartMessage;
            Logger.Information("Part: {0} -> {1}", partMsg.Sender, partMsg.Channel);
            String[] channelNames = partMsg.Channel.Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (String channelName in channelNames)
            {
                // メインチャンネルはスキップ
                if (String.Compare(channelName, _config.ChannelName, true) == 0)
                    continue;
                
                if (!channelName.StartsWith("#") || channelName.Length < 3)
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                    continue;
                }

                // グループを取得/作成
                Group group;
                if (_groups.TryGetValue(channelName, out group))
                {
                    group.IsJoined = false;
                }
                else
                {
                    SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                    continue;
                }
                partMsg = new PartMessage(channelName, "");
                SendServer(partMsg);

                // もう捨てていい?
                if (group.Members.Count == 0)
                {
                    _groups.Remove(group.Name);
                    SendTwitterGatewayServerMessage("グループ \""+group.Name+"\" を削除しました。");
                }
            }

            SaveGroups();
        }
        private void MessageReceived_KICK(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "KICK", true) != 0) return;

            String[] channels = e.Message.CommandParams[0].Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            String[] kickTargets = e.Message.CommandParams[1].Split(new Char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (channels.Length == 0 || (channels.Length != 1 && channels.Length != kickTargets.Length))
            {
                SendErrorReply(ErrorReply.ERR_NEEDMOREPARAMS, "Not enough parameters");
                return;
            }

            if (channels.Length == 1)
            {
                // 一チャンネルから複数けりだす
                Group group;
                if (!_groups.TryGetValue(channels[0], out group))
                {
                    SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                    return;
                }
                foreach (String kickTarget in kickTargets)
                {
                    if (group.Exists(kickTarget))
                    {
                        group.Remove(kickTarget);

                        OtherMessage kickMsg = new OtherMessage("KICK");
                        kickMsg.Sender = e.Message.Sender;
                        kickMsg.CommandParams[0] = channels[0];
                        kickMsg.CommandParams[1] = kickTarget;
                        kickMsg.CommandParams[2] = e.Message.CommandParams[2];
                        Send(kickMsg);
                    }
                    else
                    {
                        SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                        return;
                    }
                }
            }
            else
            {
                // 複数チャンネルからそれぞれ
                for (Int32 i = 0; i < channels.Length; i++)
                {
                    String channelName = channels[i];
                    Group group;
                    if (!_groups.TryGetValue(channelName, out group))
                    {
                        SendErrorReply(ErrorReply.ERR_NOTONCHANNEL, "You're not on that channel");
                        return;
                    }
                    if (group.Exists(kickTargets[i]))
                    {
                        group.Remove(kickTargets[i]);
                        
                        OtherMessage kickMsg = new OtherMessage("KICK");
                        kickMsg.Sender = e.Message.Sender;
                        kickMsg.CommandParams[0] = group.Name;
                        kickMsg.CommandParams[1] = kickTargets[i];
                        kickMsg.CommandParams[2] = e.Message.CommandParams[2];
                        Send(kickMsg);
                    }
                    else
                    {
                        SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                        return;
                    }
                }
            }

            SaveGroups();
        }
        private void MessageReceived_LIST(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "LIST", true) != 0) return;
            foreach (Group group in _groups.Values)
            {
                SendNumericReply(NumericReply.RPL_LIST, group.Name, group.Members.Count.ToString(), "");
            }
            SendNumericReply(NumericReply.RPL_LISTEND, "End of LIST");
        }
        private void MessageReceived_INVITE(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "INVITE", true) != 0) return;
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
                return;
            }

            String userName = e.Message.CommandParams[0];
            String channelName = e.Message.CommandParams[1];
            Logger.Information("Invite: {0} -> {1}", userName, channelName);
            if (!channelName.StartsWith("#") || channelName.Length < 3 || String.Compare(channelName, _config.ChannelName, true) == 0)
            {
                SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such nick/channel");
                return;
            }

            // グループを取得、ユーザ追加
            Group group = GetGroupByChannelName(channelName);
            if (!group.Exists(userName))
            {
                group.Add(userName);
            }
            if (group.IsJoined)
            {
                JoinMessage joinMsg = new JoinMessage(channelName, "");
                joinMsg.SenderHost = "twitter@" + Server.ServerName;
                joinMsg.SenderNick = userName;
                Send(joinMsg);
            }

            SaveGroups();
        }

        void MessageReceived_PRIVMSG(object sender, MessageReceivedEventArgs e)
        {
            PrivMsgMessage message = e.Message as PrivMsgMessage;
            if (message == null) return;

            StatusUpdateEventArgs eventArgs = new StatusUpdateEventArgs(message, message.Content);
            if (!FireEvent(UpdateStatusRequestReceived, eventArgs)) return;

            UpdateStatusWithReceiverDeferred(message.Receiver, eventArgs.Text);
        }

        #region Update Status Methods
        /// <summary>
        /// 設定された時間待機した後Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <returns></returns>
         public Deferred.DeferredState<Boolean> UpdateStatusWithReceiverDeferred(String receiver, String message)
        {
            return UpdateStatusWithReceiverDeferred(receiver, message, 0);
        }

        /// <summary>
        /// 設定された時間待機した後Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="inReplyToId"></param>
        /// <returns></returns>
        public Deferred.DeferredState<Boolean> UpdateStatusWithReceiverDeferred(String receiver, String message, Int64 inReplyToId)
        {
            return UpdateStatusWithReceiverDeferred(receiver, message, inReplyToId, null);
        }

        /// <summary>
        /// 設定された時間待機した後Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。完了時に指定されたコールバックメソッドを呼び出します。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="inReplyToId"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public Deferred.DeferredState<Boolean> UpdateStatusWithReceiverDeferred(String receiver, String message, Int64 inReplyToId, Action<Status> callback)
        {
            return UpdateStatusWithReceiverDeferredInternal(receiver, message, inReplyToId, callback);
        }

        private Deferred.DeferredState<Boolean> UpdateStatusWithReceiverDeferredInternal(String receiver, String message, Int64 inReplyToId, Action<Status> callback)
        {
            // 140文字制限のチェック
            if (!CheckMessageLength(receiver, message))
            {
                return Deferred.DeferredInvoke<Boolean>(() => false, 0);
            }

            Deferred.DeferredState<Boolean> state = Deferred.DeferredInvoke<String, String, Int64, Action<Status>, Boolean>(UpdateStatusWithReceiver, Config.UpdateDelayTime * 1000, (asyncResult) => {
                Deferred.DeferredState<Boolean> state_ = asyncResult.AsyncState as Deferred.DeferredState<Boolean>;
                
                // 送信リストから外す
                lock (PostWaitList)
                    PostWaitList.Remove(state_);

            }, receiver, message, inReplyToId, callback);

            PostWaitList.Add(state);
            
            return state;
        }


        /// <summary>
        /// Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public Boolean UpdateStatusWithReceiver(String receiver, String message)
        {
            return UpdateStatusWithReceiver(receiver, message, 0);
        }

        /// <summary>
        /// Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="inReplyToId"></param>
        /// <returns></returns>
        public Boolean UpdateStatusWithReceiver(String receiver, String message, Int64 inReplyToId)
        {
            return UpdateStatusWithReceiver(receiver, message, inReplyToId, null);
        }

        /// <summary>
        /// Twitterのステータスを更新し、失敗した場合には指定されたチャンネルに通知し、リトライします。完了時に指定されたコールバックメソッドを呼び出します。
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="inReplyToId"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public Boolean UpdateStatusWithReceiver(String receiver, String message, Int64 inReplyToId, Action<Status> callback)
        {
            return UpdateStatusWithReceiverInternal(receiver, message, inReplyToId, callback);
        }

        private Boolean UpdateStatusWithReceiverInternal(String receiver, String message, Int64 inReplyToId, Action<Status> callback)
        {
            Boolean isRetry = false;
            Boolean succeed = true;
        Retry:
            try
            {
                // チャンネル宛は自分のメッセージを書き換え
                if ((String.Compare(receiver, _config.ChannelName, true) == 0) || receiver.StartsWith("#"))
                {
                    String postMessage = message;
                    
                    // 140文字制限のチェック
                    if (!CheckMessageLength(receiver, message))
                    {
                        return false;
                    }
                    
                    try
                    {
                        // InReplyId が 0 じゃないときは指定されている扱い
                        Status status = (inReplyToId > 0) ? UpdateStatus(message, inReplyToId) : UpdateStatus(message);
                        message = status.Text;
                        if (callback != null)
                        {
                            try
                            {
                                callback(status);
                            }
                            catch (Exception e)
                            {
                                Logger.Error(e.ToString());
                            }
                        }
                        if (!FireEvent(UpdateStatusRequestCommited, new TimelineStatusEventArgs(status))) return false;
                    }
                    catch (TwitterServiceException tse)
                    {
                        SendTwitterGatewayServerMessage("エラー: メッセージは完了しましたが、レスポンスを正しく受信できませんでした。(" + tse.Message + ")");
                    }

                    // ほかのグループに送信する
                    SendChannelMessage(receiver, CurrentNick, postMessage, false, true, true, false);
                }
                else if (String.Compare(receiver, "trace", true) != 0)
                {
                    // 人に対する場合はDirect Message
                    _twitter.SendDirectMessage(receiver, message);
                }
                if (isRetry)
                {
                    SendChannelMessage(receiver, Server.ServerNick, "メッセージ送信のリトライに成功しました。", true, false, false, true);
                }
            }
            catch (WebException ex)
            {
                String content = String.Format("メッセージ送信に失敗しました({0})" + (!isRetry ? "/リトライします。" : ""), TwitterOAuth.GetMessageFromException(ex).Replace("\n", " "));
                SendChannelMessage(receiver, Server.ServerNick, content, true, false, false, true);
#if DEBUG
                //var retVal = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
                //foreach (var line in retVal.Split('\n'))
                //    SendChannelMessage(receiver, Server.ServerNick, line.Trim(), true, false, false, true);
#endif
                // 一回だけリトライするよ
                if (!isRetry)
                {
                    isRetry = true;
                    goto Retry;
                }
                else
                {
                    succeed = false;
                }
            }

            return succeed;
        }

        /// <summary>
        /// メッセージの長さをチェックして、長すぎる場合には送信元チャンネルにメッセージを返します。
        /// </summary>
        /// <returns></returns>
        private Boolean CheckMessageLength(String receiver, String message)
        {
            // 140文字制限のチェック
            var tmpMessage = Regex.Replace(message, "https?://[-_.!~*'()a-zA-Z0-9;/?:@&=+$,%#]+", "http://t.co/__________");
            if (tmpMessage.Length > 140)
            {
                Int32 overCharCount = tmpMessage.Length - 140;
                SendChannelMessage(receiver, Server.ServerNick,
                                   String.Format("140文字を超えたメッセージの送信は出来ません。{0}文字の超過です。(おおよその場所: {1}...)", overCharCount, tmpMessage.Substring(140, Math.Min(5, overCharCount))), true, false, false, true);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 設定された時間待機した後Twitterのステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Deferred.DeferredState<Status> UpdateStatusAsync(String message)
        {
            return Deferred.DeferredInvoke<String, Status>(UpdateStatus, Config.UpdateDelayTime * 1000, message);
        }

        /// <summary>
        /// 設定された時間待機した後Twitterのステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Deferred.DeferredState<Status> UpdateStatusAsync(String message, Int64 inReplyToStatusId)
        {
            return Deferred.DeferredInvoke<String, Int64, Status>(UpdateStatus, Config.UpdateDelayTime * 1000, message, inReplyToStatusId);
        }

        /// <summary>
        /// Twitterのステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Status UpdateStatus(String message)
        {
            // 送信(もし以前のスタイルのReplyが有効の場合には対象のユーザの最後に受信したIDにくっつける)
            if (_config.EnableOldStyleReply)
            {
                Match match = Regex.Match(message, "^@([A-Za-z0-9_]+)");
                if (match.Success && _lastStatusIdsByScreenName.ContainsKey(match.Groups[1].Value))
                    return UpdateStatus(message, _lastStatusIdsByScreenName[match.Groups[1].Value]);
                else
                    return UpdateStatus(message, 0);
            }
            else
            {
                return UpdateStatus(message, 0);
            }
        }

        /// <summary>
        /// Twitterのステータスを更新します。
        /// </summary>
        /// <param name="message"></param>
        /// <param name="inReplyToStatusId"></param>
        /// <returns></returns>
        public Status UpdateStatus(String message, Int64 inReplyToStatusId)
        {
            StatusUpdateEventArgs eventArgs = new StatusUpdateEventArgs(message, inReplyToStatusId);
            if (!FireEvent(PreSendUpdateStatus, eventArgs)) return null;

            Status status = _twitter.UpdateStatus(eventArgs.Text, inReplyToStatusId);
                        
            if (status != null)
            {
                Logger.Information("Status Update: {0} (ID:{1}, CreatedAt:{2}; InReplyToStatusId:{3})", status.Text, status.Id.ToString(), status.CreatedAt.ToString(), inReplyToStatusId);

                _lastStatusIdsFromGateway.AddLast(status.Id);
                if (_lastStatusIdsFromGateway.Count > 100)
                {
                    _lastStatusIdsFromGateway.RemoveFirst();
                }
            }

            eventArgs.CreatedStatus = status;
            if (!FireEvent(PostSendUpdateStatus, eventArgs)) return null;

            return status;
        }
        /// <summary>
        /// 遅延アップデートのキャンセルを試みます。
        /// </summary>
        /// <returns>キャンセルに成功した場合にはtrue、キャンセルする対象が存在しなかった場合にはfalse</returns>
        public Boolean TryCancelDeferredUpdate()
        {
            // まず送信待ちをみる
            Deferred.DeferredState<Boolean> state = null;
            lock (PostWaitList)
            {
                if (PostWaitList.Count > 0)
                {
                    state = PostWaitList[0];
                }
            }
            
            // 完了コールバック中でPostWaitListを触っているので外側でキャンセルする
            if (state != null && state.Cancel())
            {
                // キャンセル出来た
                return true;
            }
            
            return false;
        }
        #endregion

        void MessageReceived_WHOIS(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "WHOIS", true) != 0) return;

            // nick check
            if (String.IsNullOrEmpty(e.Message.CommandParams[0]))
            {
                SendErrorReply(ErrorReply.ERR_NONICKNAMEGIVEN, "No nickname given");
                return;
            }

            User user = null;
            try
            {
                user = _twitter.GetUser(e.Message.CommandParams[0]);
                if (user == null)
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHNICK, "No such nick/channel");
                }
            }
            catch (WebException we)
            {
                SendTwitterGatewayServerMessage("エラー: " + we.Message);
            }
            catch (TwitterServiceException tse)
            {
                SendTwitterGatewayServerMessage("エラー: " + tse.Message);
            }

            if (user == null)
                return;

            // ステータスをWHOIS replyとして返す
            SendNumericReply(NumericReply.RPL_WHOISUSER, user.ScreenName, user.Id.ToString(), "localhost", "*", user.Name + " - " + user.Description);
            SendNumericReply(NumericReply.RPL_WHOISSERVER, user.ScreenName, "WebSite", user.Url);
            if (user.Status != null)
            {
                SendNumericReply(NumericReply.RPL_AWAY, user.ScreenName, user.Status.Text.Replace('\n', ' '));
                SendNumericReply(NumericReply.RPL_WHOISIDLE
                    , user.ScreenName
                    , ((TimeSpan)(DateTime.Now - user.Status.CreatedAt)).TotalSeconds.ToString()
                    , "seconds idle");
            }
            SendNumericReply(NumericReply.RPL_ENDOFWHOIS, "End of /WHOIS list");
        }

        void MessageReceived_TOPIC(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "TOPIC", true) != 0) return;
            TopicMessage topicMsg = e.Message as TopicMessage;

            // client -> server (TOPIC #Channel :Topic Msg) && channel name != server primary channel(ex.#Twitter)
            if (!String.IsNullOrEmpty(topicMsg.Topic) && (String.Compare(topicMsg.Channel, _config.ChannelName, true) != 0))
            {
                // Set channel topic
                Group group = GetGroupByChannelName(topicMsg.Channel);
                group.Topic = topicMsg.Topic;
                SaveGroups();
                
                // server -> client (set client topic)
                Send(new TopicMessage(topicMsg.Channel, topicMsg.Topic){
                    SenderNick = CurrentNick
                });
            }
        }
        
        void MessageReceived_MODE(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "MODE", true) != 0) return;
            ModeMessage modeMsg = e.Message as ModeMessage;

            // チャンネルターゲットかつタイムラインチャンネル以外のみ
            if (modeMsg.Target.StartsWith("#") && (String.Compare(modeMsg.Target, _config.ChannelName, true) != 0))
            {
                String channel = modeMsg.Target;
                String modeArgs = modeMsg.ModeArgs;
                Group group;
                if (!_groups.TryGetValue(channel, out group))
                {
                    SendErrorReply(ErrorReply.ERR_NOSUCHCHANNEL, "No such channel");
                    return;
                }
                
                foreach (ChannelMode mode in ChannelMode.Parse(modeArgs))
                {
                    foreach (ChannelMode mode2 in new List<ChannelMode>(group.ChannelModes))
                    {
                        if (mode2.Mode == mode.Mode && mode2.Parameter == mode.Parameter)
                        {
                            if (mode.IsRemove)
                            {
                                // すでにあって削除
                                group.ChannelModes.Remove(mode2);
                            }
                            else
                            {
                                // すでにある
                                goto NEXT;
                            }
                        }
                    }
                    
                    if (!mode.IsRemove)
                    {
                        group.ChannelModes.Add(mode);
                    }
                    SendServer(new ModeMessage(channel, mode.ToString()));
                    SaveGroups();
                NEXT:
                    ;
                }
            }
        }

        void MessageReceived_PING(object sender, MessageReceivedEventArgs e)
        {
            if (String.Compare(e.Message.Command, "PING", true) != 0) return;
            Send(IRCMessage.CreateMessage("PONG :" + e.Message.CommandParam));
        }
        #endregion

        #region Twitter Service イベント
        void twitter_CheckError(object sender, ErrorEventArgs e)
        {
            // OAuth の 401 Unauthorized にはなぜかエラーが入ってるのでそれを出す
            if (TwitterService != null && TwitterService.OAuthClient != null)
            {
                SendServerErrorMessage(TwitterOAuth.GetMessageFromException(e.Exception));
            }
            else
            {
                // Default
                SendServerErrorMessage(e.Exception.Message);
            }
        }

        void twitter_DirectMessageReceived(object sender, DirectMessageEventArgs e)
        {
            // 初回は無視する
            if (e.IsFirstTime)
                return;
            
            DirectMessage message = e.DirectMessage;
            String text = (_config.ResolveTinyUrl) ? Utility.ResolveTinyUrlInMessage(message.Text) : message.Text;
            String[] lines = text.Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (String line in lines)
            {
                PrivMsgMessage privMsg = new PrivMsgMessage();
                privMsg.SenderNick = message.SenderScreenName;
                privMsg.SenderHost = "twitter@" + Server.ServerName;
                privMsg.Receiver = CurrentNick;
                //privMsg.Content = String.Format("{0}: {1}", screenName, text);
                privMsg.Content = line;
                Send(privMsg);
            }
        }

        void twitter_TimelineStatusesReceived(object sender, StatusesUpdatedEventArgs e)
        {
            SendPing();

            TimelineStatusesEventArgs eventArgs = new TimelineStatusesEventArgs(e.Statuses, _isFirstTime);
            if (!FireEvent(PreProcessTimelineStatuses, eventArgs)) return;

            // 初回だけは先にチェックしておかないとnamesが後から来てジャマ
            if (_isFirstTime && !_config.DisableUserList)
            {
                CheckFriends();
            }
            
            Boolean friendsCheckRequired = e.FriendsCheckRequired;
            foreach (Status status in e.Statuses.Status)
            {
                ProcessTimelineStatus(status, ref friendsCheckRequired);
            }
            
            // Friendsをチェックするのは成功して、チェックが必要となったとき
            if (e.FriendsCheckRequired && !_config.DisableUserList)
            {
                CheckFriends();
            }

            if (!FireEvent(PostProcessTimelineStatuses, eventArgs)) return;
            
            _isFirstTime = false;
        }

        void twitter_RepliesReceived(object sender, StatusesUpdatedEventArgs e)
        {
            Boolean dummy = false;
            foreach (Status status in e.Statuses.Status)
            {
                ProcessTimelineStatus(status, ref dummy, false, e.IsFirstTime);
            }
        }
        #endregion

        #region Compatiblity
        /// <summary>
        /// TwitterIrcGatewayからのメッセージを送信します
        /// </summary>
        /// <param name="message"></param>
        public void SendTwitterGatewayServerMessage(String message)
        {
            SendGatewayServerMessage(message);
        }
        #endregion

        #region チャンネルメッセージ
        /// <summary>
        /// ユーザ自身の更新メッセージなどをチャンネルに送信、必要なチャンネルにエコーバックします。
        /// </summary>
        /// <param name="content">送信するメッセージ</param>
        public void SendChannelMessage(String content)
        {
            SendChannelMessage(String.Empty, Connections[0].UserInfo.ClientHost, content, true, true, true, false);
        }

        /// <summary>
        /// ユーザ自身の更新メッセージなどをチャンネルに送信、必要なチャンネルにエコーバックします。
        /// </summary>
        /// <param name="receivedChannel">入力のあったチャンネル</param>
        /// <param name="content">送信するメッセージ</param>
        public void SendChannelMessage(String receivedChannel, String content)
        {
            SendChannelMessage(receivedChannel, Connections[0].UserInfo.ClientHost, content, true, true, true, false);
        }
 
        /// <summary>
        /// ユーザ自身の更新メッセージなどをチャンネルに送信、必要なチャンネルにエコーバックします。
        /// </summary>
        /// <param name="receivedChannel">入力のあったチャンネル</param>
        /// <param name="sender">送信元</param>
        /// <param name="content">送信するメッセージ</param>
        public void SendChannelMessage(String receivedChannel, String sender, String content)
        {
            SendChannelMessage(receivedChannel, sender, content, true, true, true, false);
        } 
        /// <summary>
        /// メッセージをクライアントに送信、必要の応じてトピックに設定し、必要なチャンネルにエコーバックします。
        /// </summary>
        /// <param name="receivedChannel">入力があったチャンネル</param>
        /// <param name="content">送信するメッセージ</param>
        /// <param name="sender">送信元</param>
        /// <param name="sendToTargetChannel">対象のチャンネルに送信するかどうかを指定します</param>
        /// <param name="withEchoBack">ほかのチャンネルにエコーバックするかどうかを指定します</param>
        /// <param name="setTopic">トピックに設定するかどうかを指定します</param>
        /// <param name="forceNotice">送信メッセージを設定にかかわらずNOTICEにするかどうかを指定します</param>
        public void SendChannelMessage(String receivedChannel, String sender, String content, Boolean sendToTargetChannel, Boolean withEchoBack, Boolean setTopic, Boolean forceNotice)
        {
            // 改行は削除しておく
            content = content.Replace("\n", "").Replace("\r", "");

            // topicに設定する
            if (_config.SetTopicOnStatusChanged && setTopic)
            {
                TopicMessage topicMsg = new TopicMessage(_config.ChannelName, content);
                topicMsg.Sender = sender;
                Send(topicMsg);
            }
            
            // 指定されたチャンネルに流す必要があればまず流す
            if (sendToTargetChannel && !String.IsNullOrEmpty(receivedChannel))
            {
                if (_config.BroadcastUpdateMessageIsNotice || forceNotice)
                {
                    Send(new NoticeMessage()
                    {
                        Sender = sender,
                        Receiver = receivedChannel,
                        Content = content
                    });
                }
                else
                {
                    Send(new PrivMsgMessage()
                    {
                        Sender = sender,
                        Receiver = receivedChannel,
                        Content = content
                    });
                }
            }

            // 他のチャンネルにも投げる
            if (_config.BroadcastUpdate && withEchoBack)
            {
                // #Twitter
                if (String.Compare(receivedChannel, _config.ChannelName, true) != 0)
                {
                    // XXX: 例によってIRCライブラリのバージョンアップでどうにかしたい
                    if (_config.BroadcastUpdateMessageIsNotice || forceNotice)
                    {
                        Send(new NoticeMessage()
                        {
                            Sender = sender,
                            Receiver = _config.ChannelName,
                            Content = content
                        });
                    }
                    else
                    {
                        Send(new PrivMsgMessage()
                        {
                            Sender = sender,
                            Receiver = _config.ChannelName,
                            Content = content
                        });
                    }
                }

                // group
                foreach (Group group in _groups.Values)
                {
                    if (group.IsJoined && !group.IsSpecial && !group.IgnoreEchoBack && String.Compare(receivedChannel, group.Name, true) != 0)
                    {
                        if (_config.BroadcastUpdateMessageIsNotice || forceNotice)
                        {
                            Send(new NoticeMessage()
                            {
                                Sender = sender,
                                Receiver = group.Name,
                                Content = content
                            });
                        }
                        else
                        {
                            Send(new PrivMsgMessage()
                            {
                                Sender = sender,
                                Receiver = group.Name,
                                Content = content
                            });
                        }
                    }
                }
            }
            // 全チャンネルエコーバックが有効でなく、チャンネルが指定されていない場合にはデフォルトに流す
            else if (String.IsNullOrEmpty(receivedChannel))
            {
                if (forceNotice)
                {
                    Send(new NoticeMessage()
                    {
                        Sender = sender,
                        Receiver = _config.ChannelName,
                        Content = content
                    });
                }
                else
                {
                    Send(new PrivMsgMessage()
                    {
                        Sender = sender,
                        Receiver = _config.ChannelName,
                        Content = content
                    });
                }
            }

        }
        #endregion
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="group"></param>
        public void JoinChannel(IIrcMessageSendable messageSendable, Group group)
        {
            JoinMessage joinMsg = new JoinMessage(group.Name, "");
            messageSendable.SendServer(joinMsg);

            messageSendable.SendNumericReply(NumericReply.RPL_NAMREPLY, "=", group.Name, String.Format("@{0} ", CurrentNick) + String.Join(" ", group.Members.ToArray()));
            messageSendable.SendNumericReply(NumericReply.RPL_ENDOFNAMES, group.Name, "End of NAMES list");
            group.IsJoined = true;

            // mode
            foreach (ChannelMode mode in group.ChannelModes)
            {
                messageSendable.Send(new ModeMessage(group.Name, mode.ToString()));
            }

            // Set topic of client, if topic was set
            if (!String.IsNullOrEmpty(group.Topic))
            {
                messageSendable.Send(new TopicMessage(group.Name, group.Topic));
            }
            else
            {
                messageSendable.SendNumericReply(NumericReply.RPL_NOTOPIC, group.Name, "No topic is set");
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        private void GetFriendNames()
        {
            RunCheck(delegate
            {
                User[] friends = _twitter.GetFriends(10);
                _followingUsers = new HashSet<User>();
                // 保持していてもしょうがないので消す
                foreach (var friend in friends)
                    friend.Status = null;
                _followingUsers.UnionWith(friends);

                ShowChannelUsers(this);
            });
        }
        
        /// <summary>
        /// チャンネルにユーザリストを送信します。
        /// </summary>
        /// <param name="messageSendable"></param>
        private void ShowChannelUsers(IIrcMessageSendable messageSendable)
        {
            List<String> users = _followingUsers.Select(u => u.ScreenName).ToList();
            for (var i = 0; i < ((users.Count / 100) + 1); i++)
            {
                Int32 count = Math.Min(users.Count - (i * 100), 100);
                messageSendable.SendNumericReply(NumericReply.RPL_NAMREPLY, "=", _config.ChannelName, String.Format("{0} ", CurrentNick) + String.Join(" ", users.GetRange(i * 100, count).ToArray()));
            }
            
            messageSendable.SendNumericReply(NumericReply.RPL_ENDOFNAMES, _config.ChannelName, "End of NAMES list");
        }

        /// <summary>
        /// 
        /// </summary>
        private void SendPing()
        {
            Send(new OtherMessage(String.Format("PING :{0}", Server.ServerName)));
        }

        /// <summary>
        /// 
        /// </summary>
        private void CheckFriends()
        {
            if (_followingUsers.Count == 0)
            {
                GetFriendNames();
                return;
            }

            RunCheck(delegate
            {
                User[] friends = _twitter.GetFriends(10);

                // てきとうに。
                // 増えた分
                foreach (User user in friends)
                {
                    if (!_followingUsers.Contains(user))
                    {
                        JoinMessage joinMsg = new JoinMessage(_config.ChannelName, "");
                        joinMsg.SenderNick = user.ScreenName;
                        joinMsg.SenderHost = String.Format("{0}@{1}", "twitter", Server.ServerName);
                        Send(joinMsg);
                    }
                }
                // 減った分
                foreach (User user in _followingUsers)
                {
                    if (!friends.Contains(user))
                    {
                        PartMessage partMsg = new PartMessage(_config.ChannelName, "");
                        partMsg.SenderNick = user.ScreenName;
                        partMsg.SenderHost = String.Format("{0}@{1}", "twitter", Server.ServerName);
                        Send(partMsg);
                    }
                }

                _followingUsers.IntersectWith(friends);

            });
        }

        /// <summary>
        /// タイムラインのステータスを処理して、クライアントに送信します
        /// </summary>
        /// <param name="status"></param>
        /// <param name="friendsCheckRequired"></param>
        public void ProcessTimelineStatus (Status status, ref Boolean friendsCheckRequired)
        {
            ProcessTimelineStatus(status, ref friendsCheckRequired, false);
        }
        public void ProcessTimelineStatus(Status status, ref Boolean friendsCheckRequired, Boolean ignoreGatewayCheck)
        {
            ProcessTimelineStatus(status, ref friendsCheckRequired, ignoreGatewayCheck, _isFirstTime);
        }
        public void ProcessTimelineStatus(Status status, ref Boolean friendsCheckRequired, Boolean ignoreGatewayCheck, Boolean isFirstTime)
        {
            TimelineStatusEventArgs eventArgs = new TimelineStatusEventArgs(status, status.Text, "PRIVMSG");
            if (!FireEvent(PreProcessTimelineStatus, eventArgs)) return;
            
            // チェック
            // 自分がゲートウェイを通して発言したものは捨てる
            if (!ignoreGatewayCheck && (status.User == null || String.IsNullOrEmpty(status.User.ScreenName) || _lastStatusIdsFromGateway.Contains(status.Id)))
            {
                return;
            }
            
            // @だけのReplyにIDをつけるモードがオンの時はStatusのIDを記録する
            if (_config.EnableOldStyleReply)
            {
                _lastStatusIdsByScreenName[status.User.ScreenName] = status.Id;
            }

            // friends チェックが必要かどうかを確かめる
            // まだないときは取ってくるフラグを立てる
            friendsCheckRequired |= !(_followingUsers.Contains(status.User));
            
            // フィルタ
            if (!FireEvent(PreFilterProcessTimelineStatus, eventArgs)) return;
            FilterArgs filterArgs = new FilterArgs(this, eventArgs.Text, status.User, eventArgs.IRCMessageType, false, status);
            if (!_filter.ExecuteFilters(filterArgs))
            {
                // 捨てる
                return;
            }
            eventArgs.IRCMessageType = filterArgs.IRCMessageType;
            eventArgs.Text = filterArgs.Content;

            if (!FireEvent(PostFilterProcessTimelineStatus, eventArgs)) return;
            if (!FireEvent(PreSendMessageTimelineStatus, eventArgs)) return;
            
            // 送信先を決定する
            List<RoutedGroup> routedGroups = RoutingStatusMessage(status, eventArgs.Text, eventArgs.IRCMessageType);
            // メインチャンネルを送信先として追加する
            routedGroups.Add(new RoutedGroup()
                             {
                                 Group                        = new Group(_config.ChannelName),
                                 IRCMessageType               = eventArgs.IRCMessageType,
                                 IsExistsInChannelOrNoMembers = true,
                                 IsMessageFromSelf            = false,
                                 Text                         = eventArgs.Text
                             });
            if (!FireEvent(MessageRoutedTimelineStatus, new TimelineStatusRoutedEventArgs(status, eventArgs.Text, routedGroups))) return;
            
            // 送信する
            foreach (RoutedGroup routedGroup in routedGroups)
            {
                TimelineStatusGroupEventArgs eventArgsGroup = new TimelineStatusGroupEventArgs(status, routedGroup.Text, routedGroup.IRCMessageType, routedGroup.Group);
                if (!FireEvent(PreSendGroupMessageTimelineStatus, eventArgsGroup)) return;

                String[] lines = eventArgsGroup.Text.Split(new Char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (isFirstTime && !_config.DisableNoticeAtFirstTime)
                    {
                        // 初回のときはNOTICE+時間
                        Send(CreateIRCMessageFromStatusAndType(status, "NOTICE", routedGroup.Group.Name,
                                                               String.Format("{0}: {1}",
                                                                             status.CreatedAt.ToString("HH:mm"), line)));
                    }
                    else
                    {
                        Send(CreateIRCMessageFromStatusAndType(status, eventArgsGroup.IRCMessageType,
                                                               routedGroup.Group.Name, line));
                    }
                }
                
                if (!FireEvent(PostSendGroupMessageTimelineStatus, eventArgsGroup)) return;
            }

            if (!FireEvent(PostSendMessageTimelineStatus, eventArgs)) return;
            if (!FireEvent(PostProcessTimelineStatus, eventArgs)) return;
        }

        /// <summary>
        /// メッセージを送信する先を決定します
        /// </summary>
        /// <param name="status"></param>
        /// <param name="text"></param>
        /// <param name="ircMessageType"></param>
        /// <returns></returns>
        public List<RoutedGroup> RoutingStatusMessage(Status status, String text, String ircMessageType)
        {
            List<RoutedGroup> routedGroups = new List<RoutedGroup>();
            
            foreach (Group group in _groups.Values)
            {
                if (!group.IsJoined || !group.IsRoutable)
                    continue;

                Boolean isOrMatch = group.IsOrMatch;
                Boolean isMatched = String.IsNullOrEmpty(group.Topic) ? true : Regex.IsMatch(text, (isOrMatch ? group.Topic.Substring(1) : group.Topic));
                Boolean isExistsInChannelOrNoMembers = (group.Exists(status.User.ScreenName) || group.Members.Count == 0);
                Boolean isMessageFromSelf = (_twitterUser != null) ? (status.User.Id == _twitterUser.Id && !group.IgnoreEchoBack) : false;

                // 0: self && !IgnoreEchoback
                // 1: member exists in channel && match regex
                // 2: no members in channel(self only) && match regex
                // 3: member exists in channel || match regex (StartsWith: "|")
                // 4: no members in channel(self only) || match regex (StartsWith: "|")
                if (isMessageFromSelf || (group.IsOrMatch ? (isExistsInChannelOrNoMembers || isMatched) : (isExistsInChannelOrNoMembers && isMatched)))
                {
                    routedGroups.Add(new RoutedGroup()
                                     {
                                         Group = group,
                                         IsExistsInChannelOrNoMembers = isExistsInChannelOrNoMembers,
                                         IsMessageFromSelf = isMessageFromSelf,
                                         // 自分からのメッセージでBroadcastUpdateMessageIsNoticeがTrueのときはNOTICE
                                         IRCMessageType = (isMessageFromSelf && _config.BroadcastUpdateMessageIsNotice) ? "NOTICE" : ircMessageType,
                                         Text = text
                                     });
                }
            }
            return routedGroups;
        }

        // XXX: IRCクライアントライブラリのアップデートで対応できるけどとりあえず...
        private IRCMessage CreateIRCMessageFromStatusAndType(Status status, String type, String receiver, String line)
        {
            IRCMessage msg;
            switch (type.ToUpperInvariant())
            {
                case "NOTICE":
                    msg = new NoticeMessage(receiver, line);
                    break;
                case "PRIVMSG":
                default:
                    msg = new PrivMsgMessage(receiver, line);
                    break;
            } 
            msg.SenderNick = status.User.ScreenName;
            msg.SenderHost = "twitter@" + Server.ServerName;

            return msg;
        }

        /// <summary>
        /// チェックを実行します。例外が発生した場合には自動的にメッセージを送信します。
        /// </summary>
        /// <param name="proc">実行するチェック処理</param>
        /// <returns></returns>
        public Boolean RunCheck(Procedure proc)
        {
            try
            {
                proc();
            }
            catch (WebException ex)
            {
                if (ex.Response == null || !(ex.Response is HttpWebResponse) || ((HttpWebResponse)(ex.Response)).StatusCode != HttpStatusCode.NotModified)
                {
                    // not-modified 以外
                    twitter_CheckError(_twitter, new ErrorEventArgs(ex));
                    return false;
                }
            }
            catch (TwitterServiceException ex2)
            {
                twitter_CheckError(_twitter, new ErrorEventArgs(ex2));
                return false;
            }
            return true;
        }

        /// <summary>
        /// チェックを実行します。Twitterに由来する例外が発生した場合には指定したデリゲートを呼び出します。
        /// </summary>
        /// <param name="proc">実行するチェック処理</param>
        /// <returns></returns>
        public Boolean RunCheck(Procedure proc, Action<Exception> twitterExceptionCallback)
        {
            try
            {
                proc();
            }
            catch (WebException ex)
            {
                if (ex.Response == null || !(ex.Response is HttpWebResponse) || ((HttpWebResponse)(ex.Response)).StatusCode != HttpStatusCode.NotModified)
                {
                    // not-modified 以外
                    twitterExceptionCallback(ex);
                    return false;
                }
            }
            catch (TwitterServiceException ex2)
            {
                twitterExceptionCallback(ex2);
                return false;
            }
            return true;
        }
        
        /// <summary>
        /// 
        /// </summary>
        protected override void OnClosing()
        {
            OnSessionEnded();
            Dispose();

            base.OnClosing();
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
            return String.Format("Session: User={0}, Connections=[{1}]", _twitterUser.ScreenName, String.Join(", ", (from conn in Connections select conn.UserInfo.EndPoint.ToString()).ToArray()));
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
                        Logger.Error(ex.ToString());
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
                try
                {
                    if (AddInManager != null)
                        AddInManager.Uninitialize();
                }
                catch {}

                if (_config.EnableTrace)
                {
#if FALSE
                    Trace.Listeners.Remove(_traceListener);
#endif
                }

                if (_twitter != null)
                {
                    _twitter.Dispose();
                    _twitter = null;
                }
                GC.SuppressFinalize(this);
                _isDisposed = true;
            }
        }

        #endregion

        protected override void OnAttached(ConnectionBase connection)
        {
            lock (this)
            {
                // セッションを初期化
                if (!IsStarted)
                    InitializeSession();
                
                // メインチャンネルにJOIN
                SendServer(new JoinMessage(_config.ChannelName, ""));
                // ユーザ一覧
                if (_followingUsers.Count > 0)
                {
                    ShowChannelUsers(connection);
                }
                // グループにJOIN
                foreach (Group group in Groups.Values)
                {
                    if (group.IsJoined)
                    {
                        JoinChannel(connection, group);
                    }
                }

                // 開始
                if (!IsStarted)
                    Start();
            }
        }

        protected override void OnDetached(ConnectionBase connection)
        {
        }

        protected override void OnMessageReceivedFromClient(MessageReceivedEventArgs e)
        {
            // 転送する
            OnMessageReceived(e.Message, e);
        }
    }

    public delegate void Procedure();
}
