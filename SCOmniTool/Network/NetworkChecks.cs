using SCOmniTool.Models;

namespace SCOmniTool.Network;

internal static class NetworkChecks
{
    public static void Run(DiagnosticSession session)
    {
        session.SetNetworkResults(RelayProbe.Check(session.Services));
        session.SetNetworkEnvironment(NetworkEnvironmentProbe.Check());
    }
}
