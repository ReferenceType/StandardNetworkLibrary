using System;

namespace NetworkLibrary.Utils
{
    public class MiniLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error };
        public static event Action<string> LogDebug;
        public static event Action<string> LogInfo;
        public static event Action<string> LogWarn;
        public static event Action<string> LogError;
        public static event Action<string> AllLog;

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