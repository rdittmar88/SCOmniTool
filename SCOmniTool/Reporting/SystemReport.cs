using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;

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

    public static string BuildSnapshot()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Performance snapshot");
        builder.AppendLine("One sample. Rate values use two reads about one second apart. This is not a performance history.");
        AppendUptime(builder);

        PerformanceCounter? cpu = null;
        PerformanceCounter? availableMemory = null;
        var disks = new List<DiskCounters>();
        var sampleRates = false;
        try
        {
            try
            {
                cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                cpu.NextValue();
                sampleRates = true;
            }
            catch (Exception ex)
            {
                builder.AppendLine("Processor in use: " + ex.Message);
            }

            try
            {
                availableMemory = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                builder.AppendLine("Available RAM: " + ex.Message);
            }

            try
            {
                OpenDisks(disks);
                foreach (var disk in disks)
                {
                    disk.AverageQueue.NextValue();
                    disk.BytesPerSecond.NextValue();
                }

                if (disks.Count > 0)
                {
                    sampleRates = true;
                }
            }
            catch (Exception ex)
            {
                builder.AppendLine("Disk: " + ex.Message);
            }

            if (sampleRates)
            {
                Thread.Sleep(1000);
            }

            if (cpu != null)
            {
                try
                {
                    builder.AppendLine("Processor in use: " + cpu.NextValue().ToString("0.0", CultureInfo.InvariantCulture) + "%");
                }
                catch (Exception ex)
                {
                    builder.AppendLine("Processor in use: " + ex.Message);
                }
            }

            AppendRam(builder, availableMemory);
            AppendDisks(builder, disks);
        }
        finally
        {
            cpu?.Dispose();
            availableMemory?.Dispose();
            foreach (var disk in disks)
            {
                disk.Dispose();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendUptime(StringBuilder builder)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LastBootUpTime FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var raw = item["LastBootUpTime"]?.ToString();
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        builder.AppendLine("Uptime: not available");
                        return;
                    }

                    var boot = ManagementDateTimeConverter.ToDateTime(raw);
                    var uptime = DateTime.Now - boot;
                    if (uptime < TimeSpan.Zero)
                    {
                        uptime = TimeSpan.Zero;
                    }

                    builder.AppendLine("Last boot: " + boot.ToString("yyyy-MM-dd HH:mm:ss"));
                    builder.AppendLine(
                        "Uptime: " + uptime.Days + " days " + uptime.Hours + " hours " + uptime.Minutes + " minutes");
                    return;
                }
            }

            builder.AppendLine("Uptime: not available");
        }
        catch (Exception ex)
        {
            builder.AppendLine("Uptime: " + ex.Message);
        }
    }

    private static void AppendRam(StringBuilder builder, PerformanceCounter? availableMemory)
    {
        if (availableMemory == null)
        {
            return;
        }

        try
        {
            var availableMb = availableMemory.NextValue();
            var availableBytes = (double)availableMb * 1024d * 1024d;
            builder.AppendLine("Available RAM: " + availableMb.ToString("0", CultureInfo.InvariantCulture) + " MB");

            var totalBytes = ReadTotalPhysicalMemory();
            if (totalBytes > 0)
            {
                var inUse = Math.Max(0d, totalBytes - availableBytes);
                var percent = inUse / totalBytes * 100d;
                builder.AppendLine("RAM in use: " + percent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("Available RAM: " + ex.Message);
        }
    }

    private static double ReadTotalPhysicalMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var raw = item["TotalPhysicalMemory"]?.ToString();
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var bytes))
                    {
                        return bytes;
                    }
                }
            }
        }
        catch
        {
            // The installed RAM line already covers a failed hardware query.
        }

        return 0;
    }

    private static void OpenDisks(List<DiskCounters> disks)
    {
        var category = new PerformanceCounterCategory("PhysicalDisk");
        var names = category.GetInstanceNames()
            .OrderBy(name => string.Equals(name, "_Total", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            try
            {
                disks.Add(new DiskCounters(
                    name,
                    new PerformanceCounter("PhysicalDisk", "Avg. Disk Queue Length", name, true),
                    new PerformanceCounter("PhysicalDisk", "Disk Bytes/sec", name, true),
                    new PerformanceCounter("PhysicalDisk", "Current Disk Queue Length", name, true)));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(name + ": " + ex.Message, ex);
            }
        }
    }

    private static void AppendDisks(StringBuilder builder, List<DiskCounters> disks)
    {
        foreach (var disk in disks)
        {
            try
            {
                var averageQueue = disk.AverageQueue.NextValue();
                var bytesPerSecond = disk.BytesPerSecond.NextValue();
                var currentQueue = disk.CurrentQueue.NextValue();
                builder.AppendLine("Disk " + disk.Name);
                builder.AppendLine("  Current queue length: " + currentQueue.ToString("0.0", CultureInfo.InvariantCulture));
                builder.AppendLine("  Average queue length: " + averageQueue.ToString("0.0", CultureInfo.InvariantCulture));
                builder.AppendLine("  Bytes per second: " + FormatBytesPerSecond(bytesPerSecond));
            }
            catch (Exception ex)
            {
                builder.AppendLine("Disk " + disk.Name + ": " + ex.Message);
            }
        }
    }

    private static string FormatBytesPerSecond(float bytesPerSecond)
    {
        if (bytesPerSecond >= 1024d * 1024d)
        {
            return (bytesPerSecond / 1024d / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " MB/s";
        }

        return (bytesPerSecond / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB/s";
    }

    private sealed class DiskCounters : IDisposable
    {
        public DiskCounters(
            string name,
            PerformanceCounter averageQueue,
            PerformanceCounter bytesPerSecond,
            PerformanceCounter currentQueue)
        {
            Name = name;
            AverageQueue = averageQueue;
            BytesPerSecond = bytesPerSecond;
            CurrentQueue = currentQueue;
        }

        public string Name { get; }
        public PerformanceCounter AverageQueue { get; }
        public PerformanceCounter BytesPerSecond { get; }
        public PerformanceCounter CurrentQueue { get; }

        public void Dispose()
        {
            AverageQueue.Dispose();
            BytesPerSecond.Dispose();
            CurrentQueue.Dispose();
        }
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
