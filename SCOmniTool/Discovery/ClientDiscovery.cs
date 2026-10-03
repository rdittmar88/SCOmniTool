using System;
using System.Collections.Generic;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ClientDiscovery
{
    public static List<ClientInfo> FromUninstallKeys(IEnumerable<RegistryKeyInfo> keys, ICollection<string> notes)
    {
        var results = new List<ClientInfo>();
        var indexByKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in keys)
        {
            if (key.Type != RegistryKeyType.UniKey)
            {
                continue;
            }

            var values = RegistryDiscovery.ReadRegistryValues(
                key.KeyPath,
                notes,
                "UninstallString",
                "QuietUninstallString",
                "InstallLocation");

            var client = new ClientInfo
            {
                KeyName = key.KeyName,
                DisplayName = key.DisplayValue,
                RegistryPath = key.KeyPath,
                UninstallString = Value(values, "UninstallString"),
                QuietUninstallString = Value(values, "QuietUninstallString"),
                InstallLocation = Value(values, "InstallLocation")
            };

            var identity = string.IsNullOrWhiteSpace(client.KeyName) ? client.DisplayName : client.KeyName;
            if (string.IsNullOrWhiteSpace(identity))
            {
                results.Add(client);
                continue;
            }

            if (!indexByKey.TryGetValue(identity, out var index))
            {
                indexByKey[identity] = results.Count;
                results.Add(client);
                continue;
            }

            var existing = results[index];
            if (Prefer(client, existing))
            {
                FillMissing(client, existing);
                results[index] = client;
            }
            else
            {
                FillMissing(existing, client);
            }
        }

        return results;
    }

    private static bool Prefer(ClientInfo candidate, ClientInfo existing)
    {
        var candidateWow = IsWowPath(candidate.RegistryPath);
        var existingWow = IsWowPath(existing.RegistryPath);
        if (candidateWow != existingWow)
        {
            return candidateWow;
        }

        return FieldCount(candidate) > FieldCount(existing);
    }

    private static bool IsWowPath(string path)
    {
        return path.IndexOf(@"\WOW6432Node\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int FieldCount(ClientInfo client)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(client.InstallLocation))
        {
            count++;
        }

        if (!string.IsNullOrWhiteSpace(client.UninstallString))
        {
            count++;
        }

        if (!string.IsNullOrWhiteSpace(client.QuietUninstallString))
        {
            count++;
        }

        return count;
    }

    private static void FillMissing(ClientInfo target, ClientInfo source)
    {
        if (string.IsNullOrWhiteSpace(target.DisplayName))
        {
            target.DisplayName = source.DisplayName;
        }

        if (string.IsNullOrWhiteSpace(target.InstallLocation))
        {
            target.InstallLocation = source.InstallLocation;
        }

        if (string.IsNullOrWhiteSpace(target.UninstallString))
        {
            target.UninstallString = source.UninstallString;
        }

        if (string.IsNullOrWhiteSpace(target.QuietUninstallString))
        {
            target.QuietUninstallString = source.QuietUninstallString;
        }
    }

    private static string Value(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
