using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    public class GeneralConfig : IConfiguration
    {
        [Description("Search コマンドでの検索時の表示件数を指定します")]
        public Int32 SearchCount;
        [Description("Timeline コマンドでのタイムライン取得時の各タイムラインごとの表示件数を指定します")]
        public Int32 TimelineCount;
        [Description("Timeline, Search, Favorites コマンドでのステータスの後ろにURLをつけるかどうかを指定します")]
        public Int32 FavoritesCount;
        [Description("Favorites コマンドでのステータス取得時の表示件数を指定します")]
        public Boolean ShowPermalinkAfterStatus;

        [Browsable(false)]
        public List<String> ConsoleAliases { get; set; }

        public GeneralConfig()
        {
            SearchCount = 10;
            TimelineCount = 10;
            FavoritesCount = 10;
            ShowPermalinkAfterStatus = false;
        
            ConsoleAliases = new List<string>();
        }
    }
}
