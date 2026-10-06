namespace SCOmniTool.Models;

internal sealed class WindowsFirewallStatus
{
    public string ServiceStatus { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Private { get; set; } = string.Empty;
    public string Public { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}
