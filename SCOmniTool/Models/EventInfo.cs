using System;

namespace SCOmniTool.Models;

internal sealed class EventInfo
{
    public DateTime? TimeCreated { get; set; }
    public string LogName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string Message { get; set; } = string.Empty;
}
