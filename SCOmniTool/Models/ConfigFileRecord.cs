using System.Collections.Generic;

namespace SCOmniTool.Models;

internal sealed class ConfigSetting
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

internal sealed class ConfigFileRecord
{
    public string LocationLabel { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool Found { get; set; }
    public string Error { get; set; } = string.Empty;
    public bool ParsedAsXml { get; set; }
    public string RawText { get; set; } = string.Empty;
    public List<ConfigSetting> Settings { get; } = new();
}
