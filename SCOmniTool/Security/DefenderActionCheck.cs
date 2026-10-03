using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using SCOmniTool.Models;
using SCOmniTool.Reporting;

namespace SCOmniTool.Security;

internal static class DefenderActionCheck
{
    public static void Run(DiagnosticSession session)
    {
        Console.WriteLine();
        DefenderScanResult result;
        if (session.IsAdministrator)
        {
            Console.WriteLine("Reading Defender actions...");
            result = DefenderActionScanner.Scan(session.Options);
        }
        else
        {
            result = RunElevated(session.Options);
        }

        session.SetDefenderResult(result);
        ReportWriter.PrintDefenderActions(session);
    }

    private static DefenderScanResult RunElevated(AppOptions options)
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "SCOmniTool-defender-" + Guid.NewGuid().ToString("N") + ".txt");
        var executable = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(executable))
        {
            executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SCOmniTool.exe");
        }

        var arguments = "/defender-actions \"/defender-out:" + outputPath + "\"";
        arguments += options.AllEvents
            ? " /all"
            : " /days:" + options.EventWindowDays;

        Console.WriteLine("Administrator approval is required to read Defender actions.");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas"
            });
            if (process == null)
            {
                return Failed("Defender actions could not be started.");
            }

            process.WaitForExit();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Failed("Administrator approval was not granted.");
        }
        catch (Exception ex)
        {
            return Failed("Could not read Defender actions: " + ex.Message);
        }

        if (!File.Exists(outputPath))
        {
            return Failed("Defender actions did not produce a result.");
        }

        try
        {
            return DefenderActionFile.Read(outputPath);
        }
        catch (Exception ex)
        {
            return Failed("Could not read Defender actions: " + ex.Message);
        }
        finally
        {
            try
            {
                File.Delete(outputPath);
            }
            catch
            {
                // The temp result is only needed until it has been read.
            }
        }
    }

    private static DefenderScanResult Failed(string note)
    {
        return new DefenderScanResult
        {
            Collected = false,
            Note = note
        };
    }
}
