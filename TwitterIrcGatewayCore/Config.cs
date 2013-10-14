using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Misuzilla.Applications.TwitterIrcGateway.AddIns;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// TwitterIrcGateway 本体の設定情報を表します。
    /// </summary>
    public class Config : MarshalByRefObject, IConfiguration
    {
        private Int32 _interval;

        [Browsable(false)]
        public String IMServiceServerName { get; set; }
        [Browsable(false)]
        public String IMServerName { get; set; }
        [Browsable(false)]
        public String IMUserName { get; set; }
        [Browsable(false)]
        public String IMEncryptoPassword { get; set; }
        [Browsable(false)]
        public List<String> DisabledAddInsList { get; set; }
        [Browsable(false)]
        public String OAuthUserPasswordHash { get; set; }
        [Browsable(false)]
        public String OAuthAccessToken { get; set; }
        [Browsable(false)]
        public String OAuthTokenSecret { get; set; }

        [Description("TypableMapを有効化または無効化します")]
        public Boolean EnableTypableMap { get; set; }
        [Description("TypableMapの色番号を変更します")]
        public Int32 TypableMapKeyColorNumber { get; set; }
        [Description("TypableMapのキーサイズを変更します")]
        public Int32 TypableMapKeySize { get; set; }

        [Description("冗長な末尾削除を有効化または無効化します")]
        public Boolean EnableRemoveRedundantSuffix { get; set; }
        [Description("@で返信した際に最後に受信したステータスに対して返すかどうかを指定します")]
        public Boolean EnableOldStyleReply { get; set; }

        [Description("TwitterIrcGateway内部に保持するStatusの数を指定します。")]
        public Int32 BufferSize { get; set; }

        /// <summary>
        /// チェックする間隔を指定します。
        /// </summary>
        [Description("チェックする間隔を指定します。")]
        public Int32 Interval { get; set; }
        /// <summary>
        /// ダイレクトメッセージをチェックする間隔を指定します。
        /// </summary>
        [Description("ダイレクトメッセージをチェックする間隔を指定します。")]
        public Int32 IntervalDirectMessage { get; set; }
        /// <summary>
        /// Repliesをチェックするかどうかを指定します。
        /// </summary>
        [Description("Repliesをチェックするかどうかを指定します。")]
        public Boolean EnableRepliesCheck { get; set; }
        /// <summary>
        /// Repliesチェックする間隔を指定します。
        /// </summary>
        [Description("Repliesチェックする間隔を指定します。")]
        public Int32 IntervalReplies { get; set; }
        /// <summary>
        /// エラーを無視するかどうかを指定します。
        /// </summary>
        [Description("エラーを無視するかどうかを指定します。")]
        public Boolean IgnoreWatchError { get; set; }
        /// <summary>
        /// TinyURLを展開するかどうかを指定します。
        /// </summary>
        [Description("TinyURLを展開するかどうかを指定します。")]
        public Boolean ResolveTinyUrl { get; set; }
        /// <summary>
        /// 取りこぼし防止を利用するかどうかを指定します。
        /// </summary>
        [Description("取りこぼし防止を利用するかどうかを指定します。")]
        public Boolean EnableDropProtection { get; set; }
        /// <summary>
        /// ステータスを更新したときにトピックを変更するかどうかを指定します。
        /// </summary>
        [Description("ステータスを更新したときにトピックを変更するかどうかを指定します。")]
        public Boolean SetTopicOnStatusChanged { get; set; }
        /// <summary>
        /// トレースを有効にするかどうかを指定します。
        /// </summary>
        [Description("トレースを有効にするかどうかを指定します。")]
        public Boolean EnableTrace { get; set; }
        /// <summary>
        /// Twitterのステータスが流れるチャンネル名を指定します。
        /// </summary>
        [Description("Twitterのステータスが流れるチャンネル名を指定します。")]
        public String ChannelName { get; set; }
        /// <summary>
        /// ユーザ一覧を取得するかどうかを指定します。
        /// </summary>
        [Description("ユーザ一覧を取得するかどうかを指定します。")]
        public Boolean DisableUserList { get; set; }
        /// <summary>
        /// アップデートをすべてのチャンネルに投げるかどうかを指定します。
        /// </summary>
        [Description("アップデートをすべてのチャンネルに投げるかどうかを指定します。")]
        public Boolean BroadcastUpdate { get; set; }
        /// <summary>
        /// クライアントにメッセージを送信するときのウェイトを指定します。
        /// </summary>
        [Description("クライアントにメッセージを送信するときのウェイトを指定します。")]
        public Int32 ClientMessageWait { get; set; }
        /// <summary>
        /// アップデートをすべてのチャンネルに投げるときNOTICEにするかどうかを指定します。
        /// </summary>
        [Description("アップデートをすべてのチャンネルに投げるときNOTICEにするかどうかを指定します。")]
        public Boolean BroadcastUpdateMessageIsNotice { get; set; }
        /// <summary>
        /// データの取得にPOSTメソッドを利用するかどうかを指定します。
        /// </summary>
        [Browsable(false)]
        [Description("データの取得にPOSTメソッドを利用するかどうかを指定します。")]
        public Boolean POSTFetchMode { get; set; }
        /// <summary>
        /// タイムラインの一回の取得につき何件取得するかを指定します。
        /// </summary>
        [Description("タイムラインの一回の取得につき何件取得するかを指定します。")]
        public Int32 FetchCount { get; set; }
        /// <summary>
        /// Twitterにアクセスする際にgzip圧縮を有効にするかどうかを指定します。
        /// </summary>
        [Description("Twitterにアクセスする際にgzip圧縮を有効にするかどうかを指定します。")]
        [Browsable(false)] // TODO: ホスティングじゃない場合にはBrowsableをはずす
        public Boolean EnableCompression { get; set; }
        /// <summary>
        /// 初回取得時のタイムラインをNOTICEで送信するかどうかを指定します。
        /// </summary>
        [Description("初回取得時のタイムラインをNOTICEで送信するかどうかを指定します。")]
        public Boolean DisableNoticeAtFirstTime { get; set; }
        /// <summary>
        /// フォローしているユーザ一覧を取得する際、次のページが存在するか判断する閾値を指定します。
        /// </summary>
        [Description("フォローしているユーザ一覧を取得する際、次のページが存在するか判断する閾値を指定します。")]
        public Int32 FriendsPerPageThreshold { get; set; }
        /// <summary>
        /// 更新時に何秒待機したのちリクエストを送信するかどうかを指定します。
        /// </summary>
        [Description("更新時に何秒待機したのちリクエストを送信するかどうかを指定します。")]
        public Int32 UpdateDelayTime { get; set; }

        /// <summary>
        /// デフォルトの設定
        /// </summary>
        public static Config Default = new Config();
        
        public Config()
        {
            ChannelName = "#Twitter";
            EnableTypableMap = false;
            TypableMapKeyColorNumber = 14;
            TypableMapKeySize = 2;
            EnableRemoveRedundantSuffix = false;
            DisabledAddInsList = new List<string>();
            EnableOldStyleReply = false;
            FetchCount = 50;
            BufferSize = 250;
            EnableCompression = false;
            Interval = 60;
            IntervalDirectMessage = 360;
            IntervalReplies = 120;
            DisableNoticeAtFirstTime = false;
            FriendsPerPageThreshold = 100;
            UpdateDelayTime = 5;

            if (Default != null)
            {
                ChannelName = Default.ChannelName;
                Interval = Default.Interval;
                IntervalDirectMessage = Default.IntervalDirectMessage;
                EnableRepliesCheck = Default.EnableRepliesCheck;
                IntervalReplies = Default.IntervalReplies;
                IgnoreWatchError = Default.IgnoreWatchError;
                ResolveTinyUrl = Default.ResolveTinyUrl;
                EnableDropProtection = Default.EnableDropProtection;
                SetTopicOnStatusChanged = Default.SetTopicOnStatusChanged;
                EnableTrace = Default.EnableTrace;
                ChannelName = Default.ChannelName;
                DisableUserList = Default.DisableUserList;
                BroadcastUpdate = Default.BroadcastUpdate;
                ClientMessageWait = Default.ClientMessageWait;
                BroadcastUpdateMessageIsNotice = Default.BroadcastUpdateMessageIsNotice;
                POSTFetchMode = Default.POSTFetchMode;
                EnableCompression = Default.EnableCompression;
                DisableNoticeAtFirstTime = Default.DisableNoticeAtFirstTime;
                FriendsPerPageThreshold = Default.FriendsPerPageThreshold;
                UpdateDelayTime = Default.UpdateDelayTime;
            }
        }

        #region XML Serialize
        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Config()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Config));
                }
            }
        }
        public static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public void Serialize(Stream stream)
        {
            using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stream, Encoding.UTF8))
            {
                _serializer.Serialize(xmlTextWriter, this);
            }
        }

        public static Config Deserialize(Stream stream)
        {
            return _serializer.Deserialize(stream) as Config;
        }
        #endregion

        public String GetIMPassword(String key)
        {
            StringBuilder sb = new StringBuilder();
            String passwordDecoded = Encoding.UTF8.GetString(Convert.FromBase64String(IMEncryptoPassword));
            for (var i = 0; i < passwordDecoded.Length; i++)
            {
                sb.Append((Char)(passwordDecoded[i] ^ key[i % key.Length]));
            }
            return sb.ToString();
        }

        public void SetIMPassword(String key, String password)
        {
            StringBuilder sb = new StringBuilder();
            for (var i = 0; i < password.Length; i++)
            {
                sb.Append((Char)(password[i] ^ key[i % key.Length]));
            }
            IMEncryptoPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Config Load(String path)
        {
            // group 読み取り
            if (File.Exists(path))
            {
                TraceLogger.Server.Information(String.Format("Load Config: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Config config = Config.Deserialize(fs);
                            if (config != null)
                                return config;
                        }
                        catch (XmlException xe) { TraceLogger.Server.Information(xe.Message); }
                        catch (InvalidOperationException ioe) { TraceLogger.Server.Information(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    TraceLogger.Server.Information(ie.Message);
                    throw;
                }
            }
            return new Config();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Save(String path)
        {
            TraceLogger.Server.Information(String.Format("Save Config: {0}", path));
            try
            {
                String dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    try
                    {
                        this.Serialize(fs);
                    }
                    catch (XmlException xe) { TraceLogger.Server.Information(xe.Message); }
                    catch (InvalidOperationException ioe) { TraceLogger.Server.Information(ioe.Message); }
                }
            }
            catch (IOException ie)
            {
                TraceLogger.Server.Information(ie.Message);
                throw;
            }
        }

        #region Configuration
        private readonly static String ConfigBasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "Configs");

        public static String GetConfigPath(String userId, String fileName)
        {
            return Path.Combine(Path.Combine(ConfigBasePath, userId), fileName);
        }

        /// <summary>
        /// 
        /// </summary>
        public static Config LoadConfig(String userId)
        {
            return Config.Load(GetConfigPath(userId, "Config.xml"));
        }

        /// <summary>
        /// 
        /// </summary>
        public static void SaveConfig(String userId, Config config)
        {
            config.Save(GetConfigPath(userId, "Config.xml"));
        }
        #endregion
    }
}
