namespace SidecarV3;

public class Log
{
    public enum LogType
    {
        DBUG,
        EROR,
        WARN,
        _ADD, // device added
        _RMV, // device removed
        _CHG, // device details updated
        
    }

    public class Logger
    {
        private static bool _debug;
        public Logger(bool debugMode = true)
        {
            _debug = debugMode;
            
        }
        public void LogCL(string message, LogType type)
        {
            if (!_debug && LogType.DBUG == type) return; //skip debug messages if not in debug mode
            
            Console.WriteLine($"{type}:{message}");
        }
    }
}