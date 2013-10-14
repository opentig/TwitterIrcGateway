using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using IronPython.Hosting;
using IronRuby.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting.Providers;
using Microsoft.Scripting.Hosting.Shell;
using Microsoft.Scripting.Runtime;
using Misuzilla.Applications.TwitterIrcGateway.AddIns.Console;

namespace Misuzilla.Applications.TwitterIrcGateway.AddIns.DLRIntegration
{
    [Description("DLR統合 コンテキストに切り替えます")]
    public class DLRContext : Context
    {
        public override ICollection<ContextInfo> Contexts { get { return (IsEvalEnabled ? new ContextInfo[] { new ContextInfo(typeof(IpyContext)), new ContextInfo(typeof(IrbContext)) } : new ContextInfo[0]); } }
        public Boolean IsEvalEnabled { get { return File.Exists(Path.Combine(Session.UserConfigDirectory, "EnableDLRDebug")); } }

        [Description("読み込まれているスクリプトを一覧表示します")]
        public void List()
        {
            DLRIntegrationAddIn addIn = CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            if (addIn.ScriptScopes.Keys.Count == 0)
            {
                Console.NotifyMessage("スクリプトは現在読み込まれていません。");
                return;
            }

            foreach (var key in addIn.ScriptScopes.Keys)
            {
                Console.NotifyMessage(key);
            }
        }

        [Description("スクリプトを再読み込みします")]
        public void Reload()
        {
            Console.NotifyMessage("スクリプトを再読み込みします。");
            CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>().ReloadScripts((fileName, ex) =>
            {
                Console.NotifyMessage("ファイル " + fileName + " を読み込みました。");
                if (ex != null)
                {
                    Console.NotifyMessage("実行時にエラーが発生しました:");
                    Console.NotifyMessage(ex.Message);
                    if (ex is SyntaxErrorException)
                    {
                        SyntaxErrorException syntaxEx = ex as SyntaxErrorException;
                        Console.NotifyMessage(String.Format("  行: {0}, 列 {1}, ファイル: {2}", syntaxEx.Line, syntaxEx.Column, syntaxEx.SourcePath));
                    }
                }
            });
            Console.NotifyMessage("スクリプトを再読み込みしました。");
        }

        [Description("現在のスクリプトスコープでスクリプトを評価します")]
        public void Eval([Description("言語名またはスクリプトエンジンの名前")]
                         String languageName,
                         [Description("評価する式")]
                         String expression)
        {
            if (IsEvalEnabled)
            {
                Object retVal = CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>().Eval(languageName, expression);
                Console.NotifyMessage(retVal == null ? "(null)" : retVal.ToString());
            }
            else
            {
                Console.NotifyMessage("Eval コマンドは現在無効化されています。");
                //ConsoleAddIn.NotifyMessage("ユーザ設定ディレクトリに EnableDLRDebug ファイルを作成することで有効になります。");
            }
        }
    }

    [Description("IronPython コンソールコンテキストに切り替えます")]
    public class IpyContext : Context
    {
        private Thread _consoleThread;
        private PythonCommandLine _pythonCommandLine;
        private VirtualConsole _virtualConsole;

        public override void Initialize()
        {
            DLRIntegrationAddIn addIn = CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _virtualConsole = new VirtualConsole(CurrentSession, Console);
            addIn.ScriptRuntime.IO.SetOutput(MemoryStream.Null, _virtualConsole.Output);
            addIn.ScriptRuntime.IO.SetErrorOutput(MemoryStream.Null, _virtualConsole.Output);
            _pythonCommandLine = new PythonCommandLine();//(CurrentServer, CurrentSession);
            PythonConsoleOptions consoleOptions = new PythonConsoleOptions();
            addIn.ScriptRuntime.Globals.SetVariable("Session", CurrentSession);
            addIn.ScriptRuntime.Globals.SetVariable("CurrentSession", CurrentSession);
            addIn.ScriptRuntime.Globals.SetVariable("Server", CurrentServer);
            addIn.ScriptRuntime.Globals.SetVariable("CurrentServer", CurrentServer);

            _consoleThread = new Thread(t =>
            {
                _pythonCommandLine.Run(addIn.ScriptRuntime.GetEngine("py"), _virtualConsole, consoleOptions);
            });
            _consoleThread.Start();
            Thread.Sleep(1000);
            _pythonCommandLine.ScriptScope.SetVariable("Session", CurrentSession);
            _pythonCommandLine.ScriptScope.SetVariable("CurrentSession", CurrentSession);
            _pythonCommandLine.ScriptScope.SetVariable("Server", CurrentServer);
            _pythonCommandLine.ScriptScope.SetVariable("CurrentServer", CurrentServer);


            base.Initialize();
        }

        public override bool OnPreProcessInput(string inputLine)
        {
            if (inputLine.Trim().ToLower() == "exit")
                return false;

            _virtualConsole.SetLine((inputLine == " ") ? "" : inputLine);
            return true;
        }

        [Browsable(false)]
        public override void Help(string commandName)
        {
        }

        [Description("IronPython コンソールを終了します")]
        public new void Exit()
        {
            base.Exit();
        }

        public override void Dispose()
        {
            if (_consoleThread != null)
            {
                _consoleThread.Abort();
                _consoleThread = null;
            }
            base.Dispose();
        }
    }


    [Description("IronRuby コンソールコンテキストに切り替えます")]
    public class IrbContext : Context
    {
        private Thread _consoleThread;
        private CommandLine _commandLine;
        private VirtualConsole _virtualConsole;

        public override void Initialize()
        {
            DLRIntegrationAddIn addIn = CurrentSession.AddInManager.GetAddIn<DLRIntegrationAddIn>();
            _virtualConsole = new VirtualConsole(CurrentSession, Console);
            addIn.ScriptRuntime.IO.SetOutput(MemoryStream.Null, _virtualConsole.Output);
            addIn.ScriptRuntime.IO.SetErrorOutput(MemoryStream.Null, _virtualConsole.Output);
            HostingHelpers.GetDomainManager(addIn.ScriptRuntime).SharedIO.SetOutput(new VirtualStream(Console), _virtualConsole.Output);
            HostingHelpers.GetDomainManager(addIn.ScriptRuntime).SharedIO.SetErrorOutput(new VirtualStream(Console), _virtualConsole.Output);
            _commandLine = new RubyCommandLine();//(CurrentServer, CurrentSession);
            ConsoleOptions consoleOptions = new RubyConsoleOptions();
            addIn.ScriptRuntime.Globals.SetVariable("Session", CurrentSession);
            addIn.ScriptRuntime.Globals.SetVariable("CurrentSession", CurrentSession);
            addIn.ScriptRuntime.Globals.SetVariable("Server", CurrentServer);
            addIn.ScriptRuntime.Globals.SetVariable("CurrentServer", CurrentServer);

            _consoleThread = new Thread(t =>
            {
                _commandLine.Run(addIn.ScriptRuntime.GetEngine("rb"), _virtualConsole, consoleOptions);
            });
            _consoleThread.Start();

            base.Initialize();
        }

        public override bool OnPreProcessInput(string inputLine)
        {
            if (inputLine.Trim().ToLower() == "exit")
                return false;

            _virtualConsole.SetLine((inputLine == " ") ? "" : inputLine);
            return true;
        }

        [Browsable(false)]
        public override void Help(string commandName)
        {
        }

        [Description("IronRuby コンソールを終了します")]
        public new void Exit()
        {
            base.Exit();
        }

        public override void Dispose()
        {
            if (_consoleThread != null)
            {
                _consoleThread.Abort();
                _consoleThread = null;
            }
            base.Dispose();
        }
    }


    internal class VirtualWriter : TextWriter
    {
        private Console.Console _console;
        public override Encoding Encoding { get { return Encoding.UTF8; } }
        public override void Write(string value)
        {
            if (!String.IsNullOrEmpty(value.Trim()))
                _console.NotifyMessage(value);
        }

        public VirtualWriter(Console.Console console)
        {
            _console = console;
        }
    }
    internal class VirtualStream : Stream
    {
        private Console.Console _console;
        private MemoryStream _buffer;

        public VirtualStream(Console.Console console)
        {
            _console = console;
            _buffer = new MemoryStream();
        }


        public override bool CanRead
        {
            get { return false; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            _console.NotifyMessage(Encoding.UTF8.GetString(_buffer.ToArray()));
            _buffer.SetLength(0);
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (buffer[i] == 0x0A) // LF
                {
                    Flush();
                }
                _buffer.WriteByte(buffer[i]);
            }
        }
    }

    internal class VirtualConsole : IConsole
    {
        private Session _session;
        private Console.Console _console;
        private String _line;
        private ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private VirtualWriter _writer;

        public VirtualConsole(Session session, Console.Console console)
        {
            _writer = new VirtualWriter(console);
            _session = session;
            _console = console;
        }

        public void SetLine(String line)
        {
            _line = line;
            _resetEvent.Set();
        }

        #region IConsole メンバ

        public TextWriter ErrorOutput
        {
            get { return _writer; }
            set { }
        }

        public TextWriter Output
        {
            get { return _writer; }
            set { }
        }

        public string ReadLine(int autoIndentSize)
        {
            _resetEvent.WaitOne();
            _resetEvent.Reset();
            return _line;
        }

        public void Write(string text, Style style)
        {
            // TODO: style
            _console.NotifyMessage(text);
        }

        public void WriteLine()
        {
            _console.NotifyMessage(" ");
        }

        public void WriteLine(string text, Style style)
        {
            // TODO: style
            _console.NotifyMessage(text);
        }

        #endregion
    }
}
