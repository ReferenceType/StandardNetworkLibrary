using System;

namespace NetworkLibrary.DistributedP2P.Components
{
    public interface ILogger
    {
        event Action<LogData> LogAvailable;

        void Log(LogType logType, string log);
        void Log(Exception ex);
        void SetAllowedOptions(LogType allowedLogTypes);
        string Stringify(LogData data);
    }
}