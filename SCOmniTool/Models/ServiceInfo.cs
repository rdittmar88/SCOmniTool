namespace SCOmniTool.Models;

internal sealed class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string RelayHost { get; set; } = string.Empty;
    public int? RelayPort { get; set; }
    public bool PortAssumed { get; set; }
}
