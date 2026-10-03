using System;
using System.Collections.Generic;
using System.Management;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class AntivirusScanner
{
    public static List<AntivirusProduct> FindAll(out string note)
    {
        note = string.Empty;
        var readings = new List<SecurityProductReading>();

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
                    readings.Add(ProductStateText.Read(
                        Property(product, "displayName"),
                        Property(product, "pathToSignedProductExe"),
                        product["productState"]));
                }
            }
        }
        catch (Exception ex)
        {
            note = "Could not read AntiVirusProduct: " + ex.Message;
            return new List<AntivirusProduct>();
        }

        return ToProducts(readings);
    }

    private static List<AntivirusProduct> ToProducts(IReadOnlyList<SecurityProductReading> readings)
    {
        var descriptions = ProductStateText.Describe(readings);
        var products = new List<AntivirusProduct>(readings.Count);
        for (var i = 0; i < readings.Count; i++)
        {
            products.Add(new AntivirusProduct
            {
                Name = readings[i].Name,
                Path = readings[i].Path,
                ProductState = descriptions[i]
            });
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
}
