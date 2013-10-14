using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns
{
    class UndoStatusUpdate : AddInBase
    {
        private LinkedList<Int64> _lastUpdateStatusList = new LinkedList<Int64>();
        private const String UndoCommandString = "undo";
        private const Int32 MaxUndoCount = 10;
        
        public override void Initialize()
        {
            CurrentSession.PostSendUpdateStatus += (sender, e) =>
                                            {
                                                _lastUpdateStatusList.AddLast(e.CreatedStatus.Id);
                                                if (_lastUpdateStatusList.Count > MaxUndoCount)
                                                {
                                                    _lastUpdateStatusList.RemoveFirst();
                                                }
                                            };
            CurrentSession.UpdateStatusRequestReceived += (sender, e) =>
                                           {
                                               if (String.Compare(e.Text.Trim(), UndoCommandString, true) == 0)
                                               {
                                                   e.Cancel = true;
                                                    
                                                   // まず送信待ちをみる
                                                   if (CurrentSession.TryCancelDeferredUpdate())
                                                   {
                                                       CurrentSession.SendServer(
                                                           new NoticeMessage(e.ReceivedMessage.Receiver,
                                                                             "ステータスのアップデート予定を削除しました。"));
                                                       return;
                                                   }

                                                   // 送信済みを削除する
                                                   if (_lastUpdateStatusList.Count == 0)
                                                   {
                                                       CurrentSession.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver,
                                                                                            "ステータスアップデートの取り消しできません。"));
                                                       return;
                                                   }

                                                   // 削除する
                                                   try
                                                   {
                                                       Int64 statusId = _lastUpdateStatusList.Last.Value;
                                                       Status status = CurrentSession.TwitterService.DestroyStatus(statusId);
                                                       CurrentSession.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver,
                                                                                            String.Format(
                                                                                                "ステータス \"{0}\" ({1}) を削除しました。",
                                                                                                status.Text, status.Id)));
                                                       _lastUpdateStatusList.Remove(statusId);
                                                   }
                                                   catch (TwitterServiceException te)
                                                   {
                                                       CurrentSession.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver,
                                                                                            String.Format(
                                                                                                "ステータスの削除に失敗しました: {0}",
                                                                                                te.Message)));
                                                   }
                                                   catch (WebException we)
                                                   {
                                                       CurrentSession.SendServer(new NoticeMessage(e.ReceivedMessage.Receiver,
                                                                                            String.Format(
                                                                                                "ステータスの削除に失敗しました: {0}",
                                                                                                we.Message)));
                                                   }
                                               }
                                           };
        }
    }
}
