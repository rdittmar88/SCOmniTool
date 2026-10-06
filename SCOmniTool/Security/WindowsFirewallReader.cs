using System;
using System.Globalization;
using System.Reflection;
using System.ServiceProcess;
using Microsoft.Win32;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class WindowsFirewallReader
{
    private const int ProfileDomain = 1;
    private const int ProfilePrivate = 2;
    private const int ProfilePublic = 4;

    public static WindowsFirewallStatus Read()
    {
        var status = new WindowsFirewallStatus
        {
            ServiceStatus = ReadServiceStatus()
        };

        if (!TryReadPolicy(status))
        {
            ReadRegistryProfiles(status);
        }

        return status;
    }

    private static string ReadServiceStatus()
    {
        try
        {
            using var service = new ServiceController("MpsSvc");
            return service.Status.ToString();
        }
        catch (Exception ex)
        {
            return "not available (" + ex.Message + ")";
        }
    }

    private static bool TryReadPolicy(WindowsFirewallStatus status)
    {
        object? policy = null;
        try
        {
            var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            if (type == null)
            {
                status.Error = "Windows Firewall policy is not available.";
                return false;
            }

            policy = Activator.CreateInstance(type);
            if (policy == null)
            {
                status.Error = "Windows Firewall policy could not be opened.";
                return false;
            }

            var current = ToInt(type.InvokeMember(
                "CurrentProfileTypes",
                BindingFlags.GetProperty,
                null,
                policy,
                null));
            status.Domain = ReadProfile(type, policy, ProfileDomain, current);
            status.Private = ReadProfile(type, policy, ProfilePrivate, current);
            status.Public = ReadProfile(type, policy, ProfilePublic, current);
            status.Error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            status.Error = "Could not read the Windows Firewall policy: " + ex.Message;
            return false;
        }
        finally
        {
            if (policy != null)
            {
                try
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(policy);
                }
                catch
                {
                    // The policy object is already released.
                }
            }
        }
    }

    private static string ReadProfile(Type type, object policy, int profile, int current)
    {
        object? value;
        try
        {
            value = type.InvokeMember(
                "FirewallEnabled",
                BindingFlags.GetProperty,
                null,
                policy,
                new object[] { profile });
        }
        catch
        {
            value = type.InvokeMember(
                "get_FirewallEnabled",
                BindingFlags.InvokeMethod,
                null,
                policy,
                new object[] { profile });
        }

        var text = IsEnabled(value) ? "on" : "off";
        if ((current & profile) != 0)
        {
            text += " (current)";
        }

        return text;
    }

    private static void ReadRegistryProfiles(WindowsFirewallStatus status)
    {
        status.Domain = ReadRegistryProfile(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\DomainProfile", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\DomainProfile");
        status.Private = ReadRegistryProfile(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PrivateProfile", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
        status.Public = ReadRegistryProfile(@"SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile", @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\PublicProfile");
        if (status.Domain != "not available" || status.Private != "not available" || status.Public != "not available")
        {
            status.Error = string.Empty;
        }
    }

    private static string ReadRegistryProfile(string policyPath, string localPath)
    {
        var policy = ReadDword(policyPath, "EnableFirewall");
        if (policy.HasValue)
        {
            return policy.Value != 0 ? "on" : "off";
        }

        var local = ReadDword(localPath, "EnableFirewall");
        if (local.HasValue)
        {
            return local.Value != 0 ? "on" : "off";
        }

        return "not available";
    }

    private static int? ReadDword(string path, string name)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Default })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var key = baseKey.OpenSubKey(path);
                if (key == null)
                {
                    continue;
                }

                var value = key.GetValue(name);
                if (value == null)
                {
                    return null;
                }

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                // The other registry view may still be readable.
            }
        }

        return null;
    }

    private static int ToInt(object? value)
    {
        if (value == null)
        {
            return 0;
        }

        return Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static bool IsEnabled(object? value)
    {
        if (value is bool flag)
        {
            return flag;
        }

        return ToInt(value) != 0;
    }
}
