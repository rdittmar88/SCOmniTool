using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SCOmniTool.Models;

namespace SCOmniTool.Network;

internal static class NetworkEnvironmentProbe
{
    private const int RasErrorBufferTooSmall = 603;
    private const int WinHttpAccessTypeNoProxy = 1;
    private const int WinHttpAccessTypeNamedProxy = 3;

    private static readonly string[] ProxyEnvironmentNames =
    {
        "http_proxy",
        "https_proxy",
        "all_proxy",
        "ftp_proxy",
        "no_proxy"
    };

    private static readonly string[] VpnAdapterHints =
    {
        "vpn",
        "anyconnect",
        "globalprotect",
        "global protect",
        "forti",
        "wireguard",
        "wintun",
        "tap-windows",
        "tap-win",
        "openvpn",
        "nordlynx",
        "nordvpn",
        "pulse secure",
        "ivanti",
        "zscaler",
        "tailscale",
        "zerotier",
        "softether",
        "check point",
        "sonicwall",
        "netmotion",
        "netskope",
        "palo alto",
        "wan miniport (ike",
        "wan miniport (sstp",
        "wan miniport (l2tp",
        "wan miniport (pptp"
    };

    private static readonly string[] IgnoredTunnelHints =
    {
        "teredo",
        "isatap",
        "6to4",
        "iphttps",
        "ip-https"
    };

    public static NetworkEnvironment Check()
    {
        var environment = new NetworkEnvironment();
        ReadAdapters(environment);
        var activeNames = ReadActiveRasNames(environment);
        ReadVpnProfiles(environment, activeNames);
        ReadActiveVpns(environment, activeNames);
        environment.Proxy = ReadProxy();
        return environment;
    }

    private static void ReadAdapters(NetworkEnvironment environment)
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Select(adapter => TryReadAdapter(adapter))
                .Where(adapter => adapter != null)
                .Cast<NetworkAdapterInfo>()
                .OrderBy(adapter => adapter.Kind, StringComparer.Ordinal)
                .ThenBy(adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            environment.ActiveAdapters.AddRange(adapters);
        }
        catch (Exception ex)
        {
            environment.AdapterError = "Could not read network adapters: " + ex.Message;
        }
    }

    private static NetworkAdapterInfo? TryReadAdapter(NetworkInterface adapter)
    {
        try
        {
            if (adapter.OperationalStatus != OperationalStatus.Up)
            {
                return null;
            }

            if (IsVpnAdapter(adapter))
            {
                return null;
            }

            var kind = AdapterKind(adapter);
            if (kind.Length == 0)
            {
                return null;
            }

            var info = new NetworkAdapterInfo
            {
                Name = string.IsNullOrWhiteSpace(adapter.Name) ? adapter.Description : adapter.Name,
                Description = adapter.Description ?? string.Empty,
                Kind = kind,
                MacAddress = FormatMac(adapter),
                Speed = FormatSpeed(adapter)
            };

            try
            {
                var properties = adapter.GetIPProperties();
                AddAddresses(info, properties);
                foreach (var gateway in properties.GatewayAddresses)
                {
                    var text = gateway.Address?.ToString() ?? string.Empty;
                    if (text.Trim().Length > 0)
                    {
                        info.Gateways.Add(text);
                    }
                }

                foreach (var dns in properties.DnsAddresses)
                {
                    var text = dns.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        info.DnsServers.Add(text);
                    }
                }

                try
                {
                    info.Dhcp = properties.GetIPv4Properties().IsDhcpEnabled ? "yes" : "no";
                }
                catch
                {
                    info.Dhcp = "not available";
                }
            }
            catch (Exception ex)
            {
                info.Addresses.Add("could not read addresses: " + ex.Message);
            }

            return info;
        }
        catch
        {
            return null;
        }
    }

    private static void AddAddresses(NetworkAdapterInfo info, IPInterfaceProperties properties)
    {
        var addresses = new List<string>();
        foreach (var address in properties.UnicastAddresses)
        {
            var text = address.Address?.ToString() ?? string.Empty;
            if (text.Trim().Length > 0)
            {
                addresses.Add(text);
            }
        }

        info.Addresses.AddRange(addresses
            .OrderBy(address => address.Contains(":") ? 1 : 0)
            .ThenBy(address => address, StringComparer.OrdinalIgnoreCase));
    }

    private static string AdapterKind(NetworkInterface adapter)
    {
        var text = AdapterText(adapter);
        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
            text.Contains("wireless") ||
            text.Contains("wi-fi") ||
            text.Contains("wifi") ||
            text.Contains("802.11"))
        {
            return "Wireless";
        }

        if (text.Contains("ethernet") ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet3Megabit ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)
        {
            return "Wired";
        }

        return string.Empty;
    }

    private static bool IsVpnAdapter(NetworkInterface adapter)
    {
        var text = AdapterText(adapter);
        if (IgnoredTunnelHints.Any(hint => text.Contains(hint)))
        {
            return false;
        }

        if (adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel ||
            adapter.NetworkInterfaceType == NetworkInterfaceType.Ppp)
        {
            return true;
        }

        return VpnAdapterHints.Any(hint => text.Contains(hint));
    }

    private static string AdapterText(NetworkInterface adapter)
    {
        return ((adapter.Name ?? string.Empty) + " " + (adapter.Description ?? string.Empty)).ToLowerInvariant();
    }

    private static string FormatMac(NetworkInterface adapter)
    {
        try
        {
            var bytes = adapter.GetPhysicalAddress().GetAddressBytes();
            if (bytes.Length == 0 || bytes.All(value => value == 0))
            {
                return "not available";
            }

            return string.Join("-", bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
        }
        catch
        {
            return "not available";
        }
    }

    private static string FormatSpeed(NetworkInterface adapter)
    {
        try
        {
            var bitsPerSecond = adapter.Speed;
            if (bitsPerSecond <= 0)
            {
                return "not available";
            }

            var megabits = bitsPerSecond / 1000000d;
            if (megabits >= 1000d)
            {
                return (megabits / 1000d).ToString("0.##", CultureInfo.InvariantCulture) + " Gbps";
            }

            return megabits.ToString("0.##", CultureInfo.InvariantCulture) + " Mbps";
        }
        catch
        {
            return "not available";
        }
    }

    private static HashSet<string> ReadActiveRasNames(NetworkEnvironment environment)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var apiError = TryRasEnumConnections(names);
        if (apiError.Length == 0)
        {
            return names;
        }

        names.Clear();
        var dialError = TryRasDial(names);
        if (dialError.Length == 0)
        {
            return names;
        }

        environment.ActiveVpnError = apiError + " " + dialError;
        return names;
    }

    private static string TryRasEnumConnections(HashSet<string> names)
    {
        try
        {
            var connections = new RasConnection[1];
            connections[0].size = Marshal.SizeOf(typeof(RasConnection));
            var bufferSize = connections[0].size;
            var count = 0;
            var result = RasEnumConnections(connections, ref bufferSize, ref count);
            if (result == RasErrorBufferTooSmall)
            {
                var structSize = Math.Max(1, connections[0].size);
                var needed = Math.Max(1, (bufferSize + structSize - 1) / structSize);
                if (needed > 64)
                {
                    needed = 64;
                }

                connections = new RasConnection[needed];
                for (var i = 0; i < connections.Length; i++)
                {
                    connections[i].size = structSize;
                }

                bufferSize = connections.Length * structSize;
                result = RasEnumConnections(connections, ref bufferSize, ref count);
            }

            if (result != 0)
            {
                return "RasEnumConnections returned " + result + ".";
            }

            var limit = Math.Min(count, connections.Length);
            for (var i = 0; i < limit; i++)
            {
                var name = (connections[i].entryName ?? string.Empty).Trim();
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            return "Could not read active Windows VPN connections: " + ex.Message + ".";
        }
    }

    private static string TryRasDial(HashSet<string> names)
    {
        try
        {
            var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rasdial.exe");
            if (!File.Exists(exe))
            {
                return "rasdial.exe was not found.";
            }

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10000))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // The listing process is already gone.
                }

                return "Reading active Windows VPN connections timed out.";
            }

            var output = outputTask.Result ?? string.Empty;
            var error = (errorTask.Result ?? string.Empty).Trim();
            var listing = false;
            foreach (var raw in output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = raw.Trim();
                if (line.Equals("No connections", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (line.StartsWith("Connected to", StringComparison.OrdinalIgnoreCase))
                {
                    listing = true;
                    continue;
                }

                if (line.StartsWith("Command completed", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (listing && line.Length > 0)
                {
                    names.Add(line);
                }
            }

            if (names.Count == 0 && process.ExitCode != 0)
            {
                return error.Length == 0
                    ? "rasdial exited with code " + process.ExitCode + "."
                    : error;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            return "Could not list active Windows VPN connections: " + ex.Message + ".";
        }
    }

    private static void ReadVpnProfiles(NetworkEnvironment environment, HashSet<string> activeNames)
    {
        foreach (var path in PhonebookPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                foreach (var profile in ReadPhonebook(path, activeNames))
                {
                    if (environment.VpnProfiles.Any(existing =>
                            string.Equals(existing.Name, profile.Name, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(existing.Phonebook, profile.Phonebook, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    environment.VpnProfiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                environment.VpnProfileError = "Could not read " + path + ": " + ex.Message;
            }
        }

        environment.VpnProfiles.Sort((left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> PhonebookPaths()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        yield return Path.Combine(appData, "Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");
        yield return Path.Combine(programData, "Microsoft", "Network", "Connections", "Pbk", "rasphone.pbk");
    }

    private static List<VpnConnectionInfo> ReadPhonebook(string path, HashSet<string> activeNames)
    {
        var profiles = new List<VpnConnectionInfo>();
        string? section = null;
        var type = string.Empty;
        var device = string.Empty;
        var server = string.Empty;
        var sawSection = false;

        void Flush()
        {
            var current = section ?? string.Empty;
            if (!sawSection || current.Trim().Length == 0 || !IsVpnProfile(type, device))
            {
                return;
            }

            var name = current.Trim();
            profiles.Add(new VpnConnectionInfo
            {
                Name = name,
                Status = activeNames.Contains(name) ? "connected" : "not connected",
                Device = device.Trim(),
                Server = server.Trim(),
                Source = "Windows VPN profile",
                Phonebook = path
            });
        }

        using var reader = new StreamReader(path, Encoding.Default, true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (text.StartsWith("[", StringComparison.Ordinal) && text.EndsWith("]", StringComparison.Ordinal))
            {
                Flush();
                section = text.Substring(1, text.Length - 2);
                type = string.Empty;
                device = string.Empty;
                server = string.Empty;
                sawSection = true;
                continue;
            }

            var split = text.IndexOf('=');
            if (split <= 0)
            {
                continue;
            }

            var key = text.Substring(0, split).Trim();
            var value = text.Substring(split + 1).Trim();
            if (key.Equals("Type", StringComparison.OrdinalIgnoreCase))
            {
                type = value;
            }
            else if (key.Equals("Device", StringComparison.OrdinalIgnoreCase))
            {
                device = value;
            }
            else if (key.Equals("PhoneNumber", StringComparison.OrdinalIgnoreCase))
            {
                server = value;
            }
        }

        Flush();
        return profiles;
    }

    private static bool IsVpnProfile(string type, string device)
    {
        var kind = type.Trim();
        if (kind == "2")
        {
            return true;
        }

        if (kind.Length > 0)
        {
            return false;
        }

        var text = device.ToLowerInvariant();
        return text.Contains("vpn") ||
            text.Contains("ikev2") ||
            text.Contains("sstp") ||
            text.Contains("l2tp") ||
            text.Contains("pptp");
    }

    private static void ReadActiveVpns(NetworkEnvironment environment, HashSet<string> activeNames)
    {
        foreach (var profile in environment.VpnProfiles)
        {
            if (profile.Status == "connected")
            {
                environment.ActiveVpns.Add(profile);
            }
        }

        foreach (var name in activeNames.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            if (environment.ActiveVpns.Any(vpn => string.Equals(vpn.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            environment.ActiveVpns.Add(new VpnConnectionInfo
            {
                Name = name,
                Status = "connected",
                Source = "Windows VPN"
            });
        }

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || !IsVpnAdapter(adapter))
                {
                    continue;
                }

                var name = adapter.Name ?? string.Empty;
                if (name.Trim().Length == 0)
                {
                    name = adapter.Description ?? string.Empty;
                }

                var description = adapter.Description ?? string.Empty;
                if (AlreadyListed(name, description, environment.ActiveVpns))
                {
                    continue;
                }

                environment.ActiveVpns.Add(new VpnConnectionInfo
                {
                    Name = name ?? string.Empty,
                    Status = "connected",
                    Device = description,
                    Source = "network adapter"
                });
            }
        }
        catch (Exception ex)
        {
            var message = "Could not read VPN network adapters: " + ex.Message;
            environment.ActiveVpnError = string.IsNullOrWhiteSpace(environment.ActiveVpnError)
                ? message
                : environment.ActiveVpnError + " " + message;
        }
    }

    private static bool AlreadyListed(string name, string description, List<VpnConnectionInfo> active)
    {
        foreach (var vpn in active)
        {
            if (string.Equals(vpn.Name, name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(vpn.Device, description, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(vpn.Device) &&
                    description.IndexOf(vpn.Device, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }
        }

        return false;
    }

    private static ProxySettings ReadProxy()
    {
        var proxy = new ProxySettings();
        ReadUserProxy(proxy);
        ReadWinHttpProxy(proxy);
        ReadEnvironmentProxies(proxy);
        return proxy;
    }

    private static void ReadUserProxy(ProxySettings proxy)
    {
        try
        {
            var config = new WinHttpCurrentUserIeProxyConfig();
            if (!WinHttpGetIEProxyConfigForCurrentUser(ref config))
            {
                proxy.UserError = "WinHttpGetIEProxyConfigForCurrentUser failed: " + Marshal.GetLastWin32Error();
                ReadUserProxyFromRegistry(proxy);
                return;
            }

            var autoConfig = TakeString(config.autoConfigUrl);
            var server = TakeString(config.proxy);
            var bypass = TakeString(config.proxyBypass);
            proxy.AutoDetect = config.autoDetect != 0 ? "yes" : "no";
            proxy.AutoConfigUrl = Redact(autoConfig);
            proxy.ProxyServer = Redact(server);
            proxy.ProxyBypass = bypass;
            proxy.UserProxy = string.IsNullOrWhiteSpace(server) ? "disabled" : "enabled";
            ApplyRegistryProxyEnable(proxy);
        }
        catch (Exception ex)
        {
            proxy.UserError = "Could not read the user proxy: " + ex.Message;
            ReadUserProxyFromRegistry(proxy);
        }
    }

    private static void ApplyRegistryProxyEnable(ProxySettings proxy)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key == null)
            {
                return;
            }

            var enabled = key.GetValue("ProxyEnable");
            if (enabled == null)
            {
                return;
            }

            proxy.UserProxy = Convert.ToInt32(enabled, CultureInfo.InvariantCulture) != 0 ? "enabled" : "disabled";
            if (string.IsNullOrWhiteSpace(proxy.ProxyServer))
            {
                proxy.ProxyServer = Redact(key.GetValue("ProxyServer") as string ?? string.Empty);
            }

            if (string.IsNullOrWhiteSpace(proxy.ProxyBypass))
            {
                proxy.ProxyBypass = key.GetValue("ProxyOverride") as string ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(proxy.AutoConfigUrl))
            {
                proxy.AutoConfigUrl = Redact(key.GetValue("AutoConfigURL") as string ?? string.Empty);
            }
        }
        catch (Exception ex)
        {
            if (string.IsNullOrWhiteSpace(proxy.UserError))
            {
                proxy.UserError = "Could not read Internet Settings: " + ex.Message;
            }
        }
    }

    private static void ReadUserProxyFromRegistry(ProxySettings proxy)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
            if (key == null)
            {
                proxy.UserProxy = "not available";
                return;
            }

            var enabled = key.GetValue("ProxyEnable");
            proxy.UserProxy = enabled != null && Convert.ToInt32(enabled, CultureInfo.InvariantCulture) != 0
                ? "enabled"
                : "disabled";
            proxy.ProxyServer = Redact(key.GetValue("ProxyServer") as string ?? string.Empty);
            proxy.ProxyBypass = key.GetValue("ProxyOverride") as string ?? string.Empty;
            proxy.AutoConfigUrl = Redact(key.GetValue("AutoConfigURL") as string ?? string.Empty);
            if (string.IsNullOrWhiteSpace(proxy.AutoDetect))
            {
                proxy.AutoDetect = "not available";
            }
        }
        catch (Exception ex)
        {
            proxy.UserError = "Could not read Internet Settings: " + ex.Message;
            proxy.UserProxy = "not available";
        }
    }

    private static void ReadWinHttpProxy(ProxySettings proxy)
    {
        try
        {
            var info = new WinHttpProxyInfo();
            if (!WinHttpGetDefaultProxyConfiguration(ref info))
            {
                proxy.WinHttpError = "WinHttpGetDefaultProxyConfiguration failed: " + Marshal.GetLastWin32Error();
                proxy.WinHttpProxy = "not available";
                return;
            }

            var server = TakeString(info.proxy);
            var bypass = TakeString(info.proxyBypass);
            proxy.WinHttpBypass = bypass;
            if (info.accessType == WinHttpAccessTypeNoProxy || string.IsNullOrWhiteSpace(server))
            {
                proxy.WinHttpProxy = "direct";
                return;
            }

            proxy.WinHttpProxy = info.accessType == WinHttpAccessTypeNamedProxy
                ? Redact(server)
                : "default " + Redact(server);
        }
        catch (Exception ex)
        {
            proxy.WinHttpError = "Could not read the WinHTTP proxy: " + ex.Message;
            proxy.WinHttpProxy = "not available";
        }
    }

    private static void ReadEnvironmentProxies(ProxySettings proxy)
    {
        foreach (var name in ProxyEnvironmentNames)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(name.ToUpperInvariant());
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                proxy.EnvironmentProxies.Add(name + "=" + Redact(value));
            }
        }
    }

    private static string TakeString(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer) ?? string.Empty;
        }
        finally
        {
            GlobalFree(pointer);
        }
    }

    private static string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(value, "(?i)(://[^:/\\s]+:)([^@/\\s]+)@", "$1****@");
    }

    [DllImport("rasapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RasEnumConnections(
        [In, Out] RasConnection[] connections,
        ref int bufferSize,
        ref int count);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpGetIEProxyConfigForCurrentUser(ref WinHttpCurrentUserIeProxyConfig config);

    [DllImport("winhttp.dll", SetLastError = true)]
    private static extern bool WinHttpGetDefaultProxyConfiguration(ref WinHttpProxyInfo info);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct RasLuid
    {
        public uint lowPart;
        public int highPart;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    private struct RasConnection
    {
        public int size;
        public IntPtr handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        public string entryName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        public string deviceType;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        public string deviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string phonebook;
        public int subEntry;
        public Guid entryId;
        public int flags;
        public RasLuid sessionId;
        public Guid correlationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinHttpCurrentUserIeProxyConfig
    {
        public int autoDetect;
        public IntPtr autoConfigUrl;
        public IntPtr proxy;
        public IntPtr proxyBypass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinHttpProxyInfo
    {
        public int accessType;
        public IntPtr proxy;
        public IntPtr proxyBypass;
    }
}
