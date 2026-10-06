using System;
using System.Collections.Generic;
using SCOmniTool.Security;

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
    public List<ConfigFileRecord> ConfigurationFiles { get; } = new();
    public DateTime ConfigurationScannedAt { get; set; }
    public List<EventInfo> Events { get; } = new();
    public List<string> EventLogsSearched { get; } = new();
    public List<RelayCheckResult> NetworkResults { get; } = new();
    public List<string> Notes { get; } = new();
    public List<AntivirusProduct> AntivirusProducts { get; } = new();
    public string AntivirusNote { get; set; } = string.Empty;
    public string SystemSummary { get; set; } = string.Empty;
    public string SystemReportText { get; set; } = string.Empty;
    public List<EventInfo> DefenderActions { get; } = new();
    public bool DefenderActionsCollected { get; set; }
    public string DefenderActionsNote { get; set; } = string.Empty;

    public string EventWindowDescription =>
        Options.AllEvents
            ? "all retained events"
            : "last " + Options.EventWindowDays + " days";

    public void SetConfiguration(IEnumerable<ConfigFileRecord> files)
    {
        ConfigurationFiles.Clear();
        ConfigurationFiles.AddRange(files);
        ConfigurationScannedAt = DateTime.Now;
    }

    public void SetNetworkResults(IEnumerable<RelayCheckResult> results)
    {
        NetworkResults.Clear();
        NetworkResults.AddRange(results);
    }

    public void SetDefenderResult(DefenderScanResult result)
    {
        DefenderActions.Clear();
        DefenderActions.AddRange(result.Actions);
        DefenderActionsCollected = result.Collected;
        DefenderActionsNote = result.Note ?? string.Empty;
    }

    public void MarkDefenderActionsSkipped()
    {
        DefenderActions.Clear();
        DefenderActionsCollected = false;
        DefenderActionsNote = DefenderActionScanner.SkippedNote;
    }

    public void Clear()
    {
        Processes.Clear();
        Services.Clear();
        Clients.Clear();
        RegistryKeys.Clear();
        FileItems.Clear();
        ConfigurationFiles.Clear();
        ConfigurationScannedAt = default;
        Events.Clear();
        EventLogsSearched.Clear();
        NetworkResults.Clear();
        Notes.Clear();
        AntivirusProducts.Clear();
        AntivirusNote = string.Empty;
        SystemSummary = string.Empty;
        SystemReportText = string.Empty;
        DefenderActions.Clear();
        DefenderActionsCollected = false;
        DefenderActionsNote = string.Empty;
    }
}
