# How to Use: DEPRECATED, no longer needed
Download the executable from the releases and run it in powershell or a command prompt. 
Setting the ParentProcessId to 0 disables the parent tracking functionality.
```
Usage: <ParentProcessId> <MdnsID> [--debug]
``` 
**Example**:

To find haptic devices:

`./mdns-cli-<version> 0 "_haptics._udp.local" --debug`

## Output:

This sidecar outputs in the format: `<log_type>:<log info> ` with the flags:
```csharp
public enum LogType
    {
        DBUG, // enabled with --debug flag
        EROR, // caused the system to fail
        WARN, // will be presented, but don't contain info
        _ADD, // device added + new device info.
        _RMV, // device removed + old device info.
        _CHG, // device details updated + new device info (has the same MAC address)
    }
```
All flags that contain info are returned in `.json` format immediately after the semicolon symbol

Example output:
``` 
DBUG:Logging with debug mode True
DBUG:Using MdnsID: ._haptics._udp.local
DBUG:Parent process ID is zero; will not close program automatically.
_ADD:{ "MAC": "7C:2C:67:C8:A4:64", "IP": "192.168.1.101", "DisplayName": "vest_b", "Port": 1027, "TTL": 120 }
_ADD:{ "MAC": "7C:2C:67:C9:3E:70", "IP": "192.168.1.100", "DisplayName": "vest_f", "Port": 1027, "TTL": 120 }
```

This program keeps track of devices by their MAC address reported in a TXT packet. 
Because of that, it is required that a device reports its mac address keyed by `MAC` e.g:`MAC=7C:2C:67:C9:3E:70`
Enable the debug flag to get all detected devices reported.


## Implementations
Rust:
```rs
use std::process::{Command, Stdio};
fn main() {
let output = Command::new("path/to/mdns-cli")
.arg("0")
.arg("_haptics._udp.local")
.output()
.expect("Failed to execute command");
println!("stdout: {}", String::from_utf8_lossy(&output.stdout));
}
```

Typescript:
```ts
const { exec } = require('child_process');
exec(`path/to/mdns-cli 0 _haptics._udp.local`, (error, stdout, stderr) => {
  if (error) console.error(`Error: ${error.message}`);
  else console.log(stdout);
});
```

Python:
```python
import subprocess
result = subprocess.run(['path/to/mdns-cli', '0', '_haptics._udp.local'], capture_output=True)
```

