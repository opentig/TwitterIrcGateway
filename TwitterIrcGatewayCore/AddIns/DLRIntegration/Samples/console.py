#
# 独自のコンソールやコンテキストを実装するためのサンプルスクリプトです。
#
import clr

from System import *
from System.Collections.Generic import *
from System.Diagnostics import Trace
import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class TestContext(Context):
	def Initialize(self):
		self.config = DLRBasicConfiguration(self.CurrentSession, "TestContext", Dictionary[String,String]({ "Hauhau": "設定項目の説明" }))
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Hauhau"] = "Say Hauhau!"
		return dict

	def OnUninitialize(self):
		pass

	def get_Configurations(self):
		return Array[IConfiguration]([ self.config ])

	# Implementation
	def hauhau(self, args):
		self.Console.NotifyMessage(("Hauhau: %s" % (args)))
		self.Console.NotifyMessage(("Hauhau(Config): %s" % (self.config.GetValue("Hauhau"))))

# コンソールチャンネルを追加する
console = Misuzilla.Applications.TwitterIrcGateway.AddIns.Console.Console()
console.Attach("#TestConsole", Server, Session, DLRContextHelper.Wrap(CurrentSession, "Test", TestContext))

# 普通の #Console にコンテキストを追加する
Session.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap(CurrentSession, "DLRTest", TestContext), "DLRTest", "Context DLR implementation sample")

# 後片付け
def onBeforeUnload(sender, e):
	Session.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContextHelper.Wrap(CurrentSession, "DLRTest", TestContext))
	console.Detach()

Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += onBeforeUnload
