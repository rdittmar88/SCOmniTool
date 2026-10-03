namespace SCOmniTool.Models;

internal enum RegistryKeyType
{
    ServiceKey,
    ProductKey,
    UniKey
}

internal sealed class RegistryKeyInfo
{
    public RegistryKeyType Type { get; set; }
    public string KeyPath { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public string DisplayValue { get; set; } = string.Empty;
}
