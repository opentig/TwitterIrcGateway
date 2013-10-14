#
# IronPythonでイベントをハンドルするサンプルスクリプト
#
import clr
from Misuzilla.Applications.TwitterIrcGateway import Status, Statuses, User, Users, Utility
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

# タイムラインをクライアントに送信する直前のイベントハンドラ
def OnPreSendMessageTimelineStatus(sender, e):
	e.Text = e.Text + " (by "+ e.Status.User.Name +")"

# 後片付けイベントハンドラ(これを行わないとイベントが外れないのでリロードするたびに増えてしまう)
def OnBeforeUnload(sender, e):
	Session.PreSendMessageTimelineStatus -= OnPreSendMessageTimelineStatus

# イベントハンドラを接続
Session.PreSendMessageTimelineStatus += OnPreSendMessageTimelineStatus
Session.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += OnBeforeUnload