using System;
using System.Collections.Generic;
using System.Diagnostics;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ProcessDiscovery
{
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
                        if (seen.Add(process.Id))
                        {
                            results.Add(new ProcessInfo
                            {
                                Id = process.Id,
                                Name = process.ProcessName
                            });
                        }
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
}
