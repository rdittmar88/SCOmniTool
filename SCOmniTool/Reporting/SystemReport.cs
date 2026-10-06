using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
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
        try
        {
            try
            {
                cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                cpu.NextValue();
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
            }
            catch (Exception ex)
            {
                builder.AppendLine("Disk: " + ex.Message);
            }

            var started = DateTime.UtcNow;
            var before = SampleProcesses();
            Thread.Sleep(1000);
            var elapsed = DateTime.UtcNow - started;
            var after = SampleProcesses();

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
            AppendTopProcesses(builder, before, after, elapsed);
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

    public static string BuildReportDetails()
    {
        var builder = new StringBuilder();
        AppendDisplayAdapters(builder);
        builder.AppendLine();
        AppendMonitors(builder);
        builder.AppendLine();
        builder.Append(BuildSnapshot());
        return builder.ToString().TrimEnd();
    }

    private static void AppendDisplayAdapters(StringBuilder builder)
    {
        builder.AppendLine("Display adapters");
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterCompatibility, VideoProcessor, DriverVersion, DriverDate, CurrentHorizontalResolution, CurrentVerticalResolution, VideoModeDescription, Status, AdapterRAM FROM Win32_VideoController");
            using var results = searcher.Get();
            var count = 0;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    count++;
                    if (count > 1)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine("  Name: " + Property(item, "Name"));
                    builder.AppendLine("  Manufacturer: " + Property(item, "AdapterCompatibility"));
                    builder.AppendLine("  Video processor: " + Property(item, "VideoProcessor"));
                    builder.AppendLine("  Driver version: " + Property(item, "DriverVersion"));
                    builder.AppendLine("  Driver date: " + FormatDriverDate(item["DriverDate"]));
                    builder.AppendLine("  Resolution: " + FormatResolution(item));
                    builder.AppendLine("  Video mode: " + Property(item, "VideoModeDescription"));
                    builder.AppendLine("  Status: " + Property(item, "Status"));
                    builder.AppendLine("  Adapter RAM: " + FormatAdapterRam(item["AdapterRAM"]));
                }
            }

            if (count == 0)
            {
                builder.AppendLine("  None");
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("  Could not read Win32_VideoController: " + ex.Message);
        }
    }

    private static void AppendMonitors(StringBuilder builder)
    {
        builder.AppendLine("Monitors");
        try
        {
            var monitors = ReadMonitors();
            if (monitors.Count == 0)
            {
                builder.AppendLine("  None");
                return;
            }

            var primary = monitors.FirstOrDefault(monitor =>
                    monitor.X <= 0 &&
                    monitor.Y <= 0 &&
                    monitor.X + (int)monitor.Width > 0 &&
                    monitor.Y + (int)monitor.Height > 0)
                ?? monitors[0];
            var left = monitors.Min(monitor => monitor.X);
            var top = monitors.Min(monitor => monitor.Y);
            var right = monitors.Max(monitor => monitor.X + (int)monitor.Width);
            var bottom = monitors.Max(monitor => monitor.Y + (int)monitor.Height);
            builder.AppendLine(
                "  Virtual desktop: " + (right - left) + " x " + (bottom - top) +
                " at (" + left + ", " + top + ")");

            var ordered = monitors
                .OrderBy(monitor => ReferenceEquals(monitor, primary) ? 0 : 1)
                .ThenBy(monitor => monitor.Y)
                .ThenBy(monitor => monitor.X)
                .ThenBy(monitor => monitor.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var monitor = ordered[i];
                builder.AppendLine();
                builder.AppendLine("  " + (i + 1) + ". " + monitor.Name);
                builder.AppendLine("     Resolution: " + monitor.Width + " x " + monitor.Height);
                builder.AppendLine("     Orientation: " + monitor.Orientation);
                builder.AppendLine("     Position: (" + monitor.X + ", " + monitor.Y + ")");
                builder.AppendLine("     Arrangement: " + DescribeArrangement(monitor, primary, monitors));
                builder.AppendLine("     Refresh rate: " + monitor.Refresh);
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("  Could not read the display configuration: " + ex.Message);
        }
    }

    private static List<MonitorLayout> ReadMonitors()
    {
        const int errorInsufficientBuffer = 122;
        var result = 0;
        var attempts = 0;
        uint pathCount = 0;
        uint modeCount = 0;
        DisplayConfigPathInfo[] paths;
        DisplayConfigModeInfo[] modes;
        do
        {
            attempts++;
            if (attempts > 3)
            {
                throw new InvalidOperationException("GetDisplayConfigBufferSizes kept returning a buffer that was too small");
            }

            result = GetDisplayConfigBufferSizes(QueryDisplayConfigOnlyActivePaths, out pathCount, out modeCount);
            if (result != 0)
            {
                throw new InvalidOperationException("GetDisplayConfigBufferSizes returned " + result);
            }

            if (pathCount == 0)
            {
                return new List<MonitorLayout>();
            }

            paths = new DisplayConfigPathInfo[pathCount];
            modes = new DisplayConfigModeInfo[Math.Max(modeCount, 1)];
            result = QueryDisplayConfig(
                QueryDisplayConfigOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);
        }
        while (result == errorInsufficientBuffer);

        if (result != 0)
        {
            throw new InvalidOperationException("QueryDisplayConfig returned " + result);
        }

        var monitors = new List<MonitorLayout>();
        var count = (int)Math.Min(pathCount, (uint)paths.Length);
        for (var i = 0; i < count; i++)
        {
            var path = paths[i];
            if ((path.flags & DisplayConfigPathActive) == 0)
            {
                continue;
            }

            var sourceIndex = path.sourceInfo.modeInfoIdx;
            if (sourceIndex == uint.MaxValue || sourceIndex >= modeCount || sourceIndex >= modes.Length)
            {
                continue;
            }

            var mode = modes[sourceIndex];
            if (mode.infoType != DisplayConfigModeInfoTypeSource)
            {
                continue;
            }

            monitors.Add(new MonitorLayout
            {
                Name = ReadMonitorName(path, monitors.Count + 1),
                Width = mode.width,
                Height = mode.height,
                X = mode.positionX,
                Y = mode.positionY,
                Orientation = FormatRotation(path.targetInfo.rotation),
                Refresh = FormatRefresh(path.targetInfo.refreshRate.numerator, path.targetInfo.refreshRate.denominator),
                SourceIndex = (int)sourceIndex
            });
        }

        return monitors;
    }

    private static string ReadMonitorName(DisplayConfigPathInfo path, int index)
    {
        var request = new DisplayConfigTargetDeviceName
        {
            header = new DisplayConfigDeviceInfoHeader
            {
                type = DisplayConfigDeviceInfoGetTargetName,
                size = (uint)Marshal.SizeOf(typeof(DisplayConfigTargetDeviceName)),
                adapterId = path.targetInfo.adapterId,
                id = path.targetInfo.id
            }
        };
        if (DisplayConfigGetDeviceInfo(ref request) != 0)
        {
            return "Monitor " + index;
        }

        var friendly = (request.monitorFriendlyDeviceName ?? string.Empty).Trim();
        if (friendly.Length > 0)
        {
            return friendly;
        }

        if ((request.flags & DisplayConfigTargetEdidIdsValid) != 0 && request.edidManufactureId != 0)
        {
            return EdidManufacturer(request.edidManufactureId) + " " +
                request.edidProductCodeId.ToString("X4", CultureInfo.InvariantCulture);
        }

        return "Monitor " + index;
    }

    private static string EdidManufacturer(ushort id)
    {
        var first = (char)(((id >> 10) & 0x1F) + 'A' - 1);
        var second = (char)(((id >> 5) & 0x1F) + 'A' - 1);
        var third = (char)((id & 0x1F) + 'A' - 1);
        return new string(new[] { first, second, third });
    }

    private static string FormatRotation(uint rotation)
    {
        switch (rotation)
        {
            case 1:
                return "Landscape";
            case 2:
                return "Portrait";
            case 3:
                return "Landscape (flipped)";
            case 4:
                return "Portrait (flipped)";
            default:
                return "not available";
        }
    }

    private static string FormatRefresh(uint numerator, uint denominator)
    {
        if (numerator == 0 || denominator == 0)
        {
            return "not available";
        }

        var hertz = numerator / (double)denominator;
        return hertz.ToString("0.##", CultureInfo.InvariantCulture) + " Hz";
    }

    private static string DescribeArrangement(
        MonitorLayout monitor,
        MonitorLayout primary,
        IReadOnlyList<MonitorLayout> monitors)
    {
        var cloned = false;
        foreach (var other in monitors)
        {
            if (!ReferenceEquals(other, monitor) && other.SourceIndex == monitor.SourceIndex)
            {
                cloned = true;
                break;
            }
        }

        if (ReferenceEquals(monitor, primary))
        {
            return cloned ? "Primary (cloned)" : "Primary";
        }

        if (monitor.SourceIndex == primary.SourceIndex)
        {
            return "Cloned with primary";
        }

        var horizontal = string.Empty;
        if (monitor.X + (int)monitor.Width <= primary.X)
        {
            horizontal = "left";
        }
        else if (monitor.X >= primary.X + (int)primary.Width)
        {
            horizontal = "right";
        }

        var vertical = string.Empty;
        if (monitor.Y + (int)monitor.Height <= primary.Y)
        {
            vertical = "above";
        }
        else if (monitor.Y >= primary.Y + (int)primary.Height)
        {
            vertical = "below";
        }

        string arrangement;
        if (horizontal.Length > 0 && vertical.Length > 0)
        {
            arrangement = Capitalize(vertical) + " and " + horizontal + " of primary";
        }
        else if (horizontal.Length > 0)
        {
            arrangement = Capitalize(horizontal) + " of primary";
        }
        else if (vertical.Length > 0)
        {
            arrangement = Capitalize(vertical) + " primary";
        }
        else
        {
            arrangement = "Overlapping primary";
        }

        return cloned ? arrangement + " (cloned)" : arrangement;
    }

    private static string Capitalize(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string FormatDriverDate(object? value)
    {
        var raw = value?.ToString() ?? string.Empty;
        var text = raw.Trim();
        if (text.Length == 0)
        {
            return "not available";
        }

        try
        {
            return ManagementDateTimeConverter.ToDateTime(text).ToString("yyyy-MM-dd");
        }
        catch
        {
            return text;
        }
    }

    private static string FormatResolution(ManagementObject item)
    {
        var width = Property(item, "CurrentHorizontalResolution");
        var height = Property(item, "CurrentVerticalResolution");
        if (width == "not available" || height == "not available")
        {
            return "not available";
        }

        return width + " x " + height;
    }

    private static string FormatAdapterRam(object? value)
    {
        if (value == null)
        {
            return "not available";
        }

        try
        {
            var bytes = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            if (bytes == 0 || bytes == uint.MaxValue)
            {
                return "not available";
            }

            return (bytes / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture) + " MB";
        }
        catch
        {
            return "not available";
        }
    }

    private static List<ProcessSample> SampleProcesses()
    {
        var samples = new List<ProcessSample>();
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch
        {
            return samples;
        }

        foreach (var process in processes)
        {
            using (process)
            {
                try
                {
                    samples.Add(new ProcessSample
                    {
                        Id = process.Id,
                        Name = process.ProcessName,
                        ProcessorTime = process.TotalProcessorTime,
                        WorkingSet = process.WorkingSet64
                    });
                }
                catch
                {
                    // Some processes cannot be read without administrator rights.
                }
            }
        }

        return samples;
    }

    private static void AppendTopProcesses(
        StringBuilder builder,
        List<ProcessSample> before,
        List<ProcessSample> after,
        TimeSpan elapsed)
    {
        builder.AppendLine("Top processes by RAM");
        var byRam = after
            .OrderByDescending(sample => sample.WorkingSet)
            .ThenBy(sample => sample.Id)
            .Take(5)
            .ToList();
        if (byRam.Count == 0)
        {
            builder.AppendLine("  None");
        }
        else
        {
            for (var i = 0; i < byRam.Count; i++)
            {
                var sample = byRam[i];
                builder.AppendLine(
                    "  " + (i + 1) + ". " + sample.Name + "  PID " + sample.Id + "  " + FormatWorkingSet(sample.WorkingSet));
            }
        }

        builder.AppendLine("Top processes by CPU");
        var earlier = new Dictionary<int, TimeSpan>();
        foreach (var sample in before)
        {
            earlier[sample.Id] = sample.ProcessorTime;
        }

        var processorCount = Math.Max(1, Environment.ProcessorCount);
        var elapsedMs = Math.Max(elapsed.TotalMilliseconds, 1d);
        var byCpu = new List<ProcessCpu>();
        foreach (var sample in after)
        {
            if (!earlier.TryGetValue(sample.Id, out var started))
            {
                continue;
            }

            var usedMs = (sample.ProcessorTime - started).TotalMilliseconds;
            if (usedMs < 0)
            {
                usedMs = 0;
            }

            var percent = usedMs / elapsedMs / processorCount * 100d;
            byCpu.Add(new ProcessCpu
            {
                Id = sample.Id,
                Name = sample.Name,
                Percent = percent
            });
        }

        var topCpu = byCpu
            .OrderByDescending(sample => sample.Percent)
            .ThenBy(sample => sample.Id)
            .Take(5)
            .ToList();
        if (topCpu.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        for (var i = 0; i < topCpu.Count; i++)
        {
            var sample = topCpu[i];
            builder.AppendLine(
                "  " + (i + 1) + ". " + sample.Name + "  PID " + sample.Id + "  " +
                sample.Percent.ToString("0.0", CultureInfo.InvariantCulture) + "%");
        }
    }

    private static string FormatWorkingSet(long bytes)
    {
        var megabytes = bytes / 1024d / 1024d;
        return megabytes.ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private sealed class ProcessSample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public TimeSpan ProcessorTime { get; set; }
        public long WorkingSet { get; set; }
    }

    private sealed class ProcessCpu
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Percent { get; set; }
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

    private const uint QueryDisplayConfigOnlyActivePaths = 2;
    private const uint DisplayConfigPathActive = 1;
    private const uint DisplayConfigModeInfoTypeSource = 1;
    private const uint DisplayConfigDeviceInfoGetTargetName = 2;
    private const uint DisplayConfigTargetEdidIdsValid = 4;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DisplayConfigPathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DisplayConfigModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(ref DisplayConfigTargetDeviceName requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigLuid
    {
        public uint lowPart;
        public int highPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigRational
    {
        public uint numerator;
        public uint denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathSourceInfo
    {
        public DisplayConfigLuid adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathTargetInfo
    {
        public DisplayConfigLuid adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DisplayConfigRational refreshRate;
        public uint scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public DisplayConfigPathSourceInfo sourceInfo;
        public DisplayConfigPathTargetInfo targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct DisplayConfigModeInfo
    {
        [FieldOffset(0)]
        public uint infoType;
        [FieldOffset(4)]
        public uint id;
        [FieldOffset(8)]
        public DisplayConfigLuid adapterId;
        [FieldOffset(16)]
        public uint width;
        [FieldOffset(20)]
        public uint height;
        [FieldOffset(28)]
        public int positionX;
        [FieldOffset(32)]
        public int positionY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigDeviceInfoHeader
    {
        public uint type;
        public uint size;
        public DisplayConfigLuid adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayConfigTargetDeviceName
    {
        public DisplayConfigDeviceInfoHeader header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    private sealed class MonitorLayout
    {
        public string Name { get; set; } = string.Empty;
        public uint Width { get; set; }
        public uint Height { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string Orientation { get; set; } = string.Empty;
        public string Refresh { get; set; } = string.Empty;
        public int SourceIndex { get; set; }
    }
}
