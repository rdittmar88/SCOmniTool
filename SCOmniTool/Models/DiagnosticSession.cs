using System;
using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class DiagnosticSession
{
    public AppOptions Options { get; set; } = new AppOptions();
    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;
    public bool IsAdministrator { get; set; }
    public DateTime ScannedAt { get; set; }
    public List<ProcessInfo> Processes { get; } = new();
    public List<ServiceInfo> Services { get; } = new();
    public List<ClientInfo> Clients { get; } = new();
    public List<RegistryKeyInfo> RegistryKeys { get; } = new();
    public List<FileItemInfo> FileItems { get; } = new();
    public List<EventInfo> Events { get; } = new();
    public List<string> EventLogsSearched { get; } = new();
    public List<RelayCheckResult> NetworkResults { get; } = new();
    public List<string> Notes { get; } = new();

    public string EventWindowDescription =>
        Options.AllEvents
            ? "all retained events"
            : "last " + Options.EventWindowDays + " days";

    public void SetNetworkResults(IEnumerable<RelayCheckResult> results)
    {
        NetworkResults.Clear();
        NetworkResults.AddRange(results);
    }

    public void Clear()
    {
        Processes.Clear();
        Services.Clear();
        Clients.Clear();
        RegistryKeys.Clear();
        FileItems.Clear();
        Events.Clear();
        EventLogsSearched.Clear();
        NetworkResults.Clear();
        Notes.Clear();
    }
}
