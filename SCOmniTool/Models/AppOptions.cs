using System;
using System.IO;

namespace SCOmniTool.Models;

internal sealed class AppOptions
{
    public bool Silent { get; private set; }
    public string? SilentZipPath { get; private set; }
    public bool DefenderActionsOnly { get; private set; }
    public string? DefenderActionsOutputPath { get; private set; }
    public bool AllEvents { get; private set; }
    public int EventWindowDays { get; private set; } = Constants.DefaultEventWindowDays;

    public static AppOptions Parse(string[] args)
    {
        var options = new AppOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "/s", StringComparison.OrdinalIgnoreCase))
            {
                options.Silent = true;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("/"))
                {
                    options.SilentZipPath = NormalizeZipPath(args[++i]);
                }

                continue;
            }

            if (arg.StartsWith("/s:", StringComparison.OrdinalIgnoreCase))
            {
                options.Silent = true;
                options.SilentZipPath = NormalizeZipPath(arg.Substring(3));
                continue;
            }

            if (string.Equals(arg, "/defender-actions", StringComparison.OrdinalIgnoreCase))
            {
                options.DefenderActionsOnly = true;
                continue;
            }

            if (arg.StartsWith("/defender-out:", StringComparison.OrdinalIgnoreCase))
            {
                options.DefenderActionsOutputPath = arg.Substring("/defender-out:".Length).Trim().Trim('"');
                continue;
            }

            if (string.Equals(arg, "/all", StringComparison.OrdinalIgnoreCase))
            {
                options.AllEvents = true;
                continue;
            }

            if (arg.StartsWith("/days:", StringComparison.OrdinalIgnoreCase))
            {
                options.EventWindowDays = ParseDays(arg.Substring("/days:".Length));
                continue;
            }

            if (string.Equals(arg, "/days", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException("Usage: /days:N where N is a positive number of days.");
                }

                options.EventWindowDays = ParseDays(args[++i]);
                continue;
            }

            throw new ArgumentException("Unknown argument: " + arg + ". Supported arguments: /s [zip path] /days:N /all");
        }

        return options;
    }

    private static string NormalizeZipPath(string value)
    {
        value = value.Trim().Trim('"');
        if (value.Length == 0)
        {
            throw new ArgumentException("Usage: /s [zip path]. Example: SCOmniTool.exe /s C:\\Reports\\machine.zip");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(value);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("That zip path is not valid. " + ex.Message);
        }

        if (!fullPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            fullPath += ".zip";
        }

        var fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("That zip file name is not valid.");
        }

        return fullPath;
    }

    private static int ParseDays(string value)
    {
        if (!int.TryParse(value, out var days) || days <= 0)
        {
            throw new ArgumentException("Usage: /days:N where N is a positive number of days.");
        }

        return days;
    }
}
