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
| `/days:N` | Search the last N days of events. The default is 30. |
| `/all` | Search the retained logs instead of the day window. |

**Exit codes:**

| Code | Meaning |
|------|---------|
| `0` | The run finished |
| `1` | The arguments were not recognized, the scan failed, or `/s` could not write the zip |

A closed or timed-out relay is a finding in the report. It still exits with code `0`.

## Menu

```
1) Working directory: {current folder}
2) Export all relevant diagnostic data
3) Rerun network checks
4) Dump data and rerun all reports
5) Exit
```

Option 1 prints the working directory. That folder is where zip files and later reports are saved. It starts as the folder you launched the app from. Choosing option 1 also asks whether to change it. A new folder can be created if it does not exist yet.

### Export

Export saves into the working directory and asks for a file name. The default file name is:

```
{MachineName}_{yyyyMMdd_HHmmss}_diagnostics.zip
```

The zip contains:

| File | Contents |
|------|----------|
| `discovery-report.txt` | Installations, services, keys, files, relays, event count, and the latest network results |
| `events_{MachineName}_{yyyyMMdd_HHmmss}.csv` | Matching event log rows |
| `dxdiag.txt` | Full `dxdiag /t` output |
| `security-software.txt` | Antivirus, antispyware, and firewall products from Security Center |
| `network.txt` | Latest DNS and TCP result for every relay |
| `system-summary.txt` | Computer name, OS, architecture, manufacturer, model, CPU, and RAM |

`dxdiag` runs when you export. It can take about a minute.

### Network checks

Rerun repeats the DNS and TCP checks for each relay, prints the new results, and replaces the stored results. A later export uses those results.

### Rerun all reports

Dump data and rerun all reports clears the installations, events, and network results already collected, then scans the machine again. The new report replaces the previous one. A later export uses that new scan.

## What is collected

### Processes

- `ScreenConnect.ClientService.exe`
- `ScreenConnect.WindowsClient.exe`
- `ScreenConnect.WindowsBackstageShell.exe`

The report lists the process id and name.

### Services and clients

Items whose names start with `ScreenConnect Client`, including the thumbprint suffix. Each service includes status, image path, and relay.

### Relay address

The relay is read from the service `ImagePath`:

- `h=` is the host
- `p=` is the port
- When `p=` is missing, the port is **8041** and the report marks it as assumed

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
| Download files | `C:\Users\{User}\Downloads\` files containing `ScreenConnect` in the filename |

### Events

The Application and System logs are searched, plus any log whose name contains `ScreenConnect`.

An event is kept when it is Information, Error, or Critical and the event XML or message contains `ScreenConnect`. The default window is the last 30 days.

### Network

For each distinct relay host and port:

- DNS lookup, or a note when the relay is already an IP address
- A TCP connection with a 5 second timeout

The TCP result is **open**, **closed**, or **timed out**. The check is the connection itself. No ScreenConnect protocol is sent.

## Deferred: mdbg

Attaching the .NET Framework command-line debugger (`mdbg.exe`) is not part of this version. Attaching to `ScreenConnect.ClientService` needs administrator rights.

When that is added, the tool should look for an installed `MDbg.exe` on `PATH` or under the .NET Framework SDK tools folder and start it against a process id already listed in the report. The SDK binary is not bundled with this app.

## Reads that can be incomplete

These items are left out of this version because they need administrator rights:

- UAC re-launch
- The Security event log
- Process executable path and command line for Session 0 services
- Stopping or deleting anything

If a registry key, profile folder, event log, Security Center query, or hardware query cannot be read, that item is recorded and the rest of the run continues. A denied Security Center or hardware query is written into its zip file, and the rest of the package is still saved.

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

1. Run interactively and confirm the report lists processes, services, keys, files, relays, and an event count
2. Rerun network checks and confirm the relay DNS and port lines refresh
3. Choose dump data and rerun all reports, and confirm the scan time and results are new
4. Export a zip and confirm all six files are inside
5. Run with `/s` and confirm the zip is written next to the executable with no prompts
6. Run with `/s C:\Reports\machine.zip` and confirm that file is written
7. Run with `/days:1` and confirm the event window line
8. Run as a standard user and confirm there is no UAC prompt
