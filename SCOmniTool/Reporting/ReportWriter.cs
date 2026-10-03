using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SCOmniTool.Models;

namespace SCOmniTool.Reporting;

internal static class ReportWriter
{
    public static void PrintReport(DiagnosticSession session)
    {
        Console.WriteLine();
        Console.WriteLine(BuildReport(session));
    }

    public static void PrintNetwork(DiagnosticSession session)
    {
        Console.WriteLine();
        Console.WriteLine(BuildNetwork(session));
    }

    public static string BuildReport(DiagnosticSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("ScreenConnect Diagnostic Report");
        builder.AppendLine("Machine: " + Environment.MachineName);
        builder.AppendLine("Scanned: " + session.ScannedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine("Administrator: " + (session.IsAdministrator ? "yes" : "no"));

        AppendProcesses(builder, session);
        AppendServices(builder, session);
        AppendClients(builder, session);
        AppendRegistry(builder, session);
        AppendFiles(builder, session);
        AppendNetwork(builder, session);
        AppendAntivirus(builder, session);
        AppendDefenderActions(builder, session);
        AppendEvents(builder, session);

        return builder.ToString().TrimEnd();
    }

    public static string BuildNetwork(DiagnosticSession session)
    {
        var builder = new StringBuilder();
        AppendNetwork(builder, session);
        return builder.ToString().TrimEnd();
    }

    public static void WriteEventsCsv(IReadOnlyList<EventInfo> events, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Time,Log,Level,Provider,EventId,Message");
        foreach (var entry in events)
        {
            builder.Append(CsvField(FormatTime(entry.TimeCreated)));
            builder.Append(',');
            builder.Append(CsvField(entry.LogName));
            builder.Append(',');
            builder.Append(CsvField(entry.Level));
            builder.Append(',');
            builder.Append(CsvField(entry.Provider));
            builder.Append(',');
            builder.Append(CsvField(entry.EventId.ToString()));
            builder.Append(',');
            builder.Append(CsvField(entry.Message));
            builder.AppendLine();
        }

        System.IO.File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    private static void AppendProcesses(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Processes (" + session.Processes.Count + ")");
        if (session.Processes.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var process in session.Processes.OrderBy(item => item.DisplayName).ThenBy(item => item.Id))
        {
            var path = string.IsNullOrWhiteSpace(process.ExecutablePath)
                ? "Path unavailable"
                : process.ExecutablePath;
            var command = string.IsNullOrWhiteSpace(process.CommandLine)
                ? "Command unavailable"
                : process.CommandLine;
            builder.AppendLine($"  PID {process.Id}  {process.DisplayName}  {process.Status}  {path}");
            builder.AppendLine("    " + command);
        }
    }

    private static void AppendServices(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Services (" + session.Services.Count + ")");
        if (session.Services.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var service in session.Services.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("  " + service.Name);
            if (!string.Equals(service.DisplayName, service.Name, StringComparison.Ordinal))
            {
                builder.AppendLine("    Display name: " + service.DisplayName);
            }

            builder.AppendLine("    Status: " + service.Status);
            builder.AppendLine("    Image path: " + (string.IsNullOrWhiteSpace(service.ImagePath) ? "not available" : service.ImagePath));
            builder.AppendLine("    Relay: " + FormatRelay(service));
        }
    }

    private static void AppendClients(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Clients (" + session.Clients.Count + ")");
        if (session.Clients.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var client in session.Clients.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("  " + client.DisplayName);
            builder.AppendLine("    Registry: " + client.RegistryPath);
            builder.AppendLine("    Install location: " + Display(client.InstallLocation));
            builder.AppendLine("    Uninstall string: " + Display(client.UninstallString));
            if (!string.IsNullOrWhiteSpace(client.QuietUninstallString))
            {
                builder.AppendLine("    Quiet uninstall string: " + client.QuietUninstallString);
            }

            var service = FindService(session, client);
            builder.AppendLine("    Relay: " + (service == null ? "not matched to a service" : FormatRelay(service)));
        }
    }

    private static void AppendRegistry(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Registry keys (" + session.RegistryKeys.Count + ")");
        if (session.RegistryKeys.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var key in session.RegistryKeys
                     .OrderBy(item => item.Type)
                     .ThenBy(item => item.KeyPath, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("  " + RegistryTypeLabel(key.Type));
            builder.AppendLine("    " + key.KeyPath);
            builder.AppendLine("    " + key.DisplayValue);
        }
    }

    private static void AppendFiles(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Files (" + session.FileItems.Count + ")");
        if (session.FileItems.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var item in session.FileItems
                     .OrderBy(entry => entry.Type)
                     .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine("  " + FileTypeLabel(item.Type));
            builder.AppendLine("    " + item.Path);
        }
    }

    private static void AppendNetwork(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Relay network checks (" + session.NetworkResults.Count + ")");
        builder.AppendLine("  These checks only confirm basic network connectivity.");
        builder.AppendLine("  A firewall may still block ScreenConnect-specific traffic when a port shows open.");
        if (session.NetworkResults.Count == 0)
        {
            builder.AppendLine("  No relay addresses were found.");
        }

        foreach (var result in session.NetworkResults)
        {
            builder.AppendLine("  " + result.Host + ":" + result.Port);
            builder.AppendLine("    Services: " + string.Join(", ", result.ServiceNames));
            builder.AppendLine("    Port source: " + (result.PortAssumed ? "assumed default 8041" : "from ImagePath"));
            builder.AppendLine("    DNS: " + FormatDns(result));
            builder.AppendLine("    TCP: " + FormatPort(result));
            builder.AppendLine("    Checked: " + result.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        var missing = session.Services
            .Where(service => string.IsNullOrWhiteSpace(service.RelayHost))
            .Select(service => service.Name)
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        builder.AppendLine("  No relay address in ImagePath:");
        foreach (var name in missing)
        {
            builder.AppendLine("    " + name);
        }
    }

    public static void PrintDefenderActions(DiagnosticSession session)
    {
        var builder = new StringBuilder();
        AppendDefenderActions(builder, session);
        Console.WriteLine();
        Console.WriteLine(builder.ToString().TrimEnd());
    }

    public static string BuildDefenderActions(DiagnosticSession session)
    {
        var builder = new StringBuilder();
        AppendDefenderActions(builder, session);
        return builder.ToString().TrimEnd();
    }

    private static void AppendAntivirus(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Antivirus (" + session.AntivirusProducts.Count + ")");
        if (!string.IsNullOrWhiteSpace(session.AntivirusNote))
        {
            builder.AppendLine("  " + session.AntivirusNote);
        }

        if (session.AntivirusProducts.Count == 0 && string.IsNullOrWhiteSpace(session.AntivirusNote))
        {
            builder.AppendLine("  None");
        }

        foreach (var product in session.AntivirusProducts)
        {
            builder.AppendLine("  " + product.Name);
            builder.AppendLine("    Path: " + product.Path);
            builder.AppendLine("    Product state: " + product.ProductState);
        }
    }

    private static void AppendDefenderActions(StringBuilder builder, DiagnosticSession session)
    {
        var title = session.DefenderActionsCollected
            ? "Defender actions (" + session.DefenderActions.Count + ")"
            : "Defender actions";
        Section(builder, title);
        builder.AppendLine("  Window: " + session.EventWindowDescription);
        if (!session.DefenderActionsCollected)
        {
            builder.AppendLine("  " + (string.IsNullOrWhiteSpace(session.DefenderActionsNote)
                ? "Defender actions were not collected."
                : session.DefenderActionsNote));
            return;
        }

        if (session.DefenderActions.Count == 0)
        {
            builder.AppendLine("  None");
            return;
        }

        foreach (var action in session.DefenderActions)
        {
            builder.AppendLine("  " + FormatTime(action.TimeCreated) + "  " + action.EventId);
            builder.AppendLine("    " + Flatten(action.Message));
        }
    }

    private static void AppendEvents(StringBuilder builder, DiagnosticSession session)
    {
        Section(builder, "Events (" + session.Events.Count + ")");
        builder.AppendLine("  Window: " + session.EventWindowDescription);
        builder.AppendLine("  Logs: " + (session.EventLogsSearched.Count == 0
            ? "none"
            : string.Join(", ", session.EventLogsSearched)));
        builder.AppendLine("  Full event rows are written into the diagnostic zip.");
    }

    private static string Flatten(string value)
    {
        return (value ?? string.Empty).Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
    }

    private static void Section(StringBuilder builder, string title)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(title);
        builder.AppendLine(new string('=', title.Length));
    }

    private static ServiceInfo? FindService(DiagnosticSession session, ClientInfo client)
    {
        return session.Services.FirstOrDefault(service =>
            string.Equals(service.DisplayName, client.DisplayName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(service.Name, client.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatRelay(ServiceInfo service)
    {
        if (string.IsNullOrWhiteSpace(service.RelayHost) || service.RelayPort == null)
        {
            return "not found";
        }

        var portNote = service.PortAssumed ? " (port assumed)" : string.Empty;
        return service.RelayHost + ":" + service.RelayPort.Value + portNote;
    }

    private static string FormatDns(RelayCheckResult result)
    {
        if (result.HostIsIp)
        {
            return "address is an IP literal (" + string.Join(", ", result.ResolvedAddresses) + ")";
        }

        if (!string.IsNullOrWhiteSpace(result.DnsError))
        {
            return "failed: " + result.DnsError;
        }

        if (result.ResolvedAddresses.Count == 0)
        {
            return "no addresses";
        }

        return string.Join(", ", result.ResolvedAddresses);
    }

    private static string FormatPort(RelayCheckResult result)
    {
        if (string.IsNullOrWhiteSpace(result.PortDetail))
        {
            return result.PortStatus;
        }

        return result.PortStatus + " (" + result.PortDetail + ")";
    }

    private static string Display(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "not available" : value;
    }

    private static string RegistryTypeLabel(RegistryKeyType type)
    {
        switch (type)
        {
            case RegistryKeyType.ServiceKey:
                return "Service key";
            case RegistryKeyType.ProductKey:
                return "Product key";
            case RegistryKeyType.UniKey:
                return "Uninstall key";
            default:
                return type.ToString();
        }
    }

    private static string FileTypeLabel(FileItemType type)
    {
        switch (type)
        {
            case FileItemType.ProgramFolder:
                return "Program folder";
            case FileItemType.ClickOnceFolder:
                return "ClickOnce folder (entire Apps\\2.0 tree)";
            case FileItemType.UserConfigFolder:
                return "User config folder";
            case FileItemType.SystemConfigFolder:
                return "System config folder";
            case FileItemType.SystemTempFolder:
                return "System temp folder";
            case FileItemType.DownloadFile:
                return "Download file";
            default:
                return type.ToString();
        }
    }

    private static string FormatTime(DateTime? time)
    {
        if (time == null)
        {
            return string.Empty;
        }

        var value = time.Value;
        if (value.Kind == DateTimeKind.Utc)
        {
            value = value.ToLocalTime();
        }

        return value.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static string CsvField(string? value)
    {
        var text = value ?? string.Empty;
        text = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}
