using System;
using System.Diagnostics.Eventing.Reader;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class DefenderActionScanner
{
    public const string LogName = "Microsoft-Windows-Windows Defender/Operational";
    public const string SkippedNote =
        "Defender actions were not collected. Choose Check Defender actions to approve administrator access.";

    private static readonly int[] ActionEventIds = { 1006, 1007, 1008, 1015, 1116, 1117, 1118, 1119 };

    public static DefenderScanResult Scan(AppOptions options)
    {
        var result = new DefenderScanResult { Collected = true };

        try
        {
            var query = new EventLogQuery(LogName, PathType.LogName, BuildQuery(options))
            {
                ReverseDirection = false,
                TolerateQueryErrors = true
            };

            using var reader = new EventLogReader(query);
            for (var entry = reader.ReadEvent(); entry != null; entry = reader.ReadEvent())
            {
                using (entry)
                {
                    var info = TryCreate(entry);
                    if (info != null)
                    {
                        result.Actions.Add(info);
                    }
                }
            }

            result.Actions.Sort((left, right) => Nullable.Compare(left.TimeCreated, right.TimeCreated));
        }
        catch (Exception ex)
        {
            result.Collected = false;
            result.Note = "Could not read Defender actions: " + ex.Message;
            result.Actions.Clear();
        }

        return result;
    }

    private static string BuildQuery(AppOptions options)
    {
        var ids = new string[ActionEventIds.Length];
        for (var i = 0; i < ActionEventIds.Length; i++)
        {
            ids[i] = "EventID=" + ActionEventIds[i];
        }

        var idFilter = "(" + string.Join(" or ", ids) + ")";
        if (options.AllEvents)
        {
            return "*[System[" + idFilter + "]]";
        }

        var milliseconds = (long)options.EventWindowDays * 24L * 60L * 60L * 1000L;
        return "*[System[" + idFilter + " and TimeCreated[timediff(@SystemTime) <= " + milliseconds + "]]]";
    }

    private static EventInfo? TryCreate(EventRecord entry)
    {
        if (Array.IndexOf(ActionEventIds, entry.Id) < 0)
        {
            return null;
        }

        string xml;
        try
        {
            xml = entry.ToXml() ?? string.Empty;
        }
        catch
        {
            xml = string.Empty;
        }

        var message = ReadMessage(entry);
        var mentionsScreenConnect =
            xml.IndexOf(Constants.ScreenConnectToken, StringComparison.OrdinalIgnoreCase) >= 0 ||
            message.IndexOf(Constants.ScreenConnectToken, StringComparison.OrdinalIgnoreCase) >= 0;
        if (!mentionsScreenConnect)
        {
            return null;
        }

        return new EventInfo
        {
            TimeCreated = entry.TimeCreated,
            LogName = LogName,
            Level = entry.Level?.ToString() ?? string.Empty,
            Provider = entry.ProviderName ?? string.Empty,
            EventId = entry.Id,
            Message = message
        };
    }

    private static string ReadMessage(EventRecord entry)
    {
        try
        {
            return entry.FormatDescription() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}

internal sealed class DefenderScanResult
{
    public bool Collected { get; set; }
    public string Note { get; set; } = string.Empty;
    public System.Collections.Generic.List<EventInfo> Actions { get; } = new();
}
