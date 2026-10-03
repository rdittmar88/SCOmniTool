using System.Collections.Generic;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ClientDiscovery
{
    public static List<ClientInfo> FromUninstallKeys(IEnumerable<RegistryKeyInfo> keys, ICollection<string> notes)
    {
        var results = new List<ClientInfo>();

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

            results.Add(new ClientInfo
            {
                KeyName = key.KeyName,
                DisplayName = key.DisplayValue,
                RegistryPath = key.KeyPath,
                UninstallString = Value(values, "UninstallString"),
                QuietUninstallString = Value(values, "QuietUninstallString"),
                InstallLocation = Value(values, "InstallLocation")
            });
        }

        return results;
    }

    private static string Value(Dictionary<string, string> values, string name)
    {
        return values.TryGetValue(name, out var value) ? value : string.Empty;
    }
}
