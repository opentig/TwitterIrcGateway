設定について
============
このアプリケーションは中継サーバ側の設定は TwitterIrcGateway.exe.config に記述し、
twitter に関する設定はIRCクライアントに対して行います。


IRCクライアントの設定
=====================
・ユーザID:
	ニックネームと同じなど適当に
・RealNameとか名前などと呼ばれるところ:
	アカウントのメールアドレス/ログインID(スクリーンネーム)
・ニックネーム:
	twitter のアカウントのスクリーンネーム (ex: http://twitter.com/username → username)
・パスワード
	twitter のパスワード


IRCサーバもどきの設定 (TwitterIrcGateway.exe.config)
====================================================
設定を変更する場合は TwitterIrcGateway.exe.config.sample を TwitterIrcGateway.exe.config にリネームしてください。

設定できる項目は以下のページを参考にしてください。
http://www.misuzilla.org/dist/net/twitterircgateway/options


フィルタについて
================
フィルタの設定はアプリケーションと同じディレクトリの Configs\<id>\Filters.xml に書くことで有効となります。
Filters.xml.sample を参考に設定を行ってください。


グルーピングについて
====================
JOINでチャンネルを作成し、そこにINVITEでユーザを呼び込むことで呼び込んだユーザの発言のみが見えるようになります。
もし、グループからユーザをはずしたい場合には対象のユーザをチャンネルからKICKしてください。
チャンネルに自分以外がいなくなった状態でPARTするとそのグループは破棄されます。

グルーピングの設定はアプリケーションと同じディレクトリの Configs\<id>\Groups.xml に保存されます。


終了について
============
タスクトレイにあるアイコンのコンテキストメニューより終了してください。

$Id: Readme.txt 368 2008-03-16 10:09:00Z tomoyo $