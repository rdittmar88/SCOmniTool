# SCOmniTool

A .NET Framework console application that reports ScreenConnect client installations, services, registry keys, files, and processes, then checks each agent relay. It reads and reports. It does not remove software or attach a debugger.

## Supported operating systems

- Windows 7 SP1
- Windows 10
- Windows 11
- Supported Windows Server releases (2008 R2 and later)

## Target frameworks

This project multi-targets:

| Framework | Purpose |
|-----------|---------|
| **.NET Framework 4.8** | Primary build; pre-installed on Windows 10/11 |
| **.NET Framework 4.6.2** | Fallback for Windows 7 SP1 systems without .NET 4.8 |

The executable is built as **x86** so it runs on both 32-bit and 64-bit Windows via WoW64.

## Requirements

- Windows operating system
- .NET Framework 4.6.2 or later (4.8 recommended)
- A standard user account is enough for the scans in this version

## Usage

```cmd
SCOmniTool.exe
```

The app will:

1. Scan for ScreenConnect processes, services, clients, registry keys, and files
2. Read each service `ImagePath` and take the relay host (`h=`) and port (`p=`)
3. Resolve each relay and try a TCP connection
4. Read matching Application, System, and ScreenConnect event logs
5. Print the report
6. Show a menu until you exit

### Arguments

| Argument | Meaning |
|----------|---------|
| `/s` | Print the report, write the diagnostic zip, and exit. The menu is skipped. With no path, the zip is saved next to the executable. |
| `/s [zip path]` | Same as `/s`, but the zip uses that file name and folder. Example: `SCOmniTool.exe /s C:\Reports\machine.zip`. A missing folder is created. `.zip` is added when the path does not already end with it. |
| `/days:N` | Search the last N days of events. The default is 10. |
| `/all` | Search the retained logs instead of the day window. |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | The run finished |
| `1` | The arguments were not recognized, the scan failed, or `/s` could not write the zip |

A closed or timed-out relay is a finding in the report. It still exits with code `0`.

## Menu

```
1) Change Working Directory: {current folder}
2) Export all relevant diagnostic data
3) Rerun network checks
4) Rerun configuration report
5) Dump data and rerun all reports
6) Check Defender actions
7) Exit
```

Option 1 prints the working directory and asks for a new folder. Press Enter to keep the current one. That folder is where zip files and later reports are saved. It starts as the folder you launched the app from. A new folder can be created if it does not exist yet.

Rerun configuration report reads `app.config`, `system.config`, and `user.config` again, replaces the stored configuration section, and prints that section. Processes, services, events, and network results stay as they are. A later export uses the new values.

Check Defender actions reads recent Microsoft Defender detections and actions whose details mention ScreenConnect. The opening report includes those actions only when SCOmniTool is already running as administrator. Otherwise this menu option asks for administrator approval, prints the actions, and keeps them for the next export.

### Export

Export saves into the working directory and asks for a file name. The default file name is:

```
{MachineName}_{yyyyMMdd_HHmmss}_diagnostics.zip
```

The screen shows a short summary of each category, including a system summary. The zip holds the full values.

The zip contains:

| File | Contents |
|------|----------|
| `dxdiag.txt` | Full `dxdiag /t` output |
| `eventviewerlogs.csv` | Matching event log rows |
| `full-report.txt` | Processes, services, clients, registry keys, file locations, configuration values, relays, active adapters, VPNs, proxies, antivirus, other security software, Defender actions, and the event count |
| `processes-report.txt` | Each ScreenConnect process with its path and start command, each service with status, image path, and relay, and the file locations |
| `anti-virus-report.txt` | Antivirus, antispyware, and firewall products from Security Center, other installed security software, plus Defender actions that mention ScreenConnect |
| `network-report.txt` | DNS and TCP result for every relay, active wired and wireless adapters, Windows VPN profiles, currently connected VPNs, and proxy settings |
| `configuration-report.txt` | Values from `app.config`, `system.config`, and `user.config` |
| `system-report.txt` | Computer name, OS, architecture, manufacturer, model, CPU, and RAM, plus installed display adapters, active monitors with resolution, orientation, and desktop arrangement, and a one-second snapshot of uptime, processor use, RAM use, disk bytes per second, disk queue length, and the top 5 processes by RAM and CPU. It is not a performance history. |

`dxdiag` runs when you export. It can take about a minute.

### Network checks

Rerun repeats the DNS and TCP checks for each relay and reads active wired and wireless adapters, Windows VPN profiles, currently connected VPNs, and proxy settings. It prints the network summary and replaces the stored results. A later export uses those results. These reads do not need administrator rights. A value that cannot be read is recorded and the rest of the check continues. Active VPNs are Windows VPN connections that are connected, plus network adapters that are up and look like a VPN. A third-party VPN that is installed but not connected, and that has no Windows VPN profile, is not listed.

### Rerun all reports

Dump data and rerun all reports clears the installations, events, network results, and configuration values already collected, then scans the machine again. The new report replaces the previous one. A later export uses that new scan.

## What is collected

### Processes

- `ScreenConnect.ClientService.exe`
- `ScreenConnect.WindowsClient.exe`
- `ScreenConnect.WindowsBackstageShell.exe`

The screen lists the process id as `PID`, a name that includes the install thumbprint when it can be read, and status (`Running` or `Not responding`). `processes-report.txt` and `full-report.txt` also include the executable path and the command used to start the process, each service with its status, image path, and relay, and the file locations. Download files are a count, not a list of every file. The thumbprint is the parenthetical suffix on the `ScreenConnect Client (...)` folder, so two clients are not both labeled only `ScreenConnect.ClientService`. If the path or command cannot be read, that field is shown as unavailable and the rest of the run continues. Session 0 services often hide the path and command from a standard user.

### Services and clients

Items whose names start with `ScreenConnect Client`, including the thumbprint suffix. Each service includes status, image path, and relay. The same uninstall key seen under both `SOFTWARE` and `WOW6432Node` counts as one client.

### Relay address

The relay is read from the service `ImagePath`:

- `h=` is the host
- `p=` is the port
- When `p=` is missing, the port is **8041** and the report marks it as assumed

### Network adapters, VPNs, and proxies

The screen lists each active wired or wireless adapter with one address, each Windows VPN profile as connected or not connected, each VPN that is connected now, and whether a user proxy, WinHTTP proxy, auto-config script, or environment proxy is set. `network-report.txt` and `full-report.txt` also include the adapter description, MAC address, speed, DHCP, addresses, gateways, and DNS servers, plus the VPN device, server, and phone book. Proxy passwords embedded in a URL are not written into the report.

### Registry keys

| Type | Location |
|------|----------|
| Service keys | `HKLM\SYSTEM\CurrentControlSet\Services\ScreenConnect Client (...)` |
| Product keys | `HKLM\SOFTWARE\Classes\Installer\Products\{guid}` where `ProductName` starts with `ScreenConnect Client` |
| Uninstall keys | `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{guid}` and the WOW6432Node equivalent where `DisplayName` starts with `ScreenConnect Client` |

### Files and folders

| Type | Location |
|------|----------|
| Program folders | `C:\Program Files (x86)\ScreenConnect Client (...)` |
| ClickOnce folders | `C:\Users\{User}\AppData\Local\Apps\2.0\` (entire tree per user) |
| User config folders | `C:\Users\{User}\AppData\Local\ScreenConnect Client (...)` |
| System config folders | `C:\Windows\SysWOW64\config\systemprofile\AppData\Local\ScreenConnect Client (...)` |
| System temp folders | `C:\Windows\SystemTemp\ScreenConnect\` |
| Download files | `C:\Users\{User}\Downloads\` files containing `ScreenConnect` in the filename. The report shows the count, not each file. |

### Configuration

The Configuration section on screen lists each file as found or not found. Setting values are written to `configuration-report.txt` and `full-report.txt`. A missing file is listed as not found.

| Directory | Files |
|-----------|-------|
| `C:\Program Files (x86)\ScreenConnect Client (...)` | `app.config`, `system.config` |
| `C:\Users\{User}\AppData\Local\ScreenConnect Client (...)` | `user.config` |
| `C:\Windows\SysWOW64\config\systemprofile\AppData\Local\ScreenConnect Client (...)` | `user.config` |
| `C:\ProgramData\ScreenConnect Client (...)` | `user.config` |

XML files are written as setting names and values. A file that is not XML is included as its text.

### Antivirus and other security software

Security Center reports each installed antivirus product. The product state is printed as enabled, disabled, snoozed, or expired, and whether definitions are up to date. When Windows Defender is not the active product and another antivirus is enabled, the report says Defender is deferred to that product. The original Security Center number stays in parentheses. The same section reports Windows Firewall: whether the firewall service is running, and whether the Domain, Private, and Public profiles are on or off. The active profile is marked current. This read does not need administrator rights.

A second section matches installed programs and Windows services against expected antivirus, EDR, XDR, application control, firewall, DLP, web security, and related product names. That catches agents that do not register in Security Center. Products already shown in the Antivirus section are not repeated. The screen shows the product, category, and a service or install name. `anti-virus-report.txt` and `full-report.txt` also include the version, publisher, install location, and service status. A product that is not in the expected list is not shown. These reads do not need administrator rights.

### Events

The Application and System logs are searched, plus any log whose name contains `ScreenConnect`.

An event is kept when it is Information, Error, or Critical and the event XML or message contains `ScreenConnect`. The default window is the last 10 days. `/days:N` changes that window, and `/all` searches the retained logs. Every event in that window is read, including its full message. `eventviewerlogs.csv` marks `MessageOnly` as `yes` when ScreenConnect is in the message and not in the XML. The event section shows how many of those matches were found.

### Network

For each distinct relay host and port:

- DNS lookup, or a note when the relay is already an IP address
- A TCP connection with a 5 second timeout

The TCP result is **open**, **closed**, or **timed out**. The check is the connection itself. No ScreenConnect protocol is sent. A firewall may still block ScreenConnect-specific traffic when a port shows open.

## Deferred: mdbg

Attaching the .NET Framework command-line debugger (`mdbg.exe`) is not part of this version. Attaching to `ScreenConnect.ClientService` needs administrator rights.

When that is added, the tool should look for an installed `MDbg.exe` on `PATH` or under the .NET Framework SDK tools folder and start it against a process id already listed in the report. The SDK binary is not bundled with this app.

## Reads that can be incomplete

These items are left out of this version because they need administrator rights:

- UAC re-launch
- The Security event log
- Stopping or deleting anything

If a registry key, profile folder, event log, process path, process command, Security Center query, or hardware query cannot be read, that item is recorded and the rest of the run continues. A denied Security Center or hardware query is written into its zip file, and the rest of the package is still saved.

## Building

Requires the .NET SDK with .NET Framework targeting packs installed (typically on Windows with Visual Studio):

```cmd
dotnet build SCOmniTool.sln -c Release
```

Build outputs:

- `SCOmniTool/bin/Release/net48/SCOmniTool.exe`
- `SCOmniTool/bin/Release/net462/SCOmniTool.exe`

## Manual testing checklist

Run on a Windows machine with ScreenConnect installed:

1. Run interactively and confirm the report lists processes, services, keys, files, relays, antivirus, and an event count
2. Rerun network checks and confirm the relay DNS and port lines refresh
3. Choose dump data and rerun all reports, and confirm the scan time and results are new
4. Export a zip and confirm all seven files are inside
5. Run with `/s` and confirm the zip is written next to the executable with no prompts
6. Run with `/s C:\Reports\machine.zip` and confirm that file is written
7. Run with `/days:1` and confirm the event window line
8. Run as a standard user and confirm the opening scan does not show a UAC prompt
9. Choose Check Defender actions, approve the prompt, and confirm matching Defender actions print
