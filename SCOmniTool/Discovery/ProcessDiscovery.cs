using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ProcessDiscovery
{
    private const int ProcessQueryLimitedInformation = 0x1000;

    private static readonly Regex ThumbprintPattern = new Regex(
        Regex.Escape(Constants.ClientPrefix) + @" \(([^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static List<ProcessInfo> FindAll(ICollection<string> notes)
    {
        var results = new List<ProcessInfo>();
        var seen = new HashSet<int>();

        foreach (var processName in Constants.ProcessNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (Exception ex)
            {
                notes.Add($"Could not list processes named {processName}: {ex.Message}");
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    try
                    {
                        if (!seen.Add(process.Id))
                        {
                            continue;
                        }

                        var info = new ProcessInfo
                        {
                            Id = process.Id,
                            Name = process.ProcessName,
                            Status = ReadStatus(process),
                            ExecutablePath = ReadPath(process, notes),
                            CommandLine = ReadCommandLine(process.Id, process.ProcessName, notes)
                        };
                        info.Thumbprint = ReadThumbprint(info.ExecutablePath);
                        if (string.IsNullOrEmpty(info.Thumbprint))
                        {
                            info.Thumbprint = ReadThumbprint(info.CommandLine);
                        }

                        results.Add(info);
                    }
                    catch (Exception ex)
                    {
                        notes.Add($"Could not read a {processName} process: {ex.Message}");
                    }
                }
            }
        }

        return results;
    }

    private static string ReadStatus(Process process)
    {
        try
        {
            if (!process.Responding)
            {
                return "Not responding";
            }
        }
        catch (Exception)
        {
            // A service often has no window message queue. It is still running.
        }

        return "Running";
    }

    private static string ReadPath(Process process, ICollection<string> notes)
    {
        string? moduleError = null;
        try
        {
            var path = process.MainModule?.FileName ?? string.Empty;
            if (path.Length > 0)
            {
                return path;
            }

            moduleError = "the module path was empty";
        }
        catch (Exception ex)
        {
            moduleError = ex.Message;
        }

        var limited = QueryImagePath(process.Id, out var limitedError);
        if (!string.IsNullOrWhiteSpace(limited))
        {
            return limited;
        }

        var reason = string.IsNullOrWhiteSpace(limitedError) ? moduleError : limitedError;
        notes.Add(
            $"Could not read the executable path for {process.ProcessName} ({process.Id}): {reason ?? "unknown error"}");
        return string.Empty;
    }

    private static string QueryImagePath(int processId, out string error)
    {
        error = string.Empty;
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return string.Empty;
        }

        try
        {
            var capacity = 32768;
            var buffer = new StringBuilder(capacity);
            if (!QueryFullProcessImageName(handle, 0, buffer, ref capacity))
            {
                error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                return string.Empty;
            }

            return buffer.ToString();
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    private static string ReadCommandLine(int processId, string processName, ICollection<string> notes)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + processId);
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var command = item["CommandLine"] as string ?? string.Empty;
                    if (command.Length > 0)
                    {
                        return command;
                    }
                }
            }

            notes.Add($"Could not read the start command for {processName} ({processId}).");
            return string.Empty;
        }
        catch (Exception ex)
        {
            notes.Add($"Could not read the start command for {processName} ({processId}): {ex.Message}");
            return string.Empty;
        }
    }

    private static string ReadThumbprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var match = ThumbprintPattern.Match(value);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(
        IntPtr process,
        int flags,
        StringBuilder exeName,
        ref int size);
}
