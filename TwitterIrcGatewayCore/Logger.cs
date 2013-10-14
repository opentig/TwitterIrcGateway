using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Misuzilla.Applications.TwitterIrcGateway
{
    public abstract class Logger
    {
        public abstract void Error(String message);
        public abstract void Information(String message);
        public abstract void Warning(String message);

        public void Error(String format, params Object[] args)
        {
            Error(String.Format(format, args));
        }
        public void Information(String format, params Object[] args)
        {
            Information(String.Format(format, args));
        }
        public void Warning(String format, params Object[] args)
        {
            Warning(String.Format(format, args));
        }
    }
    
    public class TraceLogger : Logger
    {
        public static readonly TraceLogger Server = new TraceLogger("Server");
        public static readonly TraceLogger Twitter = new TraceLogger("Twitter");
        public static readonly TraceLogger Filter = new TraceLogger("Filter");
        
        protected TraceSource TraceSource { get; set; }
        public TraceLogger(String traceSourceName)
        {
            TraceSource = new TraceSource(traceSourceName, SourceLevels.All);
        }

        public override void Error(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Error, 0, message);
            TraceSource.Flush();
        }
        public override void Information(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Information, 0, message);
            TraceSource.Flush();
        }
        public override void Warning(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Warning, 0, message);
            TraceSource.Flush();
        }
    }

    public class SessionTraceLogger : TraceLogger
    {
        public Session CurrentSession { get; private set; }
        
        public SessionTraceLogger(Session session) : base("Session")
        {
            CurrentSession = session;
        }

        public override void Error(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Error, CurrentSession.TwitterUser.Id, message);
            TraceSource.Flush();
        }
        public override void Information(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Information, CurrentSession.TwitterUser.Id, message);
            TraceSource.Flush();
        }
        public override void Warning(string message)
        {
            TraceSource.TraceEvent(TraceEventType.Warning, CurrentSession.TwitterUser.Id, message);
            TraceSource.Flush();
        }
    }
}
