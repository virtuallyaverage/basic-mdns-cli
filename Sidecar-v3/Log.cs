namespace SidecarV3;

public class Log
{
    public enum LogType
    {
        DBUG, // enabled with --debug flag
        EROR, // caused the system to fail
        WARN, // will be presented, but don't contain info
        _ADD, // device added + new device info.
        _RMV, // device removed + old device info.
        _CHG, // device details updated + new device info (has the same MAC address)
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