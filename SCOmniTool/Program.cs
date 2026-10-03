using System;
using SCOmniTool.Export;
using SCOmniTool.Menu;
using SCOmniTool.Models;
using SCOmniTool.Reporting;

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
}
