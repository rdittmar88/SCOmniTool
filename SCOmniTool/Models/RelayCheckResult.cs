using System;
using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class RelayCheckResult
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool PortAssumed { get; set; }
    public List<string> ServiceNames { get; } = new();
    public bool HostIsIp { get; set; }
    public List<string> ResolvedAddresses { get; } = new();
    public string DnsError { get; set; } = string.Empty;
    public string PortStatus { get; set; } = string.Empty;
    public string PortDetail { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; }
}
