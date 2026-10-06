using System;
using System.Linq;
using System.Security.Principal;
using SCOmniTool.Discovery;
using SCOmniTool.Events;
using SCOmniTool.Models;
using SCOmniTool.Network;
using SCOmniTool.Reporting;
using SCOmniTool.Security;

namespace SCOmniTool;

internal static class DiagnosticCollector
{
    public static DiagnosticSession Collect(AppOptions options)
    {
        var session = new DiagnosticSession
        {
            Options = options
        };
        Fill(session);
        return session;
    }

    public static void Fill(DiagnosticSession session)
    {
        session.ScannedAt = DateTime.Now;
        session.IsAdministrator = IsAdministrator();

        Console.WriteLine("Scanning for ScreenConnect installations...");
        session.Processes.AddRange(ProcessDiscovery.FindAll(session.Notes));
        session.Services.AddRange(ServiceDiscovery.FindAll(session.Notes));
        session.RegistryKeys.AddRange(RegistryDiscovery.FindAll(session.Notes));
        AddServicesFromRegistry(session);
        session.Clients.AddRange(ClientDiscovery.FromUninstallKeys(session.RegistryKeys, session.Notes));
        session.FileItems.AddRange(FileDiscovery.FindAll(session.Notes));

        Console.WriteLine("Reading configuration files...");
        session.SetConfiguration(ConfigurationDiscovery.FindAll(session.Notes));

        Console.WriteLine("Checking relay addresses...");
        session.SetNetworkResults(RelayProbe.Check(session.Services));

        Console.WriteLine("Reading event logs...");
        session.Events.AddRange(EventLogScanner.Scan(session));

        Console.WriteLine("Reading antivirus...");
        session.AntivirusProducts.AddRange(AntivirusScanner.FindAll(out var antivirusNote));
        session.AntivirusNote = antivirusNote;

        if (session.IsAdministrator)
        {
            Console.WriteLine("Reading Defender actions...");
            session.SetDefenderResult(DefenderActionScanner.Scan(session.Options));
        }
        else
        {
            session.MarkDefenderActionsSkipped();
        }

        Console.WriteLine("Reading system summary...");
        session.SystemSummary = SystemReport.Build();
        Console.WriteLine("Reading a performance snapshot...");
        session.SystemReportText = session.SystemSummary
            + Environment.NewLine
            + Environment.NewLine
            + SystemReport.BuildReportDetails();
    }

    private static void AddServicesFromRegistry(DiagnosticSession session)
    {
        foreach (var key in session.RegistryKeys)
        {
            if (key.Type != RegistryKeyType.ServiceKey)
            {
                continue;
            }

            if (session.Services.Any(service => string.Equals(service.Name, key.KeyName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var info = new ServiceInfo
            {
                Name = key.KeyName,
                DisplayName = key.DisplayValue,
                Status = ServiceDiscovery.ReadStatus(key.KeyName),
                ImagePath = RegistryDiscovery.ReadRegistryValue(key.KeyPath, "ImagePath", session.Notes) ?? string.Empty
            };
            RelayParser.Apply(info);
            session.Services.Add(info);
        }
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
