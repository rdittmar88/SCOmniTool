using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class AntivirusScanner
{
    public static List<AntivirusProduct> FindAll(out string note)
    {
        note = string.Empty;
        var products = new List<AntivirusProduct>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2",
                "SELECT displayName, pathToSignedProductExe, productState FROM AntiVirusProduct");
            using var results = searcher.Get();
            foreach (ManagementObject product in results)
            {
                using (product)
                {
                    products.Add(new AntivirusProduct
                    {
                        Name = Property(product, "displayName"),
                        Path = Property(product, "pathToSignedProductExe"),
                        ProductState = FormatProductState(product["productState"])
                    });
                }
            }
        }
        catch (Exception ex)
        {
            note = "Could not read AntiVirusProduct: " + ex.Message;
        }

        return products;
    }

    private static string Property(ManagementObject item, string name)
    {
        try
        {
            var value = item[name]?.ToString();
            if (value == null || string.IsNullOrWhiteSpace(value))
            {
                return "not available";
            }

            return value.Trim();
        }
        catch
        {
            return "not available";
        }
    }

    private static string FormatProductState(object? value)
    {
        if (value == null)
        {
            return "not available";
        }

        try
        {
            var number = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            return number + " (0x" + number.ToString("X", CultureInfo.InvariantCulture) + ")";
        }
        catch
        {
            return value.ToString() ?? "not available";
        }
    }
}
