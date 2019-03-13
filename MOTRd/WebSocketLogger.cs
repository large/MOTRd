using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSockets.Common;
using System.Diagnostics;
using System.Threading;

namespace MOTRd
{
    internal class WebSocketLogger : IWebSocketLogger
    {
        public void Information(Type type, string format, params object[] args)
        {
            try
            {
                //format = format.Replace('{', '[');
                //format = format.Replace('}', ']');
                Trace.TraceInformation(format, args);
            }
            catch(Exception ex)
            {
                Trace.TraceError("Got exception writing exception: " + ex.ToString());
            }
        }

        public void Warning(Type type, string format, params object[] args)
        {
            Trace.TraceWarning(format, args);
        }

        public void Error(Type type, string format, params object[] args)
        {
            Trace.TraceError(format, args);
        }

        public void Error(Type type, Exception exception)
        {
            Error(type, "{0}", exception);
        }

        public void Debug(Type type, string message,
        [System.Runtime.CompilerServices.CallerMemberName] string memberName = "",
        [System.Runtime.CompilerServices.CallerFilePath] string sourceFilePath = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLineNumber = 0)
        {
            BooleanSwitch traceDebugSwitch = new BooleanSwitch("DebugTraceListener", "Shows extra debug info");
            if (!traceDebugSwitch.Enabled)
                return;

            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
            string plusOrMinus = (utcOffset < TimeSpan.Zero) ? "-" : "+";
            string utcHourOffset = utcOffset.TotalHours == 0 ? string.Empty : string.Format(" ({0}{1:hh})", plusOrMinus, utcOffset);
            string dateWithOffset = string.Format(@"{0:yyyy/MM/dd HH:mm:ss.fff}{1}", DateTime.Now, utcHourOffset);

            string log = string.Format(@"{0} [{1}] {2} [{4} -> {3}:{5}] (Debug)", dateWithOffset, Thread.CurrentThread.ManagedThreadId, message, memberName, sourceFilePath, sourceLineNumber);

            Trace.WriteLine(log);
        }
    }
}
