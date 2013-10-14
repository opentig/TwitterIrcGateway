import clr

from System import *
from System.Collections.Generic import *
from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Net.Irc import *
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class AdminContext(Context):
	def Initialize(self):
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Users"] = "接続しているユーザの一覧を表示します。"
		dict["Notice"] = "接続しているユーザに一斉通知します。"
		dict["Disconnect"] = "接続しているユーザを切断します。"
		return dict

	def OnUninitialize(self):
		pass

	def get_Configurations(self):
		return Array[IConfiguration]([ ])

	# Implementation
	def users(self, args):
		for session in CurrentServer.Sessions.Values:
			self.Console.NotifyMessage(("%s, Id=%s" % (session, session.Id)))
		self.Console.NotifyMessage(("現在%d人のユーザが接続しています。" % (CurrentServer.Sessions.Count)))

	def notice(self, args):
		privMsg = PrivMsgMessage()
		privMsg.SenderNick = "TwitterIrcGateway-Admin"
		privMsg.SenderHost = "admin@" + Server.ServerName;
		privMsg.Content = args
		for session in CurrentServer.Sessions.Values:
			privMsg.Receiver = session.CurrentNick
			session.Send(privMsg)

	def disconnect(self, args):
		id = int(args)
		if not CurrentServer.Sessions.ContainsKey(id):
			self.Console.NotifyMessage("指定したIDを持つユーザは現在接続していません。")
			return
		CurrentServer.Sessions[id].Close()
		self.Console.NotifyMessage("指定したIDを持つユーザを切断しました。")

# #Console にコンテキストを追加する
Session.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap(CurrentSession, "AdminContext", AdminContext), "Admin", "管理用のコンテキストに切り替えます。")

# 後片付け
Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += lambda sender, e: Session.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContextHelper.Wrap(CurrentSession, "AdminContext", AdminContext))
