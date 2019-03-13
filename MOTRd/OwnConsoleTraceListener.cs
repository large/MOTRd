using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Runtime.CompilerServices;
using System.IO;

namespace MOTRd
{
    public class OwnConsoleTraceListener : TraceListener
    {
        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            BooleanSwitch traceDebugSwitch = new BooleanSwitch("DebugTraceListener", "Shows extra debug info");

            string message = string.Format(format, args);

            // write the localised date and time but include the time zone in brackets (good for combining logs from different timezones)
            TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);
            string plusOrMinus = (utcOffset < TimeSpan.Zero) ? "-" : "+";
            string utcHourOffset = utcOffset.TotalHours == 0 ? string.Empty : string.Format(" ({0}{1:hh})", plusOrMinus, utcOffset);
            string dateWithOffset = string.Format(@"{0:yyyy/MM/dd HH:mm:ss.fff}{1}", DateTime.Now, utcHourOffset);

            //Get the class that called the trace and linenumber
            int nMotrCount = 0;

            string[] test2 = eventCache.Callstack.Split('\n');

            for (int i = 0; i < test2.Count(); i++)
            {
                if (test2[i].Contains("MOTRd"))
                    nMotrCount++;

                //Every third event
                if (nMotrCount == 3)
                {
                    nMotrCount = i;
                    break;
                }
            }

            string[] test3 = test2[nMotrCount].Split(' ');
            string test4 = test3[test3.Count() - 1].Replace("\r", "");

            string test = this.GetType().Name;

        // display the threadid
            string log = string.Format(@"{0} [{1}] {2}", dateWithOffset, Thread.CurrentThread.ManagedThreadId, message);

            if (traceDebugSwitch.Enabled)
                log = string.Format(@"{0} [{1}] {2} [{3}:{4}]", dateWithOffset, Thread.CurrentThread.ManagedThreadId, message, test3[4], test4);

            switch (eventType)
            {
                case TraceEventType.Critical:
                case TraceEventType.Error:
                    if (Environment.UserInteractive)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Error: " + log);
                        Console.ResetColor();
                    }
                    else
                        WriteToLogFile("Error: " + log);

                    break;

                case TraceEventType.Warning:
                    if (Environment.UserInteractive)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Warning: " + log);
                        Console.ResetColor();
                    }
                    else
                        WriteToLogFile("Warning: " + log);
                    break;

                default:
                    if (Environment.UserInteractive)
                        Console.WriteLine(log);
                    else
                        WriteToLogFile(log);
                    break;
            }
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            this.TraceEvent(eventCache, source, eventType, id, message, new object[] {});
        }

        public override void WriteLine(string message)
        {
            if (Environment.UserInteractive)
                Console.WriteLine(message);
            else
                WriteToLogFile(message);
        }

        public override void Write(string message)
        {
            if (Environment.UserInteractive)
                Console.Write(message);
            else
                WriteToLogFile(message);
        }

        //Writes into a log-file instead
        private void WriteToLogFile(string message)
        {
            string baseFolder = MOTR_Settings.GetGlobalApplicationPath();
            System.IO.File.AppendAllText(baseFolder + @"\console.log", message + Environment.NewLine);
        }
    }
}
