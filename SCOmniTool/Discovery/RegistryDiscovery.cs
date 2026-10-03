using System;
using System.Collections.Generic;
using Microsoft.Win32;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class RegistryDiscovery
{
    private const string ServicesPath = @"SYSTEM\CurrentControlSet\Services";
    private const string ProductsPath = @"SOFTWARE\Classes\Installer\Products";
    private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string UninstallWow64Path = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public static List<RegistryKeyInfo> FindAll(ICollection<string> notes)
    {
        var results = new List<RegistryKeyInfo>();
        SafeAdd(results, notes, FindServiceKeys);
        SafeAdd(results, notes, () => FindProductKeys(notes));
        SafeAdd(results, notes, () => FindUniKeys(notes));
        return results;
    }

    private static void SafeAdd(
        List<RegistryKeyInfo> results,
        ICollection<string> notes,
        Func<List<RegistryKeyInfo>> find)
    {
        try
        {
            results.AddRange(find());
        }
        catch (Exception ex)
        {
            notes.Add("Could not finish a registry search: " + ex.Message);
        }
    }

    public static List<RegistryKeyInfo> FindServiceKeys()
    {
        return FindKeysByNamePrefix(ServicesPath, RegistryKeyType.ServiceKey, keyName =>
            keyName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal) ? keyName : null);
    }

    public static List<RegistryKeyInfo> FindProductKeys(ICollection<string> notes)
    {
        var results = new List<RegistryKeyInfo>();

        using var baseKey = Registry.LocalMachine.OpenSubKey(ProductsPath);
        if (baseKey == null)
        {
            return results;
        }

        var skipped = 0;
        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            try
            {
                using var subKey = baseKey.OpenSubKey(subKeyName);
                var productName = subKey?.GetValue("ProductName") as string;
                if (productName != null && productName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal))
                {
                    results.Add(new RegistryKeyInfo
                    {
                        Type = RegistryKeyType.ProductKey,
                        KeyPath = $@"HKLM\{ProductsPath}\{subKeyName}",
                        KeyName = subKeyName,
                        DisplayValue = productName
                    });
                }
            }
            catch
            {
                skipped++;
            }
        }

        if (skipped > 0)
        {
            notes.Add($"Skipped {skipped} inaccessible registry key(s) under HKLM\\{ProductsPath}.");
        }

        return results;
    }

    public static List<RegistryKeyInfo> FindUniKeys(ICollection<string> notes)
    {
        var results = new List<RegistryKeyInfo>();
        results.AddRange(FindUniKeysInPath(UninstallPath, notes));
        results.AddRange(FindUniKeysInPath(UninstallWow64Path, notes));
        return results;
    }

    public static string? ReadRegistryValue(string fullKeyPath, string valueName, ICollection<string>? notes = null)
    {
        try
        {
            using var key = OpenKey(fullKeyPath);
            return ValueAsString(key?.GetValue(valueName));
        }
        catch (Exception ex)
        {
            notes?.Add($"Could not read {valueName} from {fullKeyPath}: {ex.Message}");
            return null;
        }
    }

    public static Dictionary<string, string> ReadRegistryValues(
        string fullKeyPath,
        ICollection<string> notes,
        params string[] valueNames)
    {
        var found = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = OpenKey(fullKeyPath);
            if (key == null)
            {
                return found;
            }

            foreach (var valueName in valueNames)
            {
                var text = ValueAsString(key.GetValue(valueName));
                if (text == null || text.Length == 0)
                {
                    continue;
                }

                found[valueName] = text;
            }
        }
        catch (Exception ex)
        {
            notes.Add($"Could not read {fullKeyPath}: {ex.Message}");
        }

        return found;
    }

    private static List<RegistryKeyInfo> FindUniKeysInPath(string relativePath, ICollection<string> notes)
    {
        var results = new List<RegistryKeyInfo>();

        using var baseKey = Registry.LocalMachine.OpenSubKey(relativePath);
        if (baseKey == null)
        {
            return results;
        }

        var skipped = 0;
        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            try
            {
                using var subKey = baseKey.OpenSubKey(subKeyName);
                var displayName = subKey?.GetValue("DisplayName") as string;
                if (displayName != null && displayName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal))
                {
                    results.Add(new RegistryKeyInfo
                    {
                        Type = RegistryKeyType.UniKey,
                        KeyPath = $@"HKLM\{relativePath}\{subKeyName}",
                        KeyName = subKeyName,
                        DisplayValue = displayName
                    });
                }
            }
            catch
            {
                skipped++;
            }
        }

        if (skipped > 0)
        {
            notes.Add($"Skipped {skipped} inaccessible registry key(s) under HKLM\\{relativePath}.");
        }

        return results;
    }

    private static List<RegistryKeyInfo> FindKeysByNamePrefix(
        string relativePath,
        RegistryKeyType keyType,
        Func<string, string?> getDisplayValue)
    {
        var results = new List<RegistryKeyInfo>();

        using var baseKey = Registry.LocalMachine.OpenSubKey(relativePath);
        if (baseKey == null)
        {
            return results;
        }

        foreach (var subKeyName in baseKey.GetSubKeyNames())
        {
            var displayValue = getDisplayValue(subKeyName);
            if (displayValue == null)
            {
                continue;
            }

            results.Add(new RegistryKeyInfo
            {
                Type = keyType,
                KeyPath = $@"HKLM\{relativePath}\{subKeyName}",
                KeyName = subKeyName,
                DisplayValue = displayValue
            });
        }

        return results;
    }

    private static RegistryKey? OpenKey(string fullKeyPath)
    {
        var relativePath = fullKeyPath.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
            ? fullKeyPath.Substring(5)
            : fullKeyPath;

        return Registry.LocalMachine.OpenSubKey(relativePath);
    }

    private static string? ValueAsString(object? value)
    {
        switch (value)
        {
            case string text:
                return text;
            case string[] parts:
                return string.Join(" ", parts);
            default:
                return null;
        }
    }
}
