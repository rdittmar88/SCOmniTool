using System;
using System.Collections.Generic;
using System.Globalization;

namespace SCOmniTool.Security;

internal readonly struct SecurityProductReading
{
    public SecurityProductReading(string name, string path, uint? state, string unreadable)
    {
        Name = name ?? string.Empty;
        Path = path ?? string.Empty;
        State = state;
        Unreadable = unreadable ?? string.Empty;
    }

    public string Name { get; }
    public string Path { get; }
    public uint? State { get; }
    public string Unreadable { get; }
}

internal static class ProductStateText
{
    // Observed Security Center layout. Microsoft does not document these masks.
    private const uint ProtectionMask = 0xF000;
    private const uint ProtectionOn = 0x1000;
    private const uint ProtectionSnoozed = 0x2000;
    private const uint ProtectionExpired = 0x3000;
    private const uint SignatureMask = 0x00F0;
    private const uint SignatureOutOfDate = 0x0010;
    private const uint WindowsOwner = 0x0100;

    public static SecurityProductReading Read(string name, string path, object? value)
    {
        if (value == null)
        {
            return new SecurityProductReading(name, path, null, "not available");
        }

        try
        {
            var state = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            return new SecurityProductReading(name, path, state, string.Empty);
        }
        catch
        {
            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "not available";
            }

            return new SecurityProductReading(name, path, null, text.Trim());
        }
    }

    public static string[] Describe(IReadOnlyList<SecurityProductReading> products)
    {
        var enabled = new List<string>();
        foreach (var product in products)
        {
            if (product.State.HasValue && IsEnabled(product.State.Value))
            {
                enabled.Add(product.Name);
            }
        }

        var results = new string[products.Count];
        for (var i = 0; i < products.Count; i++)
        {
            var product = products[i];
            if (!product.State.HasValue)
            {
                results[i] = string.IsNullOrWhiteSpace(product.Unreadable)
                    ? "not available"
                    : product.Unreadable;
                continue;
            }

            var state = product.State.Value;
            string? deferredTo = null;
            if (IsWindowsProduct(product.Name, state) && !IsEnabled(state))
            {
                deferredTo = OtherEnabledProducts(enabled, product.Name);
            }

            results[i] = DescribeOne(state, deferredTo);
        }

        return results;
    }

    private static bool IsEnabled(uint state)
    {
        return (state & ProtectionMask) == ProtectionOn;
    }

    private static bool IsWindowsProduct(string name, uint state)
    {
        if (name.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return (state & 0x0F00) == WindowsOwner;
    }

    private static string? OtherEnabledProducts(List<string> enabled, string name)
    {
        var others = new List<string>();
        foreach (var enabledName in enabled)
        {
            if (string.IsNullOrWhiteSpace(enabledName))
            {
                continue;
            }

            if (string.Equals(enabledName, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!others.Exists(item => string.Equals(item, enabledName, StringComparison.OrdinalIgnoreCase)))
            {
                others.Add(enabledName);
            }
        }

        if (others.Count == 0)
        {
            return null;
        }

        return string.Join(", ", others);
    }

    private static string DescribeOne(uint state, string? deferredTo)
    {
        var protection = state & ProtectionMask;
        string protectionText;
        if (!string.IsNullOrEmpty(deferredTo) && protection != ProtectionOn)
        {
            protectionText = "Deferred to " + deferredTo;
        }
        else if (protection == 0)
        {
            protectionText = "Disabled";
        }
        else if (protection == ProtectionOn)
        {
            protectionText = "Enabled";
        }
        else if (protection == ProtectionSnoozed)
        {
            protectionText = "Snoozed";
        }
        else if (protection == ProtectionExpired)
        {
            protectionText = "Expired";
        }
        else
        {
            return "Unknown (" + state.ToString(CultureInfo.InvariantCulture) + ")";
        }

        var signature = state & SignatureMask;
        string definitions;
        if (signature == 0)
        {
            definitions = "definitions up to date";
        }
        else if (signature == SignatureOutOfDate)
        {
            definitions = "definitions out of date";
        }
        else
        {
            definitions = "definitions status unknown";
        }

        return protectionText + ", " + definitions + " (" + state.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
