using System;
using System.Globalization;
using System.IO;
using System.Text;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class DefenderActionFile
{
    public static void Write(string path, DefenderScanResult result)
    {
        var builder = new StringBuilder();
        if (!result.Collected)
        {
            builder.AppendLine("ERROR");
            builder.AppendLine(Encode(result.Note));
        }
        else
        {
            builder.AppendLine("OK");
            builder.AppendLine(result.Actions.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var action in result.Actions)
            {
                builder.Append(Encode(FormatTime(action.TimeCreated)));
                builder.Append('\t');
                builder.Append(action.EventId.ToString(CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.AppendLine(Encode(action.Message));
            }
        }

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

    public static DefenderScanResult Read(string path)
    {
        var result = new DefenderScanResult();
        var lines = File.ReadAllLines(path);
        if (lines.Length == 0)
        {
            result.Note = "Defender actions did not produce a result.";
            return result;
        }

        if (string.Equals(lines[0], "ERROR", StringComparison.Ordinal))
        {
            result.Note = lines.Length > 1 ? Decode(lines[1]) : "Could not read Defender actions.";
            return result;
        }

        if (!string.Equals(lines[0], "OK", StringComparison.Ordinal))
        {
            result.Note = "Defender actions returned an unrecognized result.";
            return result;
        }

        result.Collected = true;
        for (var i = 2; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            var parts = lines[i].Split('\t');
            if (parts.Length < 3 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var eventId))
            {
                continue;
            }

            DateTime? time = null;
            if (DateTime.TryParseExact(
                    Decode(parts[0]),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                time = parsed;
            }

            result.Actions.Add(new EventInfo
            {
                TimeCreated = time,
                LogName = DefenderActionScanner.LogName,
                EventId = eventId,
                Message = Decode(parts[2])
            });
        }

        return result;
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

        return value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string Encode(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\r", string.Empty).Replace("\n", "\\n").Replace("\t", " ");
    }

    private static string Decode(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[i + 1];
                if (next == 'n')
                {
                    builder.Append('\n');
                    i++;
                    continue;
                }

                if (next == '\\')
                {
                    builder.Append('\\');
                    i++;
                    continue;
                }
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }
}
