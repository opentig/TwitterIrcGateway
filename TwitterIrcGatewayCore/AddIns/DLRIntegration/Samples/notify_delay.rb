# 遅延時間をシステムメッセージとして表示する
# vim:set expandtab tabstop=2 softtabstop=2 shiftwidth=2: 
# $Id$
last_notice_time = Time.at(0)

::CurrentSession.pre_process_timeline_statuses do |sender, e|
  if !e.is_first_time && e.statuses.status.size > 0
    d = ((Time.now - ::CurrentSession.Config.Interval) - 60) # 60秒の誤差は許す
    delay = ((d - e.statuses.status[-1].created_at) / 60).to_i

    if (delay > 0 && ((last_notice_time + (10 * 60)) < Time.now)) # 最後に通知してから10分たったら。
      ::CurrentSession.send_twitter_gateway_server_message("Twitterは現在約#{delay}分遅延しています。")
      last_notice_time = Time.now
    end
  end
end
