using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wiki.Models
{
    public class Constants
    {
        public static int DTEId = 0;
        public static int DTEIdM = 1;


        private static TraceSource _docLog;
        public static TraceSource DLog
        {
            get
            {
                if (_docLog == null)
                    InitLogger();
                return _docLog;
            }
        }

        private static void InitLogger()
        {
            _docLog = new TraceSource("Log");
            _docLog.Switch = new SourceSwitch("DocLogSourceSwitch", "All");
            _docLog.Listeners.Clear(); // .Remove("Default");

            TextWriterTraceListener tl = new TextWriterTraceListener("Documenter.log");
            tl.Filter = new EventTypeFilter(SourceLevels.Verbose);
            tl.Name = "DocumenterTextWriterTraceListener";

            ColorConsoleTraceListener c = new ColorConsoleTraceListener(false);
            c.Filter = new EventTypeFilter(SourceLevels.Information);
            c.Name = "DocumenterConsoleTraceListener";

            _docLog.Listeners.Add(c);
            _docLog.Listeners.Add(tl);

            Trace.AutoFlush = true;
        }

        public static void LogOff()
        {
            DLog.Switch.Level = SourceLevels.Off;
        }

        public static void LogOn()
        {
            DLog.Switch.Level = SourceLevels.All;
        }

        private static TextWriter defaultErr;
        private static TextWriter defaultOut;

        public static void DisableConsole()
        {
            defaultErr = defaultErr ?? Console.Error;
            defaultOut = defaultOut ?? Console.Out;
            Console.SetError(TextWriter.Null);
            Console.SetOut(TextWriter.Null);
        }

        public static void EnableConsole()
        {
            Console.SetError(defaultErr);
            Console.SetOut(defaultOut);
            defaultOut = null;
            defaultErr = null;
        }
    }

    public class ColorConsoleTraceListener : ConsoleTraceListener
    {
        Dictionary<TraceEventType, Tuple<ConsoleColor, ConsoleColor>> eventColor = new Dictionary<TraceEventType, Tuple<ConsoleColor, ConsoleColor>>();

        public ColorConsoleTraceListener(bool useErrorStream) : base(useErrorStream)
        {
            eventColor.Add(TraceEventType.Verbose, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.DarkGray, ConsoleColor.Black));
            eventColor.Add(TraceEventType.Information, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.Gray, ConsoleColor.Black));
            eventColor.Add(TraceEventType.Warning, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.Black, ConsoleColor.Yellow));
            eventColor.Add(TraceEventType.Error, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.White, ConsoleColor.Red));
            eventColor.Add(TraceEventType.Critical, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.White, ConsoleColor.DarkRed));
            eventColor.Add(TraceEventType.Start, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.DarkCyan, ConsoleColor.Black));
            eventColor.Add(TraceEventType.Stop, new Tuple<ConsoleColor, ConsoleColor>(ConsoleColor.DarkCyan, ConsoleColor.Black));
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            TraceEvent(eventCache, source, eventType, id, "{0}{1}{2}{3}", "[", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "] ", message);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string format, params object[] args)
        {
            ConsoleColor originalColor = Console.ForegroundColor;
            ConsoleColor originalBgColor = Console.BackgroundColor;

            Console.ForegroundColor = getEventColor(eventType, originalColor, id);
            Console.BackgroundColor = getEventBgColor(eventType, originalBgColor, id);
            base.TraceEvent(eventCache, source, eventType, id, format, args);
            Console.ForegroundColor = originalColor;
            Console.BackgroundColor = originalBgColor;
        }

        private ConsoleColor getEventBgColor(TraceEventType eventType, ConsoleColor defaultColor, int id)
        {
            if (id == Constants.DTEIdM)
                return ConsoleColor.Green;

            if (!eventColor.ContainsKey(eventType))
            {
                return defaultColor;
            }
            return eventColor[eventType].Item2;
        }

        private ConsoleColor getEventColor(TraceEventType eventType, ConsoleColor defaultColor, int id)
        {
            if (id == Constants.DTEIdM)
                return ConsoleColor.Black;

            if (!eventColor.ContainsKey(eventType))
            {
                return defaultColor;
            }
            return eventColor[eventType].Item1;
        }
    }
}
