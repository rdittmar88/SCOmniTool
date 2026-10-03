using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using SCOmniTool.Models;

namespace SCOmniTool.Events;

internal static class EventLogScanner
{
    private static readonly string[] RequiredLogs = { "Application", "System" };

    public static List<EventInfo> Scan(DiagnosticSession session)
    {
        var logs = new List<string>(RequiredLogs);
        try
        {
            using var logSession = new EventLogSession();
            foreach (var name in logSession.GetLogNames())
            {
                if (name.IndexOf(Constants.ScreenConnectToken, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (logs.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                logs.Add(name);
            }
        }
        catch (Exception ex)
        {
            session.Notes.Add("Could not list event logs: " + ex.Message);
        }

        session.EventLogsSearched.AddRange(logs);

        var events = new List<EventInfo>();
        var queryText = BuildQuery(session.Options);
        foreach (var logName in logs)
        {
            Console.WriteLine("Reading " + logName + "...");
            try
            {
                events.AddRange(ReadLog(logName, queryText));
            }
            catch (Exception ex)
            {
                session.Notes.Add($"Could not read log {logName}: {ex.Message}");
            }
        }

        events.Sort((left, right) => Nullable.Compare(left.TimeCreated, right.TimeCreated));
        return events;
    }

    private static string BuildQuery(AppOptions options)
    {
        const string levels = "(Level=1 or Level=2 or Level=4)";
        if (options.AllEvents)
        {
            return "*[System[" + levels + "]]";
        }

        var milliseconds = (long)options.EventWindowDays * 24L * 60L * 60L * 1000L;
        return "*[System[" + levels + " and TimeCreated[timediff(@SystemTime) <= " + milliseconds + "]]]";
    }

    private static List<EventInfo> ReadLog(string logName, string queryText)
    {
        var events = new List<EventInfo>();
        var query = new EventLogQuery(logName, PathType.LogName, queryText)
        {
            ReverseDirection = false,
            TolerateQueryErrors = true
        };

        using var reader = new EventLogReader(query);
        for (var entry = reader.ReadEvent(); entry != null; entry = reader.ReadEvent())
        {
            using (entry)
            {
                var info = TryCreate(logName, entry);
                if (info != null)
                {
                    events.Add(info);
                }
            }
        }

        return events;
    }

    private static EventInfo? TryCreate(string logName, EventRecord entry)
    {
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

        var level = entry.Level;
        if (level != 1 && level != 2 && level != 4)
        {
            return null;
        }

        return new EventInfo
        {
            TimeCreated = entry.TimeCreated,
            LogName = logName,
            Level = LevelName(level),
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

    private static string LevelName(byte? level)
    {
        switch (level)
        {
            case 1:
                return "Critical";
            case 2:
                return "Error";
            case 4:
                return "Information";
            default:
                return level?.ToString() ?? string.Empty;
        }
    }
}
