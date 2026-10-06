using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class NetworkAdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string Speed { get; set; } = string.Empty;
    public string Dhcp { get; set; } = string.Empty;
    public List<string> Addresses { get; } = new();
    public List<string> Gateways { get; } = new();
    public List<string> DnsServers { get; } = new();
}
