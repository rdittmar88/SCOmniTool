using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class ProxySettings
{
    public string UserProxy { get; set; } = string.Empty;
    public string ProxyServer { get; set; } = string.Empty;
    public string ProxyBypass { get; set; } = string.Empty;
    public string AutoConfigUrl { get; set; } = string.Empty;
    public string AutoDetect { get; set; } = string.Empty;
    public string WinHttpProxy { get; set; } = string.Empty;
    public string WinHttpBypass { get; set; } = string.Empty;
    public List<string> EnvironmentProxies { get; } = new();
    public string UserError { get; set; } = string.Empty;
    public string WinHttpError { get; set; } = string.Empty;
}
