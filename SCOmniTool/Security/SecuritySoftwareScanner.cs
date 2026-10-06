using System;
using System.Collections.Generic;
using System.ServiceProcess;
using Microsoft.Win32;
using SCOmniTool.Models;

namespace SCOmniTool.Security;

internal static class SecuritySoftwareScanner
{
    public static List<SecuritySoftwareFinding> FindAll(
        IReadOnlyList<AntivirusProduct> securityCenterProducts,
        out string note)
    {
        var errors = new List<string>();
        var programs = new List<InstalledSecurityProgram>();
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, programs, errors);
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, programs, errors);
        ReadUninstall(RegistryHive.CurrentUser, RegistryView.Default, programs, errors);

        var findings = new Dictionary<string, SecuritySoftwareFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var program in programs)
        {
            var signature = SecurityProductCatalog.BestNameMatch(program.DisplayName);
            if (signature == null || AlreadyReported(signature, securityCenterProducts))
            {
                continue;
            }

            var finding = GetOrAdd(findings, signature);
            if (!ContainsProgram(finding, program))
            {
                finding.Programs.Add(program);
            }
        }

        ReadServices(findings, securityCenterProducts, errors);
        note = errors.Count == 0 ? string.Empty : string.Join(" ", errors);

        var results = new List<SecuritySoftwareFinding>(findings.Values);
        results.Sort(CompareFindings);
        return results;
    }

    private static void ReadUninstall(
        RegistryHive hive,
        RegistryView view,
        List<InstalledSecurityProgram> programs,
        List<string> errors)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (key == null)
            {
                return;
            }

            foreach (var subName in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(subName);
                    var displayName = (sub?.GetValue("DisplayName") as string ?? string.Empty).Trim();
                    if (displayName.Length == 0)
                    {
                        continue;
                    }

                    programs.Add(new InstalledSecurityProgram
                    {
                        DisplayName = displayName,
                        Version = ReadValue(sub, "DisplayVersion"),
                        Publisher = ReadValue(sub, "Publisher"),
                        InstallLocation = ReadValue(sub, "InstallLocation")
                    });
                }
                catch (Exception ex)
                {
                    if (errors.Count < 5)
                    {
                        errors.Add("Could not read an uninstall key: " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add("Could not read installed programs (" + hive + " " + view + "): " + ex.Message);
        }
    }

    private static void ReadServices(
        Dictionary<string, SecuritySoftwareFinding> findings,
        IReadOnlyList<AntivirusProduct> securityCenterProducts,
        List<string> errors)
    {
        ServiceController[] services;
        try
        {
            services = ServiceController.GetServices();
        }
        catch (Exception ex)
        {
            errors.Add("Could not list services for security software: " + ex.Message);
            return;
        }

        foreach (var service in services)
        {
            using (service)
            {
                try
                {
                    var serviceName = service.ServiceName ?? string.Empty;
                    var displayName = service.DisplayName ?? string.Empty;
                    var signature = MatchService(serviceName, displayName);
                    if (signature == null || AlreadyReported(signature, securityCenterProducts))
                    {
                        continue;
                    }

                    var finding = GetOrAdd(findings, signature);
                    if (ContainsService(finding, serviceName))
                    {
                        continue;
                    }

                    finding.Services.Add(new SecurityServiceHit
                    {
                        ServiceName = serviceName,
                        DisplayName = displayName,
                        Status = ReadStatus(service)
                    });
                }
                catch (Exception ex)
                {
                    errors.Add("Could not read a service while looking for security software: " + ex.Message);
                }
            }
        }
    }

    private static SecurityProductSignature? MatchService(string serviceName, string displayName)
    {
        if (SecurityProductCatalog.TryGetService(serviceName, out var byService))
        {
            return byService;
        }

        return SecurityProductCatalog.BestNameMatch(displayName);
    }

    private static bool AlreadyReported(
        SecurityProductSignature signature,
        IReadOnlyList<AntivirusProduct> securityCenterProducts)
    {
        foreach (var product in securityCenterProducts)
        {
            var name = product.Name ?? string.Empty;
            if (name.Length == 0 || name.Equals("not available", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (name.IndexOf(signature.Name, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            foreach (var token in signature.NameTokens)
            {
                if (token.Length >= 5 && name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static SecuritySoftwareFinding GetOrAdd(
        Dictionary<string, SecuritySoftwareFinding> findings,
        SecurityProductSignature signature)
    {
        if (findings.TryGetValue(signature.Name, out var existing))
        {
            return existing;
        }

        var finding = new SecuritySoftwareFinding
        {
            Name = signature.Name,
            Category = signature.Category
        };
        findings.Add(signature.Name, finding);
        return finding;
    }

    private static bool ContainsProgram(SecuritySoftwareFinding finding, InstalledSecurityProgram program)
    {
        foreach (var existing in finding.Programs)
        {
            if (string.Equals(existing.DisplayName, program.DisplayName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(existing.Version, program.Version, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsService(SecuritySoftwareFinding finding, string serviceName)
    {
        foreach (var existing in finding.Services)
        {
            if (string.Equals(existing.ServiceName, serviceName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadStatus(ServiceController service)
    {
        try
        {
            return service.Status.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string ReadValue(RegistryKey? key, string name)
    {
        var value = (key?.GetValue(name) as string ?? string.Empty).Trim();
        return value.Length == 0 ? string.Empty : value;
    }

    private static int CompareFindings(SecuritySoftwareFinding left, SecuritySoftwareFinding right)
    {
        var category = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);
        if (category != 0)
        {
            return category;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }
}
