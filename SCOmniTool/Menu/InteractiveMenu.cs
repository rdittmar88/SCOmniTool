using System;
using System.IO;
using SCOmniTool.Discovery;
using SCOmniTool.Events;
using SCOmniTool.Export;
using SCOmniTool.Models;
using SCOmniTool.Network;
using SCOmniTool.Reporting;
using SCOmniTool.Security;

namespace SCOmniTool.Menu;

internal static class InteractiveMenu
{
    public static void Run(DiagnosticSession session)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Options");
            Console.WriteLine("=======");
            Console.WriteLine("1) Change Working Directory: " + session.WorkingDirectory);
            Console.WriteLine("2) Export all relevant diagnostic data");
            Console.WriteLine("3) Rerun network checks");
            Console.WriteLine("4) Rerun configuration report");
            Console.WriteLine("5) Dump data and rerun all reports");
            Console.WriteLine("6) Scan additional event log days, up to 30 (may take additional time)");
            Console.WriteLine("7) Check Defender actions");
            Console.WriteLine("8) Exit");
            Console.Write("Select an option: ");

            var choice = Console.ReadLine()?.Trim();
            try
            {
                switch (choice)
                {
                    case "1":
                        ShowWorkingDirectory(session);
                        break;
                    case "2":
                        DiagnosticExporter.Export(session);
                        break;
                    case "3":
                        Console.WriteLine();
                        Console.WriteLine("Checking network...");
                        NetworkChecks.Run(session);
                        ReportWriter.PrintNetwork(session);
                        break;
                    case "4":
                        Console.WriteLine();
                        Console.WriteLine("Reading configuration files...");
                        session.SetConfiguration(ConfigurationDiscovery.FindAll(session.Notes));
                        ReportWriter.PrintConfiguration(session);
                        break;
                    case "5":
                        Console.WriteLine();
                        Console.WriteLine("Clearing saved results and running the report again...");
                        session.Clear();
                        DiagnosticCollector.Fill(session);
                        ReportWriter.PrintReport(session);
                        break;
                    case "6":
                        ScanAdditionalEvents(session);
                        break;
                    case "7":
                        DefenderActionCheck.Run(session);
                        break;
                    case "8":
                        return;
                    default:
                        Console.WriteLine("Enter a number from 1 to 8.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("That action failed: " + ex.Message);
            }
        }
    }

    private static void ShowWorkingDirectory(DiagnosticSession session)
    {
        Console.WriteLine();
        Console.WriteLine("Change Working Directory: " + session.WorkingDirectory);
        Console.WriteLine("Zip files and reports are saved here.");

        while (true)
        {
            Console.Write("New folder [" + session.WorkingDirectory + "]: ");
            var input = Console.ReadLine()?.Trim().Trim('"') ?? string.Empty;
            if (input.Length == 0)
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(input);
            }
            catch (Exception ex)
            {
                Console.WriteLine("That folder path is not valid. " + ex.Message);
                continue;
            }

            if (!Directory.Exists(fullPath))
            {
                if (!ConsolePrompt.AskYesNo("Folder does not exist. Create it? (y/n): "))
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(fullPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not create the folder. " + ex.Message);
                    continue;
                }
            }

            try
            {
                Directory.SetCurrentDirectory(fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not use that folder. " + ex.Message);
                continue;
            }

            session.WorkingDirectory = fullPath;
            Console.WriteLine("Change Working Directory: " + session.WorkingDirectory);
            return;
        }
    }

    private static void ScanAdditionalEvents(DiagnosticSession session)
    {
        Console.WriteLine();
        Console.WriteLine("Scan additional event log days");
        Console.WriteLine("A longer search reads the full message for every matching event and can take additional time.");
        Console.WriteLine("Enter 1 to " + Constants.MaxMenuEventWindowDays + " days. The new window replaces the stored events, and a later export writes those rows to the zip.");
        Console.Write("Days to search, or press Enter to keep " + session.EventWindowDescription + ": ");
        var input = Console.ReadLine()?.Trim() ?? string.Empty;
        if (input.Length == 0)
        {
            return;
        }

        if (!int.TryParse(input, out var days) || days <= 0 || days > Constants.MaxMenuEventWindowDays)
        {
            Console.WriteLine("Enter a number from 1 to " + Constants.MaxMenuEventWindowDays + ".");
            return;
        }

        session.Options.UseEventWindowDays(days);
        session.Events.Clear();
        session.EventLogsSearched.Clear();
        Console.WriteLine("Reading event logs for the last " + days + " days...");
        session.Events.AddRange(EventLogScanner.Scan(session));
        ReportWriter.PrintEvents(session);
    }
}
