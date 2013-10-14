import sys
import clr
import re
import thread
import time

import Misuzilla.Applications.TwitterIrcGateway
import Misuzilla.Applications.TwitterIrcGateway.AddIns
import Misuzilla.Applications.TwitterIrcGateway.AddIns.Console

from System import *
from System.Threading import Thread, ThreadStart
from System.Diagnostics import Trace
from System.Collections.Generic import *
from Misuzilla.Applications.TwitterIrcGateway import Status, Statuses, User, Users, Utility
from Misuzilla.Applications.TwitterIrcGateway.AddIns import IConfiguration
from Misuzilla.Applications.TwitterIrcGateway.AddIns.Console import ConsoleAddIn, Console, Context
from Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration import DLRIntegrationAddIn, DLRBasicConfiguration, DLRContextHelper

class WatcherContext(Context):
	def Initialize(self):
		self.watcher = Watcher.instance()
		self.config = DLRBasicConfiguration(self.CurrentSession, "WatcherContext", Dictionary[String,String]({ "Interval": "取得間隔", "Targets": "ウォッチ対象" }))
		pass

	def GetCommands(self):
		dict = Context.GetCommands(self)
		dict["Interval"] = "取得間隔を設定します。"
		dict["Watch"] = "指定したユーザを監視対象に追加します。"
		dict["Unwatch"] = "指定したユーザを監視対象から削除します。"
		dict["Watchlist"] = "監視対象ユーザを一覧表示します。"
		return dict

	def OnUninitialize(self):
		pass

	def get_Configurations(self):
		return Array[IConfiguration]([ self.config ])

	# Implementation
	def Interval(self, args):
		if String.IsNullOrEmpty(args):
			self.Console.NotifyMessage("取得間隔を指定してください。")
			return
		interval = int(args, 10)
		self.watcher.interval = interval
		self.config.SetValue("Interval", interval)
		self.Console.NotifyMessage("取得間隔を %s 秒に設定しました。" % args)
		self.watcher.start()

	def Watch(self, args):
		if String.IsNullOrEmpty(args):
			self.Console.NotifyMessage("監視対象を指定する必要があります。")
			return
		targets = filter(lambda x: x != "", (self.config.GetValue("Targets") or "").split(","))

		if args in targets:
			self.Console.NotifyMessage("指定されたユーザは監視対象になっています。")
			return

		targets.append(args)
		self.config.SetValue("Targets", ",".join(targets))
		self.Console.NotifyMessage("%s を監視対象に追加しました。" % args)
		self.watcher.targets = targets
		self.watcher.start()

	def Unwatch(self, args):
		if String.IsNullOrEmpty(args):
			self.Console.NotifyMessage("監視対象を指定する必要があります。")
			return
		targets = filter(lambda x: x != "", (self.config.GetValue("Targets") or "").split(","))

		if not args in targets:
			self.Console.NotifyMessage("指定されたユーザは監視対象ではありません。")
			return

		targets = filter(lambda x: String.Compare(x, args, True) != 0, targets)
		self.config.SetValue("Targets", ",".join(targets))
		self.Console.NotifyMessage("%s を監視対象から削除しました。" % args)
		self.watcher.targets = targets
		self.watcher.start()

	def Watchlist(self, args):
		targets = filter(lambda x: x != "", (self.config.GetValue("Targets") or "").split(","))
		for target in targets:
			self.Console.NotifyMessage(target)
		self.Console.NotifyMessage("現在 %d 人を監視しています。" % len(targets))

class Watcher(Object):
	@classmethod
	def instance(klass):
		if not hasattr(klass, 'instance_'):
			klass.instance_ = Watcher()
		return klass.instance_
	
	def __init__(self):
		CurrentSession.AddInManager.GetAddIn[DLRIntegrationAddIn]().BeforeUnload += self.onBeforeUnload

		# 普通の #Console にコンテキストを追加する
		CurrentSession.AddInManager.GetAddIn[ConsoleAddIn]().RegisterContext(DLRContextHelper.Wrap(CurrentSession, "WatcherContext", WatcherContext), "Watcher", "指定したユーザを監視します。")

		self.config = DLRBasicConfiguration(CurrentSession, "WatcherContext", Dictionary[String,String]({ "Interval": "取得間隔", "Targets": "ウォッチ対象" }))
		self.targets = filter(lambda x: x != "", (self.config.GetValue("Targets") or "").split(","))
		self.interval = int(self.config.GetValue("Interval") or "30", 10)
		self.running = False

		self.thread = None
		self.buffer = {}
		self.re_source = re.compile(r"<span>from (.*?)</span>")
		self.re_statuses = re.compile(r"<li class=\"hentry status.*?</li>")
		self.re_content = re.compile(r"class=\"entry-content\">(.*?)</span>")
		self.re_user = re.compile(r"class=\"screen-name\" title=\"([^\"]+)\">(.*?)</a>")
		self.re_user_name = re.compile(r"</span> <span class=\"fn\">(.*?)</span></li>")
		self.re_user_screen_name = re.compile(r"<meta content=\"([^\"]+)\" name=\"page-user-screen_name\" />")
		self.re_anchor = re.compile(r"<a href=\"(http://[^\"]*)\"[^>]*>.*?</a>")
		self.re_tag = re.compile(r"<[^>]*>")
		self.re_status_id = re.compile(r"id=\"status_(\d+)\"")
		
	def start(self):
		if not self.running:
			CurrentSession.TwitterService.CookieLogin()
			self.thread = Thread(ThreadStart(self.runProc))
			self.thread.Start()

	def runProc(self):
		Trace.WriteLine("Start Watching")
		self.running = True
		while self.interval > 0 and len(self.targets) > 0:
			for target in self.targets:
				try:
					self.fetch(target)
				except:
					Trace.WriteLine(sys.exc_info().ToString())
			Thread.Sleep(self.interval * 1000)
		self.running = False
		Trace.WriteLine("Stop Watching")

	def fetch(self, screenName):
		home = CurrentSession.TwitterService.GETWithCookie(("/%s" % screenName))
		statuses = self.re_statuses.findall(home)
		statuses.reverse()

		# User
		m_name = self.re_user_name.search(home)
		m_screen_name = self.re_user_screen_name.search(home)
		user            = User()
		user.Id         = 0
		user.Name       = m_name.group(1)
		user.ScreenName = m_screen_name.group(1)

		if not self.buffer.has_key(user.ScreenName):
			self.buffer[user.ScreenName] = []

		for status in statuses:
			s = Status()

			# Status
			s.User = user
			s.Source    = self.re_source.search(status).group(1)
			s.Text      = Utility.UnescapeCharReference(self.re_tag.sub(r"", self.re_anchor.sub(r"\1", self.re_content.search(status).group(1))))
			s.Id        = int(self.re_status_id.search(status).group(1), 10)
			s.CreatedAt = DateTime.Now
			
			#Trace.WriteLine(s.ToString())
			#Trace.WriteLine(repr(self.buffer[user.ScreenName]))
			# 流れていないものだけ流す
			if not s.Id in self.buffer[user.ScreenName]:
				self.buffer[user.ScreenName].append(s.Id)
				CurrentSession.TwitterService.ProcessStatus(s, Action[Status](lambda s1: CurrentSession.ProcessTimelineStatus(s1, False, False)))
				if len(self.buffer[user.ScreenName]) > 50:
					self.buffer[user.ScreenName].pop(0)

	def onBeforeUnload(self, sender, e):
		CurrentSession.AddInManager.GetAddIn[ConsoleAddIn]().UnregisterContext(DLRContextHelper.Wrap(CurrentSession, "WatcherContext", WatcherContext))
		self.interval = 0
		self.thread.Abort()
		self.thread.Join(5000)

watcher = Watcher.instance()
watcher.start()
