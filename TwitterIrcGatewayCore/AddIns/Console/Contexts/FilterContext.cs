using System;
using System.ComponentModel;
using Misuzilla.Applications.TwitterIrcGateway.Filter;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("フィルタの設定を行うコンテキストに切り替えます")]
    public class FilterContext : Context
    {
        [Description("存在するフィルタをすべて表示します")]
        public void List()
        {
            if (CurrentSession.Filters.Items.Length == 0)
            {
                Console.NotifyMessage("フィルタは現在設定されていません。");
                return;
            }
            
            for (var i = 0; i < CurrentSession.Filters.Items.Length; i++)
            {
                FilterItem filter = CurrentSession.Filters.Items[i];
                Console.NotifyMessage(String.Format("{0}: {1}", i, filter.ToString()));
            }
        }

        [Description("指定したフィルタを有効化します")]
        public void Enable(String args)
        {
            SwitchEnable(args, true);
        }

        [Description("指定したフィルタを無効化します")]
        public void Disable(String args)
        {
            SwitchEnable(args, false);
        }
        
        [Description("指定したフィルタを削除します")]
        public void Remove(String args)
        {
            FindAt(args, item => {
                CurrentSession.Filters.Remove(item);
                CurrentSession.SaveFilters();
                Console.NotifyMessage(String.Format("フィルタ {0} を削除しました。", item));
            });
        }
        
        [Description("指定したフィルタを編集します")]
        public void Edit(String args)
        {
            FindAt(args, item =>
                             {
                                 Type genericType = typeof (EditFilterContext<>).MakeGenericType(item.GetType());
                                 Context ctx = Activator.CreateInstance(genericType, item) as Context;
                                 ctx.CurrentServer = CurrentServer;
                                 ctx.CurrentSession = CurrentSession;
                                 ctx.Console = Console;

                                 Console.PushContext(ctx);
                             });
        }

        [Description("指定した種類のフィルタを新規追加します")]
        public void New(String filterTypeName)
        {
            Type filterType = Type.GetType("Misuzilla.Applications.TwitterIrcGateway.Filter."+filterTypeName, false, true);
            if (filterType == null || filterType == typeof(Process) || !filterType.IsSubclassOf(typeof(FilterItem)))
            {
                Console.NotifyMessage("不明なフィルタの種類が指定されました。");
                return;
            }
            Type genericType = typeof (EditFilterContext<>).MakeGenericType(filterType);
            Console.PushContext(Console.GetContext(genericType, CurrentServer, CurrentSession));
        }

        [Description("フィルタを再読み込みします")]
        public void Reload()
        {
            CurrentSession.LoadFilters();
            Console.NotifyMessage("フィルタを再読み込みしました。");
        }

        private void SwitchEnable(String args, Boolean enable)
        {
            FindAt(args, item => {
                item.Enabled = enable;
                CurrentSession.SaveFilters();
                Console.NotifyMessage(String.Format("フィルタ {0} を{1}化しました。", item, (enable ? "有効" : "無効")));
            });
        }
        private void FindAt(String args, Action<FilterItem> action)
        {
            Int32 index;
            FilterItem[] items = CurrentSession.Filters.Items;
            if (Int32.TryParse(args, out index))
            {
                if (index < items.Length && index > -1)
                {
                    action(items[index]);
                }
                else
                {
                    Console.NotifyMessage("存在しないフィルタが指定されました。");
                }
            }
            else
            {
                Console.NotifyMessage("フィルタの指定が正しくありません。");
            }
        }
    }

    /// <summary>
    /// 編集用のジェネリックコンテキスト
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class EditFilterContext<T> : Context where T : FilterItem, new()
    {
        private Boolean _isNewRecord;
        private T _filter;

        public override string ContextName
        {
            get
            {
                return (_isNewRecord ? "New" : "Edit") + typeof (T).Name;
            }
        }
        
        public EditFilterContext()
        {
            _filter = new T();
            _isNewRecord = true;
        }
        
        public EditFilterContext(T filterItem)
        {
            _filter = filterItem;
            _isNewRecord = false;
        }
        
        public override IConfiguration[] Configurations
        {
            get
            {
                return new IConfiguration[] { _filter };
            }
        }

        [Description("フィルタを保存してコンテキストを終了します")]
        public void Save()
        {
            if (_isNewRecord)
                CurrentSession.Filters.Add(_filter);
            CurrentSession.SaveFilters();
            Console.NotifyMessage(String.Format("フィルタを{0}しました。", (_isNewRecord ? "新規作成" : "保存")));
            Exit();
        }
    }
}
