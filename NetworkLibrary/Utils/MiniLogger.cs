using System;
using System.Collections.Generic;
using System.Text;

namespace NetworkLibrary.Utils
{
    public class MiniLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error };
        public static Action<string> LogDebug;
        public static Action<string> LogInfo;
        public static Action<string> LogWarn;
        public static Action<string> LogError;
        public static Action<string> AllLog;

        public static void Log(LogLevel level, string message)
        {
            switch (level)
            {
                case LogLevel.Debug:
                    LogDebug?.Invoke(message);
                    AllLog?.Invoke("[Debug] " + message);
                    break;
                case LogLevel.Info:
                    LogInfo?.Invoke(message);
                    AllLog?.Invoke("[Info] " + message);

                    break;
                case LogLevel.Warning:
                    LogWarn?.Invoke(message);
                    AllLog?.Invoke("[Warning] " + message);

                    break;
                case LogLevel.Error:
                    LogError?.Invoke(message);
                    AllLog?.Invoke("[Error] " + message);

                    break;
            }

        }
    }
}