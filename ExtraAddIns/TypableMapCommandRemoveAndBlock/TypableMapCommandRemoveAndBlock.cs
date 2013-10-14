using System;
using System.Collections.Generic;
using System.Text;
using Misuzilla.Net.Irc;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.TypableMap
{
    public class TypableMapCommandRemoveAndBlock : AddInBase
    {
        public override void Initialize()
        {
            Session.AddInsLoadCompleted += (sender, e) =>
                                               {
                                                   TypableMapCommandProcessor typableMapCmd = Session.AddInManager.GetAddIn<TypableMapSupport>().TypableMapCommands;
                                                   if (typableMapCmd != null)
                                                   {
                                                       typableMapCmd.AddCommand(new BlockCommand());
                                                       typableMapCmd.AddCommand(new UnblockCommand());
                                                       typableMapCmd.AddCommand(new RemoveCommand());
                                                       typableMapCmd.AddCommand(new FollowCommand());
                                                   }
                                               };
            Session.ConfigChanged += (sender, e) =>
                                         {
                                             // 手抜き
                                             TypableMapCommandProcessor typableMapCmd = Session.AddInManager.GetAddIn<TypableMapSupport>().TypableMapCommands;
                                             if (typableMapCmd != null)
                                             {
                                                 typableMapCmd.AddCommand(new BlockCommand());
                                                 typableMapCmd.AddCommand(new UnblockCommand());
                                                 typableMapCmd.AddCommand(new RemoveCommand());
                                                 typableMapCmd.AddCommand(new FollowCommand());
                                             }
                                         };
        }
    }

    public class FollowCommand : RemoveCommand
    {
        public override String CommandName
        {
            get { return "follow"; }
        }
    }

    public class RemoveCommand : TypableMapCommandProcessor.ITypableMapCommand
    {
        #region ITypableMapCommand メンバ

        public virtual string CommandName
        {
            get { return "remove"; }
        }

        public bool Process(TypableMapCommandProcessor processor, Misuzilla.Net.Irc.PrivMsgMessage msg, Status status, string args)
        {
            Boolean isDestroy = (String.Compare(CommandName, "remove", true) == 0);

            return processor.Session.RunCheck(() =>
            {
                if (isDestroy)
                    processor.Session.TwitterService.DestroyFriendship(status.User.ScreenName);
                else
                    processor.Session.TwitterService.CreateFriendship(status.User.ScreenName);

                processor.Session.SendServer(new NoticeMessage
                {
                    Receiver = msg.Receiver,
                    Content =
                        String.Format(
                        "ユーザ {0} を {1} しました。",
                        status.User.ScreenName, (isDestroy ? "remove" : "follow")
                        )
                });
            });
        }

        #endregion
    }

    public class BlockCommand : UnblockCommand
    {
        public override string CommandName
        {
            get { return "block"; }
        }
    }
    public class UnblockCommand : TypableMapCommandProcessor.ITypableMapCommand
    {
        #region ITypableMapCommand メンバ

        public virtual string CommandName
        {
            get { return "unblock"; }
        }

        public bool Process(TypableMapCommandProcessor processor, Misuzilla.Net.Irc.PrivMsgMessage msg, Status status, string args)
        {
            Boolean isDestroy = (String.Compare(CommandName, "unblock", true) == 0);

            return processor.Session.RunCheck(() =>
            {
                if (isDestroy)
                    processor.Session.TwitterService.DestroyBlock(status.User.ScreenName);
                else
                    processor.Session.TwitterService.CreateBlock(status.User.ScreenName);

                processor.Session.SendServer(new NoticeMessage
                {
                    Receiver = msg.Receiver,
                    Content =
                        String.Format(
                        "ユーザ {0} を {1} しました。",
                        status.User.ScreenName, (isDestroy ? "unblock" : "block")
                        )
                });
            });
        }

        #endregion
    }
}
