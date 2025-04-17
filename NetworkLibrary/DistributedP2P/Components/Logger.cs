using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace NetworkLibrary.DistributedP2P.Components
{
    [Flags]
    public enum LogType
    {
        Debug = 1,
        Info = 2,
        Warning = 4,
        Error = 8,
        Exception = 16
    }

    // readonly structs does not make defensive copies.
    public readonly struct LogData
    {
        public readonly long Id;
        public readonly LogType LogType;
        public readonly DateTime TimeStamp;
        public readonly string Log;

        public LogData(long id, LogType logType, DateTime timeStamp, string log)
        {
            Id = id;
            LogType = logType;
            TimeStamp = timeStamp;
            Log = log;
        }
    }


    public class Logger : ILogger
    {
        public event Action<LogData> LogAvailable;

        private LogType allowedLogTypes = LogType.Debug | LogType.Error | LogType.Warning | LogType.Info | LogType.Exception;
        private Dictionary<LogType, string> logTypeLookup;
        private long logId = 0;

        public void Log(LogType logType, string log)
        {
            if (!(allowedLogTypes.HasFlag(logType)))
            {
                return;
            }


            var id = Interlocked.Increment(ref logId);

            LogData data = new LogData(id: id,
                                       timeStamp: DateTime.UtcNow,
                                       logType: logType,
                                       log: log);

            try
            {
                LogAvailable?.Invoke(data);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"#[Critical Error] Logger failed! : \n{e.ToString()}");
            }

        }

        public void Log(Exception ex)
        {
            if (!(allowedLogTypes.HasFlag(LogType.Exception)))
            {
                return;
            }


            var id = Interlocked.Increment(ref logId);

            LogData data = new LogData(id: id,
                                       timeStamp: DateTime.UtcNow,
                                       logType: LogType.Exception,
                                       log: $"{ex.Message}\n{ex.StackTrace}");

            try
            {
                LogAvailable?.Invoke(data);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"#[Critical Error] Logger failed! : \n{e.ToString()}");
            }

        }

        public string Stringify(LogData data)
        {
            return $"#[{data.Id}][{data.TimeStamp}][{logTypeLookup[data.LogType]}] : {data.Log}";
        }


        /// <summary>
        /// allowedLogTypes = LogType.Debug | LogType.Error | LogType.Warning | LogType.Info | LogType.Exception
        /// </summary>
        /// <param name="logType"></param>
        public void SetAllowedOptions(LogType allowedLogTypes)
        {
            this.allowedLogTypes = allowedLogTypes;
        }

        // Constructed only once if anyone acesses this class(Thread safe).
        public Logger()
        {
            InitializeLookUpTable();

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionExit;
            AppDomain.CurrentDomain.ProcessExit += OnExit;
        }

        private void InitializeLookUpTable()
        {
            logTypeLookup = new Dictionary<LogType, string>()
            {
                {LogType.Info,"Info" },
                {LogType.Debug,"Debug" },
                {LogType.Warning,"Warning" },
                {LogType.Error,"Error" },
                {LogType.Exception,"Exception" },
            };
        }

        private void OnExit(object sender, EventArgs e)
        {
            Log(LogType.Info, "Application Exiting..");
        }

        private void UnhandledExceptionExit(object sender, UnhandledExceptionEventArgs e)
        {
            Log(LogType.Exception, $"Application Exit with an unhandled exception:" +
                $"\n{((Exception)e.ExceptionObject).Message}" +
                $"\nStack Trace:{((Exception)e.ExceptionObject).StackTrace}");
        }
    }
}
