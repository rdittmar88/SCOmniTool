using System;
using System.Text.RegularExpressions;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class RelayParser
{
    private static readonly Regex HostRegex = new Regex(
        @"[?&]h=([^&""]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PortRegex = new Regex(
        @"[?&]p=([^&""]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static void Apply(ServiceInfo service)
    {
        service.RelayHost = string.Empty;
        service.RelayPort = null;
        service.PortAssumed = false;

        if (string.IsNullOrWhiteSpace(service.ImagePath))
        {
            return;
        }

        var hostMatch = HostRegex.Match(service.ImagePath);
        if (!hostMatch.Success)
        {
            return;
        }

        var host = Unescape(hostMatch.Groups[1].Value).Trim();
        if (host.Length == 0)
        {
            return;
        }

        service.RelayHost = host;

        var portMatch = PortRegex.Match(service.ImagePath);
        if (portMatch.Success &&
            int.TryParse(Unescape(portMatch.Groups[1].Value), out var port) &&
            port > 0 &&
            port <= 65535)
        {
            service.RelayPort = port;
            service.PortAssumed = false;
            return;
        }

        service.RelayPort = Constants.DefaultRelayPort;
        service.PortAssumed = true;
    }

    private static string Unescape(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
