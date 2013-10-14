using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    /// <summary>
    /// アドインのインターフェース型。
    /// </summary>
    /// <remarks>
    /// アドインは<see cref="System.MarshalByRefObject" />を継承して、このインターフェースを実装する必要があります。
    /// 特別な理由がない限りは<see cref="AddInBase" />を継承してください。
    /// </remarks>
    public interface IAddIn
    {
        /// <summary>
        /// アドインが読み込まれ初期化されるためにアドインマネージャから呼び出されます。
        /// </summary>
        /// <param name="server">サーバのインスタンス</param>
        /// <param name="session">現在の接続のセッション情報</param>
        void Initialize(Server server, Session session);
        
        /// <summary>
        /// アドインが破棄される直前に呼び出されます。
        /// </summary>
        void Uninitialize();
    }

    /// <summary>
    /// 設定情報であることを示す、マーカーインターフェースです。
    /// </summary>
    /// <remarks>
    /// 設定情報クラスはこのインターフェースを実装する必要があります。
    /// </remarks>
    public interface IConfiguration
    {
    }

    /// <summary>
    /// カスタム設定情報であることを示すインターフェースです。
    /// </summary>
    /// <remarks>
    /// 設定情報クラスはこのインターフェースを実装する必要があります。
    /// </remarks>
    public interface ICustomConfiguration : IConfiguration
    {
        ICollection<ConfigurationPropertyInfo> GetConfigurationPropertyInfo();
        void SetValue(String Name, Object value);
        Object GetValue(String Name);
    }
    /// <summary>
    /// 設定可能なパラメータを表すクラスです。
    /// </summary>
    public class ConfigurationPropertyInfo
    {
        /// <summary>
        /// 設定名
        /// </summary>
        public String Name { get; set; }
        /// <summary>
        /// 設定の説明
        /// </summary>
        public String Description { get; set; }
        /// <summary>
        /// 設定の型
        /// </summary>
        public Type Type { get; set; }
        /// <summary>
        /// 値を取得がプロパティまたはフィールド経由の場合はPropertyInfoまたはFieldInfoを指定します
        /// </summary>
        public MemberInfo MemberInfo { get; set; }
        /// <summary>
        /// デフォルトの値
        /// </summary>
        public Object DefaultValue { get; set; }

        /// <summary>
        /// ConfigurationPropertyInfo クラスのインスタンスを作成します。
        /// </summary>
        public ConfigurationPropertyInfo()
        {
        }

        /// <summary>
        /// ConfigurationPropertyInfo クラスのインスタンスを作成して指定されたパラメータで初期化します。
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="defaultValue"></param>
        /// <param name="memberInfo"></param>
        public ConfigurationPropertyInfo(String name, String description, Type type, Object defaultValue, MemberInfo memberInfo)
        {
            Name = name;
            Description = description;
            Type = type;
            DefaultValue = defaultValue;
            MemberInfo = memberInfo;
        }

        /// <summary>
        /// 値を取得します。
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public Object GetValue(IConfiguration config)
        {
            Object value = null;
            if (MemberInfo is PropertyInfo)
                value = ((PropertyInfo)MemberInfo).GetValue(config, null);
            else if (MemberInfo is FieldInfo)
                value = ((FieldInfo)MemberInfo).GetValue(config);
            else if (config is ICustomConfiguration)
                value = ((ICustomConfiguration)config).GetValue(Name);

            return value;
        }

        /// <summary>
        /// 値を設定します。
        /// </summary>
        /// <param name="config"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void SetValue(IConfiguration config, Object value)
        {
            if (MemberInfo is PropertyInfo)
                ((PropertyInfo)MemberInfo).SetValue(config, value, null);
            else if (MemberInfo is FieldInfo)
                ((FieldInfo)MemberInfo).SetValue(config, value);
            else if (config is ICustomConfiguration)
                ((ICustomConfiguration)config).SetValue(Name, value);
        }
    }

    /// <summary>
    /// アドインのベースとなる基本的な機能を持ったクラスです。
    /// </summary>
    public abstract class AddInBase : MarshalByRefObject, IAddIn
    {
        /// <summary>
        /// 関連づけられているサーバのインスタンスを取得します。
        /// このプロパティは古い形式です。
        /// </summary>
        [Obsolete("このプロパティは古い形式です。CurrentServer プロパティを利用してください。")]
        protected Server Server { get { return CurrentServer; } }
        /// <summary>
        /// 関連づけられているセッション情報のインスタンスを取得します。
        /// このプロパティは古い形式です。
        /// </summary>
        [Obsolete("このプロパティは古い形式です。CurrentSession プロパティを利用してください。")]
        protected Session Session { get { return CurrentSession; } }
        /// <summary>
        /// 関連づけられているサーバのインスタンスを取得します。
        /// </summary>
        protected Server CurrentServer { get; private set; }
        /// <summary>
        /// 関連づけられているセッション情報のインスタンスを取得します。
        /// </summary>
        protected Session CurrentSession { get; private set; }

        #region IAddIn メンバ

        public void Initialize(Server server, Session session)
        {
            CurrentServer = server;
            CurrentSession = session;

            Initialize();
        }

        /// <summary>
        /// アドインが初期化されるときに呼び出されます。このメソッドをオーバーライドして実装します。
        /// </summary>
        public virtual void Initialize()
        {
        }

        /// <summary>
        /// アドインが破棄されるときに呼び出されます。既定ではイベントをすべて解除します。
        /// </summary>
        /// <remarks>
        /// このメソッドをオーバーライドして処理を行うことができますが、必ずベースクラスのUninitializeを呼び出してください。
        /// </remarks>
        public virtual void Uninitialize()
        {
        }

        #endregion
    }
}
