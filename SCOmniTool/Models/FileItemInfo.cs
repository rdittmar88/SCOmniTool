namespace SCOmniTool.Models;

internal enum FileItemType
{
    ProgramFolder,
    ClickOnceFolder,
    UserConfigFolder,
    SystemConfigFolder,
    SystemTempFolder,
    DownloadFile
}

internal sealed class FileItemInfo
{
    public FileItemType Type { get; set; }
    public string Path { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
}
