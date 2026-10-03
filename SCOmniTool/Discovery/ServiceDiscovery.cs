using System;
using System.Collections.Generic;
using System.ServiceProcess;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ServiceDiscovery
{
    public static List<ServiceInfo> FindAll(ICollection<string> notes)
    {
        var results = new List<ServiceInfo>();

        ServiceController[] services;
        try
        {
            services = ServiceController.GetServices();
        }
        catch (Exception ex)
        {
            notes.Add("Could not list Windows services: " + ex.Message);
            return results;
        }

        foreach (var service in services)
        {
            using (service)
            {
                try
                {
                    if (!service.ServiceName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var info = new ServiceInfo
                    {
                        Name = service.ServiceName,
                        DisplayName = service.DisplayName ?? service.ServiceName,
                        Status = ReadStatus(service, notes),
                        ImagePath = RegistryDiscovery.ReadRegistryValue(
                            ServiceKeyPath(service.ServiceName),
                            "ImagePath",
                            notes) ?? string.Empty
                    };
                    RelayParser.Apply(info);
                    results.Add(info);
                }
                catch (Exception ex)
                {
                    notes.Add("Could not read a Windows service: " + ex.Message);
                }
            }
        }

        return results;
    }

    public static string ServiceKeyPath(string serviceName)
    {
        return @"HKLM\SYSTEM\CurrentControlSet\Services\" + serviceName;
    }

    public static string ReadStatus(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            return controller.Status.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadStatus(ServiceController service, ICollection<string> notes)
    {
        try
        {
            return service.Status.ToString();
        }
        catch (Exception ex)
        {
            notes.Add($"Could not read status for {service.ServiceName}: {ex.Message}");
            return "Unknown";
        }
    }
}
