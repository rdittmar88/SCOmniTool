using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using SCOmniTool.Models;

namespace SCOmniTool.Network;

internal static class RelayProbe
{
    public static List<RelayCheckResult> Check(IReadOnlyList<ServiceInfo> services)
    {
        var groups = services
            .Where(service => !string.IsNullOrWhiteSpace(service.RelayHost) && service.RelayPort.HasValue)
            .GroupBy(
                service => service.RelayHost.Trim() + ":" + service.RelayPort!.Value,
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.First().RelayHost, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.First().RelayPort);

        var results = new List<RelayCheckResult>();
        foreach (var group in groups)
        {
            var first = group.First();
            var result = new RelayCheckResult
            {
                Host = first.RelayHost.Trim(),
                Port = first.RelayPort!.Value,
                PortAssumed = group.All(service => service.PortAssumed)
            };

            foreach (var service in group)
            {
                if (!result.ServiceNames.Any(name => string.Equals(name, service.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.ServiceNames.Add(service.Name);
                }
            }

            Console.WriteLine($"Checking {result.Host}:{result.Port}...");
            try
            {
                CheckOne(result);
            }
            catch (Exception ex)
            {
                result.CheckedAt = DateTime.Now;
                result.PortStatus = "timed out";
                result.PortDetail = ex.Message;
            }

            results.Add(result);
        }

        if (results.Count == 0)
        {
            Console.WriteLine("No relay addresses to check.");
        }

        return results;
    }

    private static void CheckOne(RelayCheckResult result)
    {
        result.CheckedAt = DateTime.Now;
        ResolveDns(result);
        ProbePort(result);
    }

    private static void ResolveDns(RelayCheckResult result)
    {
        if (IPAddress.TryParse(result.Host, out var address))
        {
            result.HostIsIp = true;
            result.ResolvedAddresses.Add(address.ToString());
            return;
        }

        try
        {
            var addresses = Dns.GetHostAddresses(result.Host);
            if (addresses.Length == 0)
            {
                result.DnsError = "No addresses returned.";
                return;
            }

            foreach (var item in addresses)
            {
                result.ResolvedAddresses.Add(item.ToString());
            }
        }
        catch (Exception ex)
        {
            result.DnsError = ex.Message;
        }
    }

    private static void ProbePort(RelayCheckResult result)
    {
        var client = new TcpClient();
        try
        {
            var connectTask = client.ConnectAsync(result.Host, result.Port);
            connectTask.ContinueWith(
                task =>
                {
                    var ignored = task.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

            if (!connectTask.Wait(Constants.NetworkTimeoutMs))
            {
                result.PortStatus = "timed out";
                return;
            }

            result.PortStatus = "open";
        }
        catch (AggregateException ex)
        {
            var error = ex.GetBaseException();
            if (error is SocketException socketError && socketError.SocketErrorCode == SocketError.ConnectionRefused)
            {
                result.PortStatus = "closed";
                return;
            }

            result.PortStatus = "timed out";
            result.PortDetail = error.Message;
        }
        finally
        {
            try
            {
                client.Close();
            }
            catch
            {
                // A timed-out socket can throw while it is closing.
            }
        }
    }
}
