using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Remoting;
using Microsoft;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;
using System.ComponentModel;
using Microsoft.Scripting.Hosting;
using System.Diagnostics;
using System.IO;
using System.Runtime.Remoting.Proxies;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    /// <summary>
    /// DLRの型をContextとして登録するためのヘルパーメソッドを提供します。
    /// </summary>
    public static class DLRContextHelper
    {
        private static AssemblyName _asmName;
        private static AssemblyBuilder _asmBuilder;
        private static ModuleBuilder _modBuilder;
        static DLRContextHelper()
        {
            _asmName = new AssemblyName(@"Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration.DynamicAssembly.DLRContextHelper");
            _asmBuilder = System.Threading.Thread.GetDomain().DefineDynamicAssembly(_asmName, AssemblyBuilderAccess.Run);
            _modBuilder = _asmBuilder.DefineDynamicModule("DLRContextModule");
        }

        /// <summary>
        /// 指定したDLRのContextを登録できるようラップした型を返します。
        /// </summary>
        /// <param name="session">保持しているセッション</param>
        /// <param name="contextName">コンテキスト名</param>
        /// <param name="dlrContextType">DLRの型オブジェクト(PythonTypeなど)</param>
        /// <returns></returns>
        public static Type Wrap(Session session, String contextName, Object dlrContextType)
        {
            lock (_asmName)
            {
                Type type = _modBuilder.GetType(contextName);
                if (type == null)
                {
                    TypeBuilder typeBuilder = _modBuilder.DefineType(contextName);
                    type = typeBuilder.CreateType();
                }
                Type genCtxType = typeof (DLRContextBase<>).MakeGenericType(type);
                return
                    genCtxType.InvokeMember("GetProxyType",
                                            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.InvokeMethod,
                                            null, null, new Object[] {session, contextName, dlrContextType}) as Type;
            }
        }
    }
    /// <summary>
    /// DLRで利用できる設定クラスです。
    /// </summary>
    public class DLRBasicConfiguration : ICustomConfiguration
    {
        private DLRConfigurationStore _configurationStore;
        private Dictionary<String, ConfigurationPropertyInfo> _configurationPropInfoList;
        private String _configStoreName;
        private Session _session;

        public DLRBasicConfiguration() { }

        public DLRBasicConfiguration(Session session, String configStoreName, IDictionary<String, String> configEntries)
        {
            _session = session;
            _configurationPropInfoList = new Dictionary<String, ConfigurationPropertyInfo>(StringComparer.InvariantCultureIgnoreCase);
            _configStoreName = configStoreName;
            _configurationStore = _session.AddInManager.GetConfig<DLRConfigurationStore>();

            foreach (var kv in configEntries)
            {
                _configurationPropInfoList.Add(kv.Key, new ConfigurationPropertyInfo { Description = kv.Value, Name = kv.Key, Type = typeof(String) });
            }
        }

        public DLRBasicConfiguration(Session session, String configStoreName, ICollection<ConfigurationPropertyInfo> configEntries)
        {
            _session = session;
            _configurationPropInfoList = new Dictionary<String, ConfigurationPropertyInfo>(StringComparer.InvariantCultureIgnoreCase);
            _configStoreName = configStoreName;
            _configurationStore = _session.AddInManager.GetConfig<DLRConfigurationStore>();

            foreach (ConfigurationPropertyInfo configPropInfo in configEntries)
                _configurationPropInfoList.Add(configPropInfo.Name, configPropInfo);
        }
        #region ICustomConfiguration メンバ
        public ICollection<ConfigurationPropertyInfo> GetConfigurationPropertyInfo()
        {
            return _configurationPropInfoList.Values;
        }

        public void SetValue(string name, object value)
        {
            _configurationStore.SetValue(_configStoreName, name, ((value == null) ? null : value.ToString()));
            _session.AddInManager.SaveConfig(_configurationStore);
        }

        public object GetValue(string name)
        {
            ConfigurationPropertyInfo configPropInfo;
            if (!_configurationPropInfoList.TryGetValue(name, out configPropInfo))
                return null;

            TypeConverter tConv = TypeDescriptor.GetConverter(configPropInfo.Type);
            if (!tConv.CanConvertFrom(typeof(String)))
                return configPropInfo.DefaultValue;

            String value = _configurationStore.GetValue(_configStoreName, name);
            if (String.IsNullOrEmpty(value))
                return configPropInfo.DefaultValue;

            return tConv.ConvertFromString(value);
        }
        #endregion
    }
    
    /// <summary>
    /// TwitterIrcGateway内部で利用するDLR設定の保存クラスです。
    /// </summary>
    public class DLRConfigurationStore : IConfiguration
    {
        public String Content { get; set; }
        public DLRConfigurationStore()
        {
            Content = "";
        }

        private Dictionary<String, Dictionary<String, String>> _contentDict;
        private void BuildDictionary()
        {
            _contentDict = new Dictionary<string, Dictionary<string, string>>();
            using (StringReader stringReader = new StringReader(Content))
            {
                String line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    String[] parts = line.Split(new char[] {'\t'}, 3);
                    if (parts.Length != 3)
                    {
                        Trace.TraceWarning("Invalid configuration line:");
                        Trace.TraceWarning(line);
                        continue;
                    }
                    
                    if (!_contentDict.ContainsKey(parts[0]))
                        _contentDict[parts[0]] = new Dictionary<string, string>();

                    if (parts[2] == "(null)")
                        _contentDict[parts[0]][parts[1]] = null;
                    else
                        _contentDict[parts[0]][parts[1]] = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[2]));
                }
            }
        }
        private void Save()
        {
            StringWriter stringWriter = new StringWriter();
            foreach (var valueByStore in _contentDict)
            {
                foreach (var keyValue in valueByStore.Value)
                {
                    stringWriter.WriteLine("{0}\t{1}\t{2}", valueByStore.Key, keyValue.Key,
                        ((keyValue.Value == null) ? "(null)" : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(keyValue.Value))));
                }
            }
            Content = stringWriter.ToString();
        }

        public String GetValue(String storeName, String configName)
        {
            BuildDictionary();
            
            if (!_contentDict.ContainsKey(storeName))
                return null;
            if (!_contentDict[storeName].ContainsKey(configName))
                return null;
            return _contentDict[storeName][configName];
        }
        public void SetValue(String storeName, String configName, String value)
        {
            BuildDictionary();
            
            if (!_contentDict.ContainsKey(storeName))
                _contentDict[storeName] = new Dictionary<string, string>();
            _contentDict[storeName][configName] = value;

            Save();
        }
    }
    
    internal class WeakReferenceSessionEqualityComparer : IEqualityComparer<WeakReference>
    {
        #region IEqualityComparer<WeakReference> メンバ
        public bool Equals(WeakReference x, WeakReference y)
        {
            return (x.IsAlive && y.IsAlive) ? x.Target == y.Target
                                            : false;
        }
        public int GetHashCode(WeakReference obj)
        {
            return obj.IsAlive ? obj.Target.GetHashCode() : obj.GetHashCode();
        }
        #endregion
    }
    internal class TransparentProxiedSessionComparer : IEqualityComparer<Session>
    {
        #region IEqualityComparer<Session> メンバ
        public bool Equals(Session x, Session y)
        {
            return GetRealSession(x) == GetRealSession(y);
        }

        public int GetHashCode(Session obj)
        {
            return GetRealSession(obj).GetHashCode();
        }
        #endregion
    
        private Session GetRealSession(Session s)
        {
            if (!RemotingServices.IsTransparentProxy(s))
                return s;

            try
            {
                EventManagedProxy<Session> proxy;
                while (RemotingServices.IsTransparentProxy(s) &&
                       (proxy = RemotingServices.GetRealProxy(s) as EventManagedProxy<Session>) != null)
                {
                    s = proxy.Target;
                }
            }
            catch
            {
            }

            return s;
        }
    }

    internal class DLRContextBase<T> : Context where T : class
    {
        private DLRIntegrationAddIn _dlrAddIn;
        private DLRBasicConfiguration _basicConfiguration;
        private ScriptRuntime _scriptRuntime;
        private Context _site;
        private static Dictionary<Session, Object> _scriptTypes = new Dictionary<Session, Object>(new TransparentProxiedSessionComparer());
        private static Dictionary<Session, String> _contextNames = new Dictionary<Session, String>(new TransparentProxiedSessionComparer());
        public override string ContextName { get { return _contextNames[CurrentSession]; } }
        public override IConfiguration[] Configurations { get { return _site.Configurations; } }
        
        internal static Type GetProxyType(Session session, String contextName, Object scriptType)
        {
            // TODO: TransparentProxy であることを期待しているので若干のメモリリークを許してる
            _scriptTypes[session] = scriptType;
            _contextNames[session] = contextName;

            return typeof (DLRContextBase<T>);
        }
        
        public override void Initialize()
        {
            _dlrAddIn = CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _scriptRuntime = _dlrAddIn.ScriptRuntime;
            _site = _scriptRuntime.Operations.CreateInstance(_scriptTypes[CurrentSession]) as Context;
            if (_site == null)
                throw new ArgumentException("指定された型はContext クラスを継承していないためインスタンス化できません。");
            
            _site.CurrentServer = CurrentServer;
            _site.CurrentSession = CurrentSession;
            _site.Console = Console;
            _site.Initialize();

            base.Initialize();
        }

        private Func<Object, Object> _func;

        public override IDictionary<string, string> GetCommands()
        {
            var commands =_site.GetCommands();
            // いくつか削除する
            commands.Remove("OnConfigurationChanged");
            commands.Remove("OnConfigurationBeforeChange");
            commands.Remove("Equals");
            commands.Remove("MemberwiseClone");
            commands.Remove("ToString");
            commands.Remove("GetHashCode");
            commands.Remove("Finalize");
            return commands;
        }

        public override bool OnCallMissingCommand(string commandName, string rawInputLine)
        {
            return _site.OnCallMissingCommand(commandName, rawInputLine);
        }

        public override void Dispose()
        {
            try
            {
                if (_site != null)
                    _site.Dispose();
                _site = null;
            }
            catch { }
            
            base.Dispose();
        }
        
        public override MethodInfo GetCommand(string commandName)
        {
            try
            {
                var commandNameNormalized = commandName;
                var memberNames = _scriptRuntime.Operations.GetMemberNames(_site);
                // なぜかGetMemberのIgnoreCaseがきかないのでがんばる
                foreach (var memberName in memberNames)
                {
                    if (String.Compare(memberName, commandName, true) == 0)
                        commandNameNormalized = memberName;
                }
                var func = _scriptRuntime.Operations.GetMember<Func<Object, Object>>(_site, commandNameNormalized, true);
                
                if (func != null)
                {
                    _func = func;
                    return MethodInfo;
                }
            }
            catch (Exception e)
            {
            }
            return base.GetCommand(commandName);
        }

        public MethodInfo MethodInfo
        {
            get { return this.GetType().GetMethod("__WrapMethod__"); }
        }

        [Browsable(false)]
        public Object @__WrapMethod__(String args)
        {
            return _func(args);
        }
    }
}
