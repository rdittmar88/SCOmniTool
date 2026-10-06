using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class NetworkEnvironment
{
    public List<NetworkAdapterInfo> ActiveAdapters { get; } = new();
    public string AdapterError { get; set; } = string.Empty;
    public List<VpnConnectionInfo> VpnProfiles { get; } = new();
    public string VpnProfileError { get; set; } = string.Empty;
    public List<VpnConnectionInfo> ActiveVpns { get; } = new();
    public string ActiveVpnError { get; set; } = string.Empty;
    public ProxySettings Proxy { get; set; } = new();
}
