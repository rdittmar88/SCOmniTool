using System;
using System.Globalization;
using System.Management;
using System.Text;

namespace SCOmniTool.Reporting;

internal static class SystemReport
{
    public static string Build()
    {
        var builder = new StringBuilder();
        builder.AppendLine("System report");
        builder.AppendLine("Collected: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("Computer name: " + Environment.MachineName);

        var wroteOs = false;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    builder.AppendLine("OS: " + Property(item, "Caption"));
                    builder.AppendLine("Version: " + Property(item, "Version"));
                    builder.AppendLine("Build: " + Property(item, "BuildNumber"));
                    builder.AppendLine("Architecture: " + Property(item, "OSArchitecture"));
                    wroteOs = true;
                }
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("Could not read Win32_OperatingSystem: " + ex.Message);
        }

        if (!wroteOs)
        {
            builder.AppendLine("OS version: " + Environment.OSVersion);
            builder.AppendLine("Architecture: " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    builder.AppendLine("Manufacturer: " + Property(item, "Manufacturer"));
                    builder.AppendLine("Model: " + Property(item, "Model"));
                    builder.AppendLine("Installed RAM: " + FormatRam(item["TotalPhysicalMemory"]));
                }
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("Could not read Win32_ComputerSystem: " + ex.Message);
        }

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            using var results = searcher.Get();
            var index = 0;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    index++;
                    var prefix = index == 1 ? "CPU" : "CPU " + index;
                    builder.AppendLine(prefix + ": " + Property(item, "Name"));
                    builder.AppendLine("Cores: " + Property(item, "NumberOfCores"));
                    builder.AppendLine("Logical processors: " + Property(item, "NumberOfLogicalProcessors"));
                }
            }

            if (index == 0)
            {
                builder.AppendLine("Logical processors: " + Environment.ProcessorCount);
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("Could not read Win32_Processor: " + ex.Message);
            builder.AppendLine("Logical processors: " + Environment.ProcessorCount);
        }

        return builder.ToString().TrimEnd();
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

    private static string FormatRam(object? value)
    {
        if (value == null)
        {
            return "not available";
        }

        try
        {
            var bytes = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            var gigabytes = bytes / 1024d / 1024d / 1024d;
            return gigabytes.ToString("0.0", CultureInfo.InvariantCulture) + " GB";
        }
        catch
        {
            return value.ToString() ?? "not available";
        }
    }
}
