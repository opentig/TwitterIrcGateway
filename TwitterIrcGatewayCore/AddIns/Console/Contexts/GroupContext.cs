using System;
using System.ComponentModel;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.Console
{
    /// <summary>
    /// 
    /// </summary>
    [Description("グループの設定を行うコンテキストに切り替えます")]
    public class GroupContext : Context
    {
        [Description("指定したグループ(チャンネル)にユーザを追加します")]
        public void Invite(String[] channelNameAndUserNames)
        {
            if (channelNameAndUserNames.Length == 1)
            {
                Console.NotifyMessage("エラー: ユーザが指定されていません。");
                return;
            }

            if (!Session.Groups.ContainsKey(channelNameAndUserNames[0]))
            {
                Console.NotifyMessage("エラー: 指定されたグループは存在しません。");
                return;
            }

            for (var i = 1; i < channelNameAndUserNames.Length; i++)
            {
                Group group = CurrentSession.Groups[channelNameAndUserNames[0]];
                String userName = channelNameAndUserNames[i];
                if (!group.Exists(userName) && (String.Compare(userName, CurrentSession.Nick, true) != 0))
                {
                    group.Add(userName);
                    if (group.IsJoined)
                    {
                        JoinMessage joinMsg = new JoinMessage(channelNameAndUserNames[0], "")
                        {
                            SenderHost = "twitter@" + Server.ServerName,
                            SenderNick = userName
                        };
                        CurrentSession.Send(joinMsg);
                    }
                }

            }

            CurrentSession.SaveGroups();
        }

        [Description("指定したグループ(チャンネル)からユーザを削除します")]
        public void Kick(String[] channelNameAndUserNames)
        {
            if (channelNameAndUserNames.Length == 1)
            {
                Console.NotifyMessage("エラー: ユーザが指定されていません。");
                return;
            }

            if (!Session.Groups.ContainsKey(channelNameAndUserNames[0]))
            {
                Console.NotifyMessage("エラー: 指定されたグループは存在しません。");
                return;
            }

            for (var i = 1; i < channelNameAndUserNames.Length; i++)
            {
                Group group = CurrentSession.Groups[channelNameAndUserNames[0]];
                String userName = channelNameAndUserNames[i];
                if (group.Exists(userName))
                {
                    group.Remove(userName);
                    if (group.IsJoined)
                    {
                        PartMessage partMsg = new PartMessage(channelNameAndUserNames[0], "")
                        {
                            SenderHost = "twitter@" + Server.ServerName,
                            SenderNick = userName
                        };
                        CurrentSession.Send(partMsg);
                    }
                }

            }

            CurrentSession.SaveGroups();
        }
    
        [Description("指定したグループ(チャンネル)の名前を変更します")]
        public void Rename([Description("現在のグループ名")]String oldChannelName, [Description("新しいグループ名")]String newChannelName)
        {
            if (!CheckChannelNamesForCopyOrRename(oldChannelName, newChannelName))
                return;

            // 旧チャンネルをPART
            CurrentSession.SendServer(new PartMessage(oldChannelName, ""));

            Group g = CurrentSession.Groups[oldChannelName];
            g.Name = newChannelName;
            CurrentSession.Groups.Remove(oldChannelName);
            CurrentSession.Groups.Add(newChannelName, g);
            
            // 新チャンネルにJOIN
            CurrentSession.JoinChannel(CurrentSession, g);
            Console.NotifyMessage(String.Format("グループ名 {0} を {1} に変更しました。", oldChannelName, newChannelName));

            CurrentSession.SaveGroups();
        }
    
        [Description("グループ(チャンネル)を新規作成します")]
        public void New([Description("新しいグループ名")]String newChannelName)
        {
            if (!CheckNewChannelName(newChannelName))
                return;

            // 新チャンネルにJOIN
            Group g = new Group(newChannelName);
            CurrentSession.Groups.Add(newChannelName, g);
            CurrentSession.JoinChannel(CurrentSession, g);
            Console.NotifyMessage(String.Format("グループ名 {0} を作成しました。", newChannelName));

            CurrentSession.SaveGroups();
        }
        
        [Description("指定したグループ(チャンネル)を削除します")]
        public void Delete([Description("削除するグループ名")]String oldChannelName)
        {
            if (!CurrentSession.Groups.ContainsKey(oldChannelName))
            {
                Console.NotifyMessage("エラー: 指定されたグループは見つかりませんでした。");
                return;
            }


            // 旧チャンネルをPART
            CurrentSession.SendServer(new PartMessage(oldChannelName, ""));
            // 削除
            CurrentSession.Groups.Remove(oldChannelName);
            Console.NotifyMessage(String.Format("グループ名 {0} を削除しました。", oldChannelName));

            CurrentSession.SaveGroups();
        }
        
        [Description("指定したグループ(チャンネル)の名前をコピーします")]
        public void Copy([Description("現在のグループ名")]String oldChannelName, [Description("新しいグループ名")]String newChannelName)
        {
            if (!CheckChannelNamesForCopyOrRename(oldChannelName, newChannelName))
                return;

            Group g = CurrentSession.Groups[oldChannelName].Clone();
            g.Name = newChannelName;
            CurrentSession.Groups.Add(newChannelName, g);
            
            // 新チャンネルにJOIN
            CurrentSession.JoinChannel(CurrentSession, g);
            Console.NotifyMessage(String.Format("グループ {0} を {1} にコピーしました。", oldChannelName, newChannelName));
            
            CurrentSession.SaveGroups();
        }
    
        private Boolean CheckNewChannelName(String newChannelName)
        {
            if (CurrentSession.Groups.ContainsKey(newChannelName) || String.Compare(CurrentSession.Config.ChannelName, newChannelName, true) == 0)
            {
                Console.NotifyMessage("エラー: 既に存在するグループ名を指定することは出来ません。");
                return false;
            }

            if (!(newChannelName.StartsWith("#") && newChannelName.Length > 2))
            {
                Console.NotifyMessage("エラー: グループ(チャンネル)名は#で始まる必要があります。");
                return false;
            }

            return true;
        }
        private Boolean CheckChannelNamesForCopyOrRename(String oldChannelName, String newChannelName)
        {
            if (!CurrentSession.Groups.ContainsKey(oldChannelName))
            {
                Console.NotifyMessage("エラー: 指定されたグループは見つかりませんでした。");
                return false;
            }
            if (String.Compare(oldChannelName, CurrentSession.Config.ChannelName, true) == 0)
            {
                Console.NotifyMessage("エラー: メインタイムラインのチャンネル名を変更するにはConfigコンテキストの設定変更で行う必要があります。");
                return false;
            }

            return CheckNewChannelName(newChannelName);
        }
    }
}
