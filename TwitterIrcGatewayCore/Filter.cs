using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.Filter
{
    [XmlInclude(typeof(Drop))]
    [XmlInclude(typeof(Redirect))]
    [XmlInclude(typeof(RewriteContent))]
    [XmlInclude(typeof(Process))]
    public class Filters
    {
        public Filters()
        {
            _items = new List<FilterItem>();
        }
        
        private static Object _syncObject = new object();
        private static XmlSerializer _serializer = null;
        static Filters()
        {
            lock (_syncObject)
            {
                if (_serializer == null)
                {
                    _serializer = new XmlSerializer(typeof(Filters));
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

        public void Add(FilterItem item)
        {
            _items.Add(item);
        }

        public void Remove(FilterItem item)
        {
            _items.Remove(item);
        }

        public void RemoveAt(Int32 index)
        {
            _items.RemoveAt(index);
        }
        
        private List<FilterItem> _items;
        public FilterItem[] Items
        {
            get { return _items.ToArray(); }
            set { _items.AddRange(value); }
        }

        /// <summary>
        /// メッセージをフィルタします
        /// </summary>
        /// <param name="args"></param>
        /// <returns>メッセージを捨てるかどうか</returns>
        public Boolean ExecuteFilters(FilterArgs args)
        {
            TraceLogger.Filter.Information(String.Format("Filter: User: {0} / Message: {1}",args.User.ScreenName, args.Content.Replace('\n', ' ')));
            Trace.Indent();
            try
            {
                foreach (FilterItem item in _items)
                {
                    if (!item.Enabled)
                        continue;

                    Trace.Indent();
                    try
                    {
                        Boolean executed = item.Execute(args);
                        if (args.Drop)
                        {
                            TraceLogger.Filter.Information(String.Format("=> Drop", item.GetType().Name, args.User.ScreenName,
                                                          args.Content.Replace('\n', ' ')));
                            return false;
                        }
                        else if (executed)
                        {
                            TraceLogger.Filter.Information(String.Format("=> Execute Result: Filter: {0} / User: {1} / Message: {2}",
                                                          item.GetType().Name,
                                                          args.User.ScreenName, args.Content.Replace('\n', ' ')));
                        }
                    }
                    finally
                    {
                        Trace.Unindent();
                    }
                }
            }
            finally 
            {
                Trace.Unindent();
            }

            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static Filters Load(String path)
        {
            if (File.Exists(path))
            {
                TraceLogger.Filter.Information(String.Format("Load Filters: {0}", path));
                try
                {
                    using (FileStream fs = new FileStream(path, FileMode.Open))
                    {
                        try
                        {
                            Filters filters = Filters.Serializer.Deserialize(fs) as Filters;
                            if (filters != null)
                            {
                                foreach (FilterItem item in filters.Items)
                                {
                                    TraceLogger.Filter.Information(String.Format(" - Filter:{0}", item.ToString()));
                                }
                                return filters;
                            }
                        }
                        catch (XmlException xe) { TraceLogger.Filter.Information(xe.Message); }
                        catch (InvalidOperationException ioe) { TraceLogger.Filter.Information(ioe.Message); }
                    }
                }
                catch (IOException ie)
                {
                    TraceLogger.Filter.Information(ie.Message);
                    throw;
                }
            }
            return new Filters();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="path"></param>
        public void Save(String path)
        {
            TraceLogger.Filter.Information(String.Format("Save Filter: {0}", path));
            try
            {
                String dir = Path.GetDirectoryName(path);
                Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    try
                    {
                        Filters.Serializer.Serialize(fs, this);
                    }
                    catch (XmlException xe) { TraceLogger.Filter.Information(xe.Message); }
                    catch (InvalidOperationException ioe) { TraceLogger.Filter.Information(ioe.Message); }
                }
            }
            catch (IOException ie)
            {
                TraceLogger.Filter.Information(ie.Message);
                throw;
            }
        }
    }

    public abstract class FilterItem : Misuzilla.Applications.TwitterIrcGateway.AddIns.IConfiguration
    {
        public FilterItem()
        {
        }
        
        private Boolean _enabled = true;
        [XmlAttribute]
        public Boolean Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public abstract Boolean Execute(FilterArgs args);
    }

    public class FilterArgs
    {
        public String Content;
        public User User;
        public String IRCMessageType;
        public Boolean Drop;
        public Session Session;
        public Status Status;

        public FilterArgs(Session session, String content, User user, String ircMessageType, Boolean drop, Status status)
        {
            this.Session = session;
            this.Content = content;
            this.User = user;
            this.IRCMessageType = ircMessageType;
            this.Drop = drop;
            this.Status = status;
        }
    }

    public class Drop : FilterItem
    {
        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }
        
        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        public override Boolean Execute(FilterArgs args)
        {
            if (!String.IsNullOrEmpty(_matchPattern))
            {
                return args.Drop =
                    Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern));
            }
            return false;
        }
        public override string ToString()
        {
            return "Drop:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
            ;
        }
    }

    public class RewriteContent : FilterItem
    {
        private String _replacePattern = "";
        public String ReplacePattern
        {
            get { return _replacePattern; }
            set { _replacePattern = value; }
        }

        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }

        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        private String _messageType = "PRIVMSG";
        public String MessageType
        {
            get { return _messageType; }
            set { _messageType = value; }
        }

        public Boolean IsRemoveContent
        {
            get;
            set;
        }

        public override Boolean Execute(FilterArgs args)
        {
            if (!String.IsNullOrEmpty(_matchPattern))
            {
                if (Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern)))
                {
                    if (IsRemoveContent || !String.IsNullOrEmpty(_replacePattern))
                    {
                        args.Content = Regex.Replace(args.Content, _matchPattern, (_replacePattern ?? String.Empty), RegexOptions.IgnoreCase);
                    }

                    args.IRCMessageType = _messageType;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return "RewriteContent:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_messageType)) ? "" : String.Format(" MessageType={0}", _messageType))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
                + ((String.IsNullOrEmpty(_replacePattern)) ? "" : String.Format(" ReplacePattern={0}", _replacePattern))
            ;
        }
    }
    
    public class Redirect : FilterItem
    {
        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }

        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        private String _channelName = "";
        public String ChannelName
        {
            get { return _channelName; }
            set { _channelName = value; }
        }

        private Boolean _duplicate = true;
        public Boolean Duplicate
        {
            get { return _duplicate; }
            set { _duplicate = value; }
        }

        public override Boolean Execute(FilterArgs args)
        {
            if (String.IsNullOrEmpty(_channelName))
                return false;

            if (!String.IsNullOrEmpty(_matchPattern))
            {
                Boolean rerouteRequired =
                    Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern)) ? true : Regex.IsMatch(args.User.ScreenName, _userMatchPattern));
                
                if (!rerouteRequired)
                    return false;
                
                IRCMessage msg;
                switch (args.IRCMessageType.ToUpperInvariant())
                {
                    case "NOTICE":
                        msg = new NoticeMessage(_channelName, args.Content);
                        break;
                    case "PRIVMSG":
                    default:
                        msg = new PrivMsgMessage(_channelName, args.Content);
                        break;
                }
                msg.SenderNick = args.User.ScreenName;
                msg.SenderHost = "twitter@" + Server.ServerName;
                args.Session.Send(msg);
                
                if (!_duplicate)
                    args.Drop = true;

                return true;
            }
            return false;
        }
        public override string ToString()
        {
            return "Redirect:"
                + ((Enabled) ? "" : "[DISABLED]")
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
                + ((String.IsNullOrEmpty(_channelName)) ? "" : String.Format(" ChannelName={0}", _channelName))
                + ((_duplicate) ? " Duplicate" : "")
            ;
        }
    }

    public class Process : FilterItem
    {
        private String _replacePattern = "";
        public String ReplacePattern
        {
            get { return _replacePattern; }
            set { _replacePattern = value; }
        }

        private String _matchPattern = "";
        public String MatchPattern
        {
            get { return _matchPattern; }
            set { _matchPattern = value; }
        }

        private String _userMatchPattern = "";
        public String UserMatchPattern
        {
            get { return _userMatchPattern; }
            set { _userMatchPattern = value; }
        }

        private String _messageType = "PRIVMSG";
        public String MessageType
        {
            get { return _messageType; }
            set { _messageType = value; }
        }

        public String InputEncoding
        {
            get; set;
        }

        public String OutputEncoding
        {
            get; set;
        }
        
        public String ProcessPath
        {
            get; set;
        }

        public String Arguments
        {
            get; set;
        }

        public Boolean XmlMode
        {
            get; set;
        }

        public override Boolean Execute(FilterArgs args)
        {
            if (!String.IsNullOrEmpty(_matchPattern))
            {
                if (Regex.IsMatch(args.Content, _matchPattern, RegexOptions.IgnoreCase) &&
                    ((String.IsNullOrEmpty(_userMatchPattern))
                         ? true
                         : Regex.IsMatch(args.User.ScreenName, _userMatchPattern)))
                {
                    Encoding encIn = (InputEncoding == null)
                                         ? Encoding.Default
                                         :(String.Compare(InputEncoding, "UTF-8", true) == 0)
                                                 ? new UTF8Encoding(false)
                                                 : Encoding.GetEncoding(InputEncoding);

                    Encoding encOut = (OutputEncoding == null)
                                          ? Encoding.Default
                                          : (String.Compare(OutputEncoding, "UTF-8", true) == 0)
                                                ? new UTF8Encoding(false)
                                                : Encoding.GetEncoding(OutputEncoding);

                    TraceLogger.Filter.Information(String.Format("Start: {0} ({1})", ProcessPath, Arguments));
                    
                    ProcessStartInfo psInfo = new ProcessStartInfo(ProcessPath, Arguments);
                    psInfo.RedirectStandardInput = true;
                    psInfo.RedirectStandardOutput = true;
                    psInfo.StandardOutputEncoding = encIn; // Process -> TIG のエンコーディング
                    psInfo.UseShellExecute = false;
                    psInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    psInfo.CreateNoWindow = true;

                    Dictionary<String, String> headers =
                        new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    String content;
                    using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psInfo))
                    using (StreamWriter sw = new StreamWriter(process.StandardInput.BaseStream, encOut))
                    {
                        sw.WriteLine("Url: http://twitter.com/{0}/statuses/{1}", args.Status.User.ScreenName,
                                     args.Status.Id);
                        WriteFieldsAndProperties(sw, "User", args.User);
                        WriteFieldsAndProperties(sw, "Status", args.Status);
                        sw.WriteLine("Filter-Drop: {0}", args.Drop);
                        sw.WriteLine("Filter-IRCMessageType: {0}", args.IRCMessageType);
                        sw.WriteLine();
                        sw.WriteLine(args.Content);
                        sw.Close();

                        String output = process.StandardOutput.ReadToEnd();
                        if (String.IsNullOrEmpty(output))
                            return false;

                        String[] parts = output.Split(new string[] {"\n\n"}, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 1)
                        {
                            content = parts[0].Trim();
                        }
                        else
                        {
                            if (parts.Length == 0)
                            {
                                TraceLogger.Filter.Information("Filter was recieved invalid data");
                                return false;
                            }
                            foreach (var line in parts[0].Split('\n'))
                            {
                                String[] headerParts = line.Split(new char[] {':'}, 2);
                                if (headerParts.Length != 2)
                                    continue;

                                headers[headerParts[0].Trim()] = headerParts[1].Trim();
                            }
                            content = parts[1].Trim();
                        }

                        if (process.WaitForExit(60*1000))
                        {
                            TraceLogger.Filter.Information("Process Exited: " + process.ExitCode.ToString());
                            // 終了コード見る
                            if (process.ExitCode == 0)
                            {
                                // 書き換え
                                Boolean drop = args.Drop;
                                if (headers.ContainsKey("Filter-Drop"))
                                    Boolean.TryParse(headers["Filter-Drop"], out drop);
                                if (headers.ContainsKey("Filter-IRCMessageType"))
                                    args.IRCMessageType = headers["Filter-IRCMessageType"];

                                args.Content = content;
                            }
                            else
                            {
                                // 何もしない
                                return false;
                            }
                        }
                    }

                    args.IRCMessageType = _messageType;
                    return true;
                }
            }
            return false;
        }
        
        private void WriteFieldsAndProperties(TextWriter writer, String name, Object o)
        {
            Type t = o.GetType();
            foreach (MemberInfo mi in t.GetMembers(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.GetField | BindingFlags.Instance))
            {
                if (mi.Name.StartsWith("_"))
                    continue;
                
                Object value = null;
                if (mi.MemberType == MemberTypes.Property)
                {
                    PropertyInfo pi = (PropertyInfo) mi;
                    value = pi.GetValue(o, null);
                }
                else if (mi.MemberType == MemberTypes.Field)
                {
                    FieldInfo fi = (FieldInfo) mi;
                    value = fi.GetValue(o);
                }
                if (value != null)
                    writer.WriteLine("{0}-{1}: {2}", name, mi.Name, EscapeString(value.ToString()));
            }
        }
        private String EscapeString(String s)
        {
            return s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        public override string ToString()
        {
            return "Process:"
                + ((Enabled) ? "" : "[DISABLED]")
                + String.Format(" ProcessPath={0}", ProcessPath)
                + ((String.IsNullOrEmpty(_userMatchPattern)) ? "" : String.Format(" UserMatchPattern={0}", _userMatchPattern))
                + ((String.IsNullOrEmpty(_messageType)) ? "" : String.Format(" MessageType={0}", _messageType))
                + ((String.IsNullOrEmpty(_matchPattern)) ? "" : String.Format(" MatchPattern={0}", _matchPattern))
                + ((String.IsNullOrEmpty(_replacePattern)) ? "" : String.Format(" ReplacePattern={0}", _replacePattern))
            ;
        }
    }
}
