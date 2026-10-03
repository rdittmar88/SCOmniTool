using System;

namespace SCOmniTool.Models;

internal sealed class AppOptions
{
    public bool Silent { get; private set; }
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

            throw new ArgumentException("Unknown argument: " + arg + ". Supported arguments: /s /days:N /all");
        }

        return options;
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
