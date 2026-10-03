using System;
using SCOmniTool.Export;
using SCOmniTool.Menu;
using SCOmniTool.Models;
using SCOmniTool.Reporting;
using SCOmniTool.Security;

namespace SCOmniTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        AppOptions options;
        try
        {
            options = AppOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }

        if (options.DefenderActionsOnly)
        {
            return RunDefenderActionsOnly(options);
        }

        try
        {
            var session = DiagnosticCollector.Collect(options);
            ReportWriter.PrintReport(session);

            if (options.Silent)
            {
                return DiagnosticExporter.ExportAutomatic(session, options.SilentZipPath) ? 0 : 1;
            }

            InteractiveMenu.Run(session);
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An unexpected error occurred: " + ex.Message);
            return 1;
        }
    }

    private static int RunDefenderActionsOnly(AppOptions options)
    {
        var outputPath = options.DefenderActionsOutputPath ?? string.Empty;
        if (outputPath.Length == 0)
        {
            Console.WriteLine("Missing /defender-out path.");
            return 1;
        }
        try
        {
            var result = DefenderActionScanner.Scan(options);
            DefenderActionFile.Write(outputPath, result);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                DefenderActionFile.Write(outputPath, new DefenderScanResult
                {
                    Collected = false,
                    Note = "Could not read Defender actions: " + ex.Message
                });
            }
            catch
            {
                Console.WriteLine(ex.Message);
            }

            return 1;
        }
    }
}
