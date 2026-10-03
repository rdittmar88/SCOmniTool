namespace SCOmniTool.Models;

internal sealed class ClientInfo
{
    public string KeyName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UninstallString { get; set; } = string.Empty;
    public string QuietUninstallString { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
}
