module Misuzilla::IronRuby
  module TypableMap
    include Misuzilla::Applications::TwitterIrcGateway::AddIns::TypableMap

    @@commands = []

    def self.setup
      @@typablemap_proc = ::CurrentSession.AddInManager.GetAddIn(Misuzilla::Applications::TwitterIrcGateway::AddIns::TypableMapSupport.to_clr_type).TypableMapCommands

      # スクリプトアンロード時にコマンドを削除する
      ::CurrentSession.AddInManager.GetAddIn(Misuzilla::Applications::TwitterIrcGateway::AddIns::DLRIntegration::DLRIntegrationAddIn.to_clr_type).BeforeUnload do |sender, e|
        @@commands.each do |command|
          @@typablemap_proc.RemoveCommand(command)
        end
      end
    end

    def self.register(command, desc, &proc_cmd)
      @@commands << command
      @@typablemap_proc.AddCommand(command, desc, ProcessCommand.new{|p, msg, status, args|
        proc_cmd.call(p, msg, status, args)
      })
    end

    setup
  end
end

# TypableMap: test コマンドを追加する
Misuzilla::IronRuby::TypableMap.register("test", "Test Command") do |p, msg, status, args|
  System::Diagnostics::Trace.WriteLine("Test: #{status.to_string}")

  true # true を返すとハンドルしたことになりステータス更新処理は行われない
end

# TypableMap: rt コマンドを追加する
Misuzilla::IronRuby::TypableMap.register("rt", "ReTweet Command") do |p, msg, status, args|
  System::Diagnostics::Trace.WriteLine("RT: #{status.to_string}")

  ::CurrentSession.RunCheck(Misuzilla::Applications::TwitterIrcGateway::Procedure.new{
    updated_status = ::CurrentSession.update_status("RT: #{status.text} (via @#{status.user.screen_name})")
    ::CurrentSession.send_channel_message(updated_status.text)
  }, System::Action[System::Exception].new{|ex|
    ::CurrentSession.send_channel_message(msg.receiver, Server.server_nick, "メッセージ送信に失敗しました", false, false, true)
  })

  true # true を返すとハンドルしたことになりステータス更新処理は行われない
end