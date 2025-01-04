using System.Collections.Concurrent;
using System.Diagnostics;
using Zeroconf;

namespace SidecarV3
{
    /// <summary>
    /// The main program class.
    /// </summary>
    class Program
    {
        static Log.Logger _logger = new Log.Logger();
        private static int _parentProcessId;
        static bool _debugMode;
        private static string? _mdnsId;

        // Dictionary to track services by MAC address
        static readonly ConcurrentDictionary<string, Service> _knownDevices =
            new ConcurrentDictionary<string, Service>();

        /// <summary>
        /// The application entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static void Main(string[] args)
        {
            if (!ParseCli()) return;

            while (true)
            {
                var outputs = ProbeFor();
                outputs.Wait();
                
                // If parentProcessId is zero, do not auto-close
                if (_parentProcessId == 0) continue;
                
                // autoclose if parent process is done
                try
                {
                    var parentProcess = Process.GetProcessById(_parentProcessId);
                    if (!parentProcess.HasExited) break;
                    _logger.LogCL("Parent process has exited. Terminating sidecar.", Log.LogType.WARN);
                }
                catch (ArgumentException)
                {
                    _logger.LogCL("Parent PID Invalid. Terminating sidecar.", Log.LogType.EROR);
                    break;
                }
            }
            
            bool ParseCli()
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("DBUG:Usage: <ParentProcessId> <MdnsID> [--debug]", Log.LogType.DBUG);
                    Console.WriteLine("WARN:Exiting: Not enough arguments.");
                    return false;
                }

                // Simple check for debug mode
                _debugMode = args.Any(a => a.Equals("--debug", StringComparison.OrdinalIgnoreCase));
                _logger = new Log.Logger(_debugMode);
                _logger.LogCL($"Logging with debug mode.", Log.LogType.DBUG);

                // Extract filters (exclude the debug flag)
                _mdnsId = args[1];
                _logger.LogCL($"Using MdnsID: {_mdnsId}", Log.LogType.DBUG);

                // Attempt to parse parent process ID
                if (!int.TryParse(args[0], out _parentProcessId))
                {
                    _logger.LogCL("Invalid parent process ID.", Log.LogType.EROR);
                    return false;
                }
                else if (_parentProcessId == 0)
                {
                    _logger.LogCL("Parent process ID is zero; will not close program automatically.", Log.LogType.DBUG);
                }
                
                return true;
            }
        }

        /// <summary>
        /// Scans for devices using mDNS, updates known devices, and handles new/removed/updated devices.
        /// </summary>
        /// <param name="domainName">The mDNS domain to query.</param>
        /// <returns>A task representing the async mDNS resolution.</returns>
        static async Task ProbeFor(string domainName = "_haptics._udp.local.")
        {
            IReadOnlyList<IZeroconfHost> results = await ZeroconfResolver.ResolveAsync(domainName);
            Dictionary<string, Service> newlyDiscovered = new Dictionary<string, Service>();

            // Collect services from all hosts
            foreach (var host in results)
            {
                foreach (var svc in host.Services.Values)
                {
                    var port = svc.Port;
                    var ttl = svc.Ttl;

                    var properties = svc.Properties.FirstOrDefault();
                    if (properties == null)
                    {
                        _logger.LogCL($"Device {host.DisplayName} Is detected but has no properties.", Log.LogType.WARN);
                        continue;
                    }

                    var mac = properties.FirstOrDefault(r => r.Key == "MAC").Value;
                    var ip = properties.FirstOrDefault(r => r.Key == "IP").Value ?? host.IPAddress;

                    if (string.IsNullOrWhiteSpace(mac))
                    {
                        _logger.LogCL($"Device {host.DisplayName} has IP:{ip} and is missing a MAC address.", Log.LogType.DBUG);
                        continue;
                    }

                    var newService = new Service(mac, ip, host.DisplayName, port, ttl);
                    newlyDiscovered[mac] = newService;
                }
            }

            // Update or add newly discovered devices
            foreach (var kv in newlyDiscovered)
            {
                var mac = kv.Key;
                var newService = kv.Value;

                if (_knownDevices.TryGetValue(mac, out var existingService))
                {
                    if (existingService.HasChanged(newService.IP, newService.Port, newService.TTL))
                    {
                        existingService.UpdateService(newService.IP, newService.Port, newService.TTL);
                        OnServiceUpdated(mac, existingService);
                    }
                }
                else
                {
                    _knownDevices[mac] = newService;
                    OnNewDeviceDiscovered(newService);
                }
            }

            // Remove devices no longer present
            var knownMacs = _knownDevices.Keys.ToArray();
            foreach (var mac in knownMacs)
            {
                if (!newlyDiscovered.ContainsKey(mac))
                {
                    if (_knownDevices.TryRemove(mac, out var removed))
                    {
                        OnDeviceRemoved(removed);
                    }
                }
            }
        }

        /// <summary>
        /// Invoked when a new device is discovered.
        /// </summary>
        /// <param name="service">The newly discovered device's information.</param>
        static void OnNewDeviceDiscovered(Service service)
        {
            _logger.LogCL(service.ToString(), Log.LogType._ADD);
        }

        /// <summary>
        /// Invoked when a tracked device changes IP, port, or TTL.
        /// </summary>
        /// <param name="mac">The MAC address of the updated device.</param>
        /// <param name="service">Updated service information.</param>
        static void OnServiceUpdated(string mac, Service service)
        {
            _logger.LogCL(service.ToString(), Log.LogType._CHG);
        }

        /// <summary>
        /// Invoked when a tracked device no longer responds.
        /// </summary>
        /// <param name="service">The removed device information.</param>
        static void OnDeviceRemoved(Service service)
        {
            _logger.LogCL(service.ToString(), Log.LogType._RMV);
        }

        /// <summary>
        /// Service model class.
        /// </summary>
        class Service
        {
            public string MAC { get; private set; }
            public string IP { get; private set; }
            public string DisplayName { get; private set; }
            public int Port { get; private set; }
            public int TTL { get; private set; }

            /// <summary>
            /// Creates a new Service instance with the specified parameters.
            /// </summary>
            /// <param name="mac">The MAC address.</param>
            /// <param name="ip">The IP address.</param>
            /// <param name="displayName">A friendly name for the device.</param>
            /// <param name="port">The device port.</param>
            /// <param name="ttl">Time To Live for the record.</param>
            public Service(string mac, string ip, string displayName, int port, int ttl)
            {
                MAC = mac;
                IP = ip;
                DisplayName = displayName;
                Port = port;
                TTL = ttl;
            }

            /// <summary>
            /// Checks if IP, port, or TTL have changed compared to this instance.
            /// </summary>
            /// <param name="newIp">New IP address.</param>
            /// <param name="newPort">New port number.</param>
            /// <param name="newTtl">New TTL value.</param>
            /// <returns>True if any property changed, otherwise false.</returns>
            public bool HasChanged(string newIp, int newPort, int newTtl)
            {
                return IP != newIp || Port != newPort || TTL != newTtl;
            }

            /// <summary>
            /// Updates this instance with a new IP, port, and TTL.
            /// </summary>
            /// <param name="newIp">New IP address.</param>
            /// <param name="newPort">New port number.</param>
            /// <param name="newTtl">New TTL value.</param>
            public void UpdateService(string newIp, int newPort, int newTtl)
            {
                IP = newIp;
                Port = newPort;
                TTL = newTtl;
            }

            /// <summary>
            /// Returns a JSON-like string representation of the service.
            /// </summary>
            /// <returns>A string displaying the service properties in JSON format.</returns>
            public override string ToString()
            {
                return $"{{ \"MAC\": \"{MAC}\", \"IP\": \"{IP}\", \"DisplayName\": \"{DisplayName}\", \"Port\": {Port}, \"TTL\": {TTL} }}";
            }
        }
    }
}
