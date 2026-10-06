using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class SecuritySoftwareFinding
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<InstalledSecurityProgram> Programs { get; } = new();
    public List<SecurityServiceHit> Services { get; } = new();
}

internal sealed class InstalledSecurityProgram
{
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
}

internal sealed class SecurityServiceHit
{
    public string ServiceName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
