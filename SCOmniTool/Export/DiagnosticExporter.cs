using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Management;
using System.Text;
using SCOmniTool.Models;
using SCOmniTool.Reporting;
using SCOmniTool.Security;

namespace SCOmniTool.Export;

internal static class DiagnosticExporter
{
    public static bool Export(DiagnosticSession session)
    {
        return WriteZip(session, session.WorkingDirectory, null, promptForName: true, missingFolderHint: "Choose option 1 to set it again.");
    }

    public static bool ExportAutomatic(DiagnosticSession session, string? zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            return WriteZip(session, GetExecutableDirectory(), null, promptForName: false, missingFolderHint: null);
        }

        var folder = Path.GetDirectoryName(zipPath);
        var fileName = Path.GetFileName(zipPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName))
        {
            Console.WriteLine("That zip path is not valid.");
            return false;
        }

        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Could not create the folder. " + ex.Message);
                return false;
            }
        }

        return WriteZip(session, folder, fileName, promptForName: false, missingFolderHint: null);
    }

    private static bool WriteZip(
        DiagnosticSession session,
        string folder,
        string? fileNameOverride,
        bool promptForName,
        string? missingFolderHint)
    {
        if (!Directory.Exists(folder))
        {
            Console.WriteLine("Working directory was not found: " + folder);
            if (!string.IsNullOrEmpty(missingFolderHint))
            {
                Console.WriteLine(missingFolderHint);
            }

            return false;
        }

        Console.WriteLine("Saving to " + folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var machine = SanitizeFileName(Environment.MachineName);
        var defaultName = machine + "_" + stamp + "_diagnostics.zip";
        var fileName = promptForName ? PromptFileName(defaultName) : (fileNameOverride ?? defaultName);
        if (fileName == null)
        {
            return false;
        }

        var destination = Path.Combine(folder, fileName);
        if (promptForName &&
            File.Exists(destination) &&
            !ConsolePrompt.AskYesNo("File already exists. Replace it? (y/n): "))
        {
            Console.WriteLine("Export cancelled.");
            return false;
        }

        string? tempRoot = null;
        string? tempZip = null;
        try
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "SCOmniTool-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            File.WriteAllText(
                Path.Combine(tempRoot, "full-report.txt"),
                ReportWriter.BuildReport(session) + Environment.NewLine,
                Encoding.UTF8);
            ReportWriter.WriteEventsCsv(
                session.Events,
                Path.Combine(tempRoot, "eventviewerlogs.csv"));
            File.WriteAllText(
                Path.Combine(tempRoot, "network-report.txt"),
                "Network report" + Environment.NewLine +
                ReportWriter.BuildNetwork(session) + Environment.NewLine,
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(tempRoot, "configuration-report.txt"),
                "Configuration report" + Environment.NewLine +
                ReportWriter.BuildConfiguration(session) + Environment.NewLine,
                Encoding.UTF8);

            Console.WriteLine("Running dxdiag. This can take up to a minute...");
            WriteDxDiag(Path.Combine(tempRoot, "dxdiag.txt"));

            Console.WriteLine("Reading security software...");
            File.WriteAllText(
                Path.Combine(tempRoot, "anti-virus-report.txt"),
                BuildSecuritySoftwareReport(session) +
                Environment.NewLine +
                ReportWriter.BuildDefenderActions(session) + Environment.NewLine,
                Encoding.UTF8);

            Console.WriteLine("Reading system summary...");
            File.WriteAllText(
                Path.Combine(tempRoot, "system-report.txt"),
                (SystemReportTextForZip(session)) + Environment.NewLine,
                Encoding.UTF8);

            Console.WriteLine("Writing zip...");
            tempZip = Path.Combine(Path.GetTempPath(), "SCOmniTool-" + Guid.NewGuid().ToString("N") + ".zip");
            ZipFile.CreateFromDirectory(tempRoot, tempZip, CompressionLevel.Optimal, includeBaseDirectory: false);
            PublishZip(tempZip, destination);
            Console.WriteLine();
            Console.WriteLine("Saved " + destination);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine("Export failed: " + ex.Message);
            return false;
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
            TryDeleteFile(tempZip);
        }
    }

    private static string GetExecutableDirectory()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            return Path.GetFullPath(baseDirectory);
        }

        var processPath = Process.GetCurrentProcess().MainModule?.FileName;
        var directory = string.IsNullOrWhiteSpace(processPath) ? null : Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return Environment.CurrentDirectory;
        }

        return Path.GetFullPath(directory);
    }

    internal static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            builder.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
        }

        var sanitized = builder.ToString().Trim();
        return sanitized.Length == 0 ? "computer" : sanitized;
    }

    private static string? PromptFileName(string defaultName)
    {
        while (true)
        {
            Console.Write("File name [" + defaultName + "]: ");
            var input = ReadTrimmed();
            var name = string.IsNullOrEmpty(input) ? defaultName : input;
            if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                name += ".zip";
            }

            if (name.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                Console.WriteLine("Enter a file name only. The file is saved in the working directory.");
                continue;
            }

            if (!IsSafeFileName(name))
            {
                Console.WriteLine("That file name is not valid.");
                continue;
            }

            return name;
        }
    }

    private static bool IsSafeFileName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               name != "." &&
               name != "..";
    }

    private static string ReadTrimmed()
    {
        var value = Console.ReadLine()?.Trim() ?? string.Empty;
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            value = value.Substring(1, value.Length - 2).Trim();
        }

        return value;
    }

    private static void WriteDxDiag(string path)
    {
        try
        {
            var dxdiag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dxdiag.exe");
            if (!File.Exists(dxdiag))
            {
                File.WriteAllText(path, "dxdiag.exe was not found at " + dxdiag + ".", Encoding.UTF8);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = dxdiag,
                Arguments = "/t \"" + path + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                File.WriteAllText(path, "dxdiag could not be started.", Encoding.UTF8);
                return;
            }

            if (!process.WaitForExit(Constants.DxDiagTimeoutMs))
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // The process may already have exited.
                }

                if (!File.Exists(path))
                {
                    File.WriteAllText(path, "dxdiag did not finish within 3 minutes.", Encoding.UTF8);
                }
                else
                {
                    Console.WriteLine("dxdiag was still running and was stopped. Any output already written was kept.");
                }

                return;
            }

            if (!File.Exists(path))
            {
                File.WriteAllText(
                    path,
                    "dxdiag exited without writing a report. Exit code: " + process.ExitCode,
                    Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "dxdiag could not be collected: " + ex.Message, Encoding.UTF8);
            }
            else
            {
                Console.WriteLine("dxdiag reported an error after writing output: " + ex.Message);
            }
        }
    }

    private static void PublishZip(string tempZip, string destination)
    {
        var partial = destination + ".partial";
        File.Copy(tempZip, partial, true);
        try
        {
            if (File.Exists(destination))
            {
                File.Replace(partial, destination, null);
            }
            else
            {
                File.Move(partial, destination);
            }
        }
        finally
        {
            TryDeleteFile(partial);
        }
    }

    private static string SystemReportTextForZip(DiagnosticSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.SystemReportText))
        {
            return session.SystemReportText;
        }

        var summary = string.IsNullOrWhiteSpace(session.SystemSummary)
            ? SystemReport.Build()
            : session.SystemSummary;
        return summary + Environment.NewLine + Environment.NewLine + SystemReport.BuildReportDetails();
    }

    private static string BuildSecuritySoftwareReport(DiagnosticSession session)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Antivirus report");
        builder.AppendLine("Machine: " + Environment.MachineName);
        builder.AppendLine("Collected: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        builder.AppendLine();
        builder.AppendLine("Antivirus");
        builder.AppendLine("---------");
        if (!string.IsNullOrWhiteSpace(session.AntivirusNote))
        {
            builder.AppendLine(session.AntivirusNote);
            builder.AppendLine();
        }
        else if (session.AntivirusProducts.Count == 0)
        {
            builder.AppendLine("No products reported.");
            builder.AppendLine();
        }
        else
        {
            foreach (var product in session.AntivirusProducts)
            {
                builder.AppendLine("Name: " + product.Name);
                builder.AppendLine("Path: " + product.Path);
                builder.AppendLine("Product state: " + product.ProductState);
                builder.AppendLine();
            }
        }

        ReportWriter.AppendFirewallReport(builder, session);

        AppendSecurityProducts(builder, "AntiSpywareProduct", "Antispyware");
        AppendSecurityProducts(builder, "FirewallProduct", "Firewall");
        builder.AppendLine();
        builder.AppendLine(ReportWriter.BuildSecuritySoftware(session));
        return builder.ToString();
    }

    private static void AppendSecurityProducts(StringBuilder builder, string className, string heading)
    {
        builder.AppendLine(heading);
        builder.AppendLine(new string('-', heading.Length));

        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\SecurityCenter2",
                "SELECT displayName, pathToSignedProductExe, productState FROM " + className);
            using var results = searcher.Get();
            var readings = new List<SecurityProductReading>();
            foreach (ManagementObject product in results)
            {
                using (product)
                {
                    readings.Add(ProductStateText.Read(
                        Property(product, "displayName"),
                        Property(product, "pathToSignedProductExe"),
                        product["productState"]));
                }
            }

            if (readings.Count == 0)
            {
                builder.AppendLine("No products reported.");
                builder.AppendLine();
            }
            else
            {
                var descriptions = ProductStateText.Describe(readings);
                for (var i = 0; i < readings.Count; i++)
                {
                    builder.AppendLine("Name: " + readings[i].Name);
                    builder.AppendLine("Path: " + readings[i].Path);
                    builder.AppendLine("Product state: " + descriptions[i]);
                    builder.AppendLine();
                }
            }
        }
        catch (Exception ex)
        {
            builder.AppendLine("Could not read " + className + ": " + ex.Message);
            builder.AppendLine();
        }
    }

    private static string Property(ManagementObject item, string name)
    {
        try
        {
            var value = item[name]?.ToString();
            if (value == null || string.IsNullOrWhiteSpace(value))
            {
                return "not available";
            }

            return value.Trim();
        }
        catch
        {
            return "not available";
        }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // Leave the temp folder if Windows still has a file open.
        }
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch
        {
            // Leave the temp zip if it could not be moved.
        }
    }
}
