namespace SCOmniTool;

internal static class Constants
{
    public const string ClientPrefix = "ScreenConnect Client";
    public const string ScreenConnectToken = "ScreenConnect";
    public const string ProcessClientService = "ScreenConnect.ClientService";
    public const string ProcessWindowsClient = "ScreenConnect.WindowsClient";
    public const string ProcessWindowsBackstageShell = "ScreenConnect.WindowsBackstageShell";
    public const int DefaultRelayPort = 8041;
    public const int DefaultEventWindowDays = 10;
    public const int NetworkTimeoutMs = 5000;
    public const int DxDiagTimeoutMs = 180000;

    public static readonly string[] ProcessNames =
    {
        ProcessClientService,
        ProcessWindowsClient,
        ProcessWindowsBackstageShell
    };

    public static readonly string[] SkippedUserProfiles =
    {
        "Default",
        "Default User",
        "Public",
        "All Users"
    };
}
