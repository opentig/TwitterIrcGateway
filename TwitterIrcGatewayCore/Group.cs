using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    /// <summary>
    /// Groupをチャンネル名で格納します。
    /// </summary>
    public class Groups : SortedList<string, Group>
    {
        public Groups()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Groups()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Group[]));
                }
            }
        }
        private static XmlSerializer Serializer
        {
            get
            {
                return _serializer;
            }
        }

        public void Serialize(Stream stream)
        {
            Group[] groups = new Group[this.Values.Count];
            this.Values.CopyTo(groups, 0);
            using (XmlTextWriter xmlTextWriter = new XmlTextWriter(stream, Encoding.UTF8))
            {
                _serializer.Serialize(xmlTextWriter, groups);
            }
        }

        public static Groups Deserialize(Stream stream)
        {
            Group[] groups = _serializer.Deserialize(stream) as Group[];
            Groups retGroups = new Groups();
            foreach (Group group in groups)
            {
                retGroups[group.Name] = group;
                //group.IsJoined = false;
                group.ChannelModes = group.ChannelModes == null ? new List<ChannelMode>() : group.ChannelModes;
            }

            return retGroups;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Groups Load(String path)
        {
            // group 読み取り
            if (File.Exists(path))
            {
                TraceLogger.Server.Information(String.Format("Load Group: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Groups groups = Groups.Deserialize(fs);
                            if (groups != null)
                                return groups;
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
            return new Groups();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Save(String path)
        {
            TraceLogger.Server.Information(String.Format("Save Group: {0}", path));
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
    }

    /// <summary>
    /// IRC上でチャンネルとして表現されるメッセージ送信対象のグループを表します。
    /// </summary>
    public class Group : IComparable
    {
        /// <summary>
        /// チャンネル名を取得・設定します
        /// </summary>
        public String Name { get; set; }
        public String Mode { get; set; }
        /// <summary>
        /// グループに属するユーザのリストを取得します
        /// </summary>
        public List<String> Members { get; set; }
        /// <summary>
        /// JOINしているかどうかを取得・設定します
        /// </summary>
        public Boolean IsJoined { get; set; }
        /// <summary>
        /// 特別扱いされるチャンネルかどうかを取得・設定します。
        /// 特別扱いされている場合、タイムラインステータスは送信されなくなります。
        /// </summary>
        public Boolean IsSpecial { get; set; }
        /// <summary>
        /// チャンネルのトピックを取得・設定します
        /// </summary>
        public String Topic { get; set; }
        /// <summary>
        /// チャンネルのモードを取得・設定します
        /// </summary>
        public List<ChannelMode> ChannelModes { get; set; }

        /// <summary>
        /// グループのインスタンスを初期化します。
        /// </summary>
        public Group()
        {
            ChannelModes = new List<ChannelMode>();
        }

        /// <summary>
        /// グループを複製します。
        /// </summary>
        /// <returns></returns>
        public Group Clone()
        {
            Group g = new Group()
            {
                Name = this.Name,
                ChannelModes = this.ChannelModes,
                IsJoined = this.IsJoined,
                IsSpecial = this.IsSpecial,
                Members = new List<string>(this.Members),
                Mode = this.Mode,
                Topic = this.Topic
            };
            return g;
        }

        /// <summary>
        /// 指定した名前でグループのインスタンスを初期化します。
        /// </summary>
        /// <param name="name">#で始まるチャンネル名</param>
        public Group(String name)
        {
            if (!name.StartsWith("#") || name.Length < 2)
            {
                throw new ArgumentException("チャンネル名は#で始まる必要があります。");
            }
            Name = name;
            Members = new List<string>();
            ChannelModes = new List<ChannelMode>();
        }

        /// <summary>
        /// 指定したユーザがグループのメンバかどうかを取得します。
        /// </summary>
        /// <param name="id">ユーザのID</param>
        /// <returns>グループのメンバに属しているかどうかを表すBoolean値。</returns>
        public Boolean Exists(String id)
        {
            Int32 pos;
            lock (Members)
            {
                pos = Members.BinarySearch(id, StringComparer.InvariantCultureIgnoreCase);
            }
            return pos > -1;
        }

        /// <summary>
        /// グループのメンバに属するユーザを追加します。
        /// </summary>
        /// <param name="id">ユーザのID</param>
        public void Add(String id)
        {
            lock (Members)
            {
                Members.Add(id);
                Members.Sort(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// 指定したユーザをグループのメンバから削除します。
        /// </summary>
        /// <param name="id">ユーザのID</param>
        public void Remove(String id)
        {
            lock (Members)
            {
                Members.Remove(id);
                Members.Sort(StringComparer.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// グループに送信されたメッセージのエコーバックを無視するかどうかを取得します。
        /// </summary>
        public Boolean IgnoreEchoBack
        {
            get
            {
                return ChannelModes.Exists(mode => mode.Mode == ChannelModeTypes.Private);
            }
        }

        /// <summary>
        /// グループのトピックを利用したマッチがメンバとOR条件になっているかどうかを取得します。
        /// </summary>
        public Boolean IsOrMatch
        {
            get
            {
                return String.IsNullOrEmpty(Topic) ? false : Topic.StartsWith("|");
            }
        }
        
        /// <summary>
        /// TwitterIrcGatewayがトピックなどを考慮してメッセージ送信先として選択できるかどうかを取得します。
        /// </summary>
        public Boolean IsRoutable
        {
            get { return !(IsSpecial || ChannelModes.Exists(mode => mode.Mode == ChannelModeTypes.InviteOnly)); }
        }

        public override string ToString()
        {
            return String.Format("Group: {0} ({1} members)", Name, Members.Count);
        }

        #region IComparable メンバ

        public int CompareTo(object obj)
        {
            if (!(obj is Group))
                return -1;

            return String.Compare((obj as Group).Name, this.Name, true, CultureInfo.InvariantCulture);
        }

        #endregion
    }

    /// <summary>
    /// 送信先を決定した後を表すクラス
    /// </summary>
    public class RoutedGroup : IComparable
    {
        /// <summary>
        /// 決定対象となったグループを取得します
        /// </summary>
        public Group Group { get; set; }
        /// <summary>
        /// 自分自身が送信したメッセージかどうかを取得します
        /// </summary>
        public Boolean IsMessageFromSelf { get; set; }
        /// <summary>
        /// チャンネルにユーザが存在しているか、0人のチャンネルだったのかを取得します
        /// </summary>
        public Boolean IsExistsInChannelOrNoMembers { get; set; }
        /// <summary>
        /// このチャンネルに送信されるIRCのメッセージの種類を取得または設定します
        /// </summary>
        public String IRCMessageType { get; set; }
        /// <summary>
        /// このチャンネルに送信されるテキストを取得または設定します
        /// </summary>
        public String Text { get; set; }
        
        public RoutedGroup()
        {
            IRCMessageType = "PRIVMSG";
        }
        
        #region IComparable メンバ
        public int CompareTo(object obj)
        {
            if (!(obj is RoutedGroup))
                return -1;

            return String.Compare((obj as RoutedGroup).Group.Name, this.Group.Name, true, CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
