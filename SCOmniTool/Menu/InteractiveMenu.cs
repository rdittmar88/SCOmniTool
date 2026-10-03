using System;
using SCOmniTool.Export;
using SCOmniTool.Models;
using SCOmniTool.Network;
using SCOmniTool.Reporting;

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
            Console.WriteLine("1) Export all relevant diagnostic data");
            Console.WriteLine("2) View network checks");
            Console.WriteLine("3) Rerun network checks");
            Console.WriteLine("4) Dump data and rerun all reports");
            Console.WriteLine("5) Show report again");
            Console.WriteLine("6) Exit");
            Console.Write("Select an option: ");

            var choice = Console.ReadLine()?.Trim();
            try
            {
                switch (choice)
                {
                    case "1":
                        DiagnosticExporter.Export(session);
                        break;
                    case "2":
                        ReportWriter.PrintNetwork(session);
                        break;
                    case "3":
                        Console.WriteLine();
                        Console.WriteLine("Checking relay addresses...");
                        session.SetNetworkResults(RelayProbe.Check(session.Services));
                        ReportWriter.PrintNetwork(session);
                        break;
                    case "4":
                        Console.WriteLine();
                        Console.WriteLine("Clearing saved results and running the report again...");
                        session.Clear();
                        DiagnosticCollector.Fill(session);
                        ReportWriter.PrintReport(session);
                        break;
                    case "5":
                        ReportWriter.PrintReport(session);
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Enter a number from 1 to 6.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("That action failed: " + ex.Message);
            }
        }
    }
}
