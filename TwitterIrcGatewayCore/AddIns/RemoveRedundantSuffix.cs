using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class RemoveRedundantSuffix : AddInBase
    {
        private static readonly Regex _suffixMatchRE = new Regex(@"^(\s*(\＜.{2,}\＞|\<.{2,}\>|\(.{2,}\)|\《.{2,}\》|\【.{2,}\】|\[.{2,}\]|\*.{2,}\*|lang:ja)+)$");
        private Dictionary<Int32, LinkedList<String>> _lastStatusFromFriends;
        
        public override void Initialize()
        {
            CurrentSession.PostFilterProcessTimelineStatus += new EventHandler<TimelineStatusEventArgs>(Session_PostFilterProcessTimelineStatus);
            CurrentSession.ConfigChanged += new EventHandler<EventArgs>(Session_ConfigChanged);

            if (CurrentSession.Config.EnableRemoveRedundantSuffix)
                _lastStatusFromFriends = new Dictionary<Int32, LinkedList<String>>();
        }

        void Session_ConfigChanged(object sender, EventArgs e)
        {
            if (_lastStatusFromFriends == null && CurrentSession.Config.EnableRemoveRedundantSuffix)
            {
                _lastStatusFromFriends = new Dictionary<Int32, LinkedList<String>>();
            }
            else if (!CurrentSession.Config.EnableRemoveRedundantSuffix)
            {
                _lastStatusFromFriends = null;
            }
        }

        void Session_PostFilterProcessTimelineStatus(object sender, TimelineStatusEventArgs e)
        {
            // Remove Redundant Suffixes
            if (CurrentSession.Config.EnableRemoveRedundantSuffix)
            {
                if (e.Status.User == null) return;

                if (!_lastStatusFromFriends.ContainsKey(e.Status.User.Id))
                {
                    _lastStatusFromFriends[e.Status.User.Id] = new LinkedList<String>();
                }
                LinkedList<String> lastStatusTextsByUId = _lastStatusFromFriends[e.Status.User.Id];
                String suffix = DetectRedundantSuffix(e.Text, lastStatusTextsByUId);
                lastStatusTextsByUId.AddLast(e.Text);
                if (lastStatusTextsByUId.Count > 5)
                {
                    lastStatusTextsByUId.RemoveFirst();
                }
                if (!String.IsNullOrEmpty(suffix))
                {
                    Debug.WriteLine("Remove Redundant suffix: " + suffix);
                    e.Text = e.Text.Substring(0, e.Text.Length - suffix.Length);
                }
            }
        }

        /// <summary>
        /// 重複した末尾の文字列を取得します。
        /// </summary>
        /// <param name="text">対象の文字列</param>
        /// <param name="hintTexts">ヒントとなる文字列のコレクション</param>
        /// <returns></returns>
        public static String DetectRedundantSuffix(String text, ICollection<String> hintTexts)
        {
            String redundantSuffix = null;
            String a1 = text;
            foreach (var a2 in hintTexts)
            {
                for (var i = 0; i < a1.Length; i++)
                {
                    // HACK: Ordinalを指定しないと特定の条件下でMonoで死ぬ
                    var pos = a2.LastIndexOf(a1.Substring(i), StringComparison.Ordinal);
                    if (pos > -1)
                    {
                        var suffix = a1.Substring(i);
                        var matches = _suffixMatchRE.Matches(suffix);
                        if (matches.Count > 0)
                        {
                            redundantSuffix = a1 = suffix;
                            break;
                        }
                    }
                }
            }

            return redundantSuffix;
        }
    }
}
