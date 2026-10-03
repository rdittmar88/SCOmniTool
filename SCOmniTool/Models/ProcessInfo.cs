namespace SCOmniTool.Models;

internal sealed class ProcessInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    public string ExecutablePath { get; set; } = string.Empty;
    public string CommandLine { get; set; } = string.Empty;
    public string Thumbprint { get; set; } = string.Empty;

    public string DisplayName =>
        string.IsNullOrEmpty(Thumbprint) ? Name : Name + " (" + Thumbprint + ")";
}
