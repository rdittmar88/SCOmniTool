using System;
using System.Collections.Generic;

namespace SCOmniTool.Security;

internal sealed class SecurityProductSignature
{
    public SecurityProductSignature(string name, string category, string[] nameTokens, string[] serviceNames)
    {
        Name = name;
        Category = category;
        NameTokens = nameTokens;
        ServiceNames = serviceNames;
    }

    public string Name { get; }
    public string Category { get; }
    public string[] NameTokens { get; }
    public string[] ServiceNames { get; }
}

internal static class SecurityProductCatalog
{
    // Names and service names drawn from current EDR/XDR comparisons
    // (CrowdStrike Falcon, SentinelOne, Microsoft Defender for Endpoint,
    // Cortex XDR, Sophos, Cybereason, Carbon Black, Trend Micro, Bitdefender,
    // Trellix, and others) and from public Windows service lists for those agents.
    private static readonly SecurityProductSignature[] Products =
    {
        new("CrowdStrike Falcon", "EDR",
            new[] { "CrowdStrike", "Falcon Sensor" },
            new[] { "CSFalconService", "CSAgent" }),
        new("SentinelOne", "EDR",
            new[] { "SentinelOne", "Sentinel Agent", "Singularity XDR" },
            new[] { "SentinelAgent", "SentinelStaticEngine", "SentinelHelperService" }),
        new("Microsoft Defender for Endpoint", "EDR",
            new[] { "Defender for Endpoint", "Advanced Threat Protection" },
            new[] { "Sense" }),
        new("Palo Alto Cortex XDR", "XDR",
            new[] { "Cortex XDR", "Palo Alto Networks Traps", "Cyvera" },
            new[] { "cyserver", "CyveraService", "tlaservice" }),
        new("Carbon Black", "EDR",
            new[] { "Carbon Black", "Cb Defense", "VMware Carbon Black" },
            new[] { "CbDefense", "CarbonBlack", "RepMgr" }),
        new("Carbon Black App Control", "Application control",
            new[] { "Carbon Black App Control", "Bit9" },
            new[] { "Parity" }),
        new("Cybereason", "EDR",
            new[] { "Cybereason" },
            new[] { "CybereasonActiveProbe", "CybereasonCRS", "CybereasonBlocki" }),
        new("Sophos", "EDR",
            new[] { "Sophos", "Intercept X" },
            new[] { "SAVService", "hmpalert", "swi_service", "Sophos Endpoint Defense Service" }),
        new("Trend Micro", "EDR",
            new[] { "Trend Micro", "Apex One", "Vision One", "OfficeScan", "Deep Security", "Worry-Free" },
            new[] { "tmlisten", "ntrtscan", "ds_agent", "ds_notifier" }),
        new("Bitdefender", "EDR",
            new[] { "Bitdefender", "GravityZone" },
            new[] { "VSSERV", "bdredline", "bdredline_agent", "EPRedline", "BDAuxSrv" }),
        new("Trellix", "EDR",
            new[] { "Trellix", "McAfee", "FireEye" },
            new[] { "masvc", "McAfeeFramework", "mfefire", "McShield", "xagt" }),
        new("Symantec Endpoint Protection", "Antivirus",
            new[] { "Symantec Endpoint", "Norton 360", "Norton AntiVirus", "Norton Internet Security", "Norton Security" },
            new[] { "SepMasterService", "SepScanService", "ccSvcHst", "NortonSecurity" }),
        new("Kaspersky", "Antivirus",
            new[] { "Kaspersky" },
            new[] { "AVP", "klnagent" }),
        new("ESET", "Antivirus",
            new[] { "ESET", "NOD32" },
            new[] { "ekrn", "ekm", "epfw", "EraAgentSvc" }),
        new("Malwarebytes", "Antivirus",
            new[] { "Malwarebytes" },
            new[] { "MBAMService", "MBEndpointAgent" }),
        new("Webroot", "Antivirus",
            new[] { "Webroot" },
            new[] { "WRSVC", "WRCoreService" }),
        new("Avast", "Antivirus",
            new[] { "Avast" },
            new[] { "AvastSvc", "aswBcc" }),
        new("AVG", "Antivirus",
            new[] { "AVG Antivirus", "AVG Internet Security", "AVG AntiVirus" },
            new[] { "AVGSvc", "avgwd" }),
        new("Avira", "Antivirus",
            new[] { "Avira" },
            new[] { "Avira.ServiceHost", "AntiVirService" }),
        new("F-Secure", "Antivirus",
            new[] { "F-Secure", "WithSecure" },
            new[] { "fshoster", "fsaua", "fsgk32" }),
        new("G DATA", "Antivirus",
            new[] { "G DATA", "G Data" },
            Array.Empty<string>()),
        new("VIPRE", "Antivirus",
            new[] { "VIPRE" },
            new[] { "SBAMSvc" }),
        new("Emsisoft", "Antivirus",
            new[] { "Emsisoft" },
            new[] { "a2service" }),
        new("Panda", "Antivirus",
            new[] { "Panda Adaptive Defense", "Panda Endpoint", "WatchGuard EPDR" },
            new[] { "PSANHost" }),
        new("Comodo", "Antivirus",
            new[] { "Comodo", "Xcitium" },
            new[] { "cmdAgent" }),
        new("BlackBerry Protect", "EDR",
            new[] { "Cylance", "BlackBerry Protect", "BlackBerry Optics" },
            new[] { "CylanceSvc" }),
        new("Cisco Secure Endpoint", "EDR",
            new[] { "Cisco Secure Endpoint", "Cisco AMP", "Immunet" },
            new[] { "CiscoAMP" }),
        new("Check Point Harmony", "EDR",
            new[] { "Check Point", "ZoneAlarm", "Harmony Endpoint" },
            new[] { "vsmon" }),
        new("FortiClient", "EDR",
            new[] { "FortiEDR", "FortiClient", "Fortinet" },
            new[] { "FortiEDR", "FortiClient" }),
        new("Elastic Security", "EDR",
            new[] { "Elastic Agent", "Elastic Endpoint", "Elastic Defend", "Endgame" },
            new[] { "Elastic Agent", "elastic-agent", "elastic-endpoint" }),
        new("Wazuh", "EDR",
            new[] { "Wazuh" },
            new[] { "WazuhSvc", "Wazuh" }),
        new("Huntress", "EDR",
            new[] { "Huntress" },
            new[] { "HuntressAgent", "HuntressUpdater", "HuntressRio" }),
        new("Deep Instinct", "EDR",
            new[] { "Deep Instinct" },
            new[] { "DeepInstinct" }),
        new("Morphisec", "EDR",
            new[] { "Morphisec" },
            Array.Empty<string>()),
        new("Secureworks Taegis", "XDR",
            new[] { "Secureworks", "Taegis" },
            Array.Empty<string>()),
        new("SonicWall Capture Client", "EDR",
            new[] { "Capture Client" },
            Array.Empty<string>()),
        new("Acronis Cyber Protect", "EDR",
            new[] { "Acronis Cyber" },
            Array.Empty<string>()),
        new("ThreatLocker", "Application control",
            new[] { "ThreatLocker" },
            new[] { "ThreatLockerService" }),
        new("Airlock Digital", "Application control",
            new[] { "Airlock Digital" },
            Array.Empty<string>()),
        new("CyberArk EPM", "Application control",
            new[] { "CyberArk", "Endpoint Privilege Manager" },
            Array.Empty<string>()),
        new("BeyondTrust Privilege Management", "Application control",
            new[] { "Privilege Management", "PowerBroker" },
            Array.Empty<string>()),
        new("Symantec DLP", "DLP",
            new[] { "Symantec DLP", "Data Loss Prevention" },
            Array.Empty<string>()),
        new("Forcepoint", "DLP",
            new[] { "Forcepoint" },
            Array.Empty<string>()),
        new("Digital Guardian", "DLP",
            new[] { "Digital Guardian" },
            Array.Empty<string>()),
        new("Code42", "DLP",
            new[] { "Code42" },
            Array.Empty<string>()),
        new("Cisco Umbrella", "Web security",
            new[] { "Umbrella", "OpenDNS" },
            new[] { "Umbrella_RC" }),
        new("Zscaler", "Web security",
            new[] { "Zscaler" },
            new[] { "ZSAService", "ZSATunnel", "ZSAUpm" }),
        new("Netskope", "Web security",
            new[] { "Netskope" },
            new[] { "stAgentSvc" }),
        new("Cloudflare WARP", "Web security",
            new[] { "Cloudflare WARP" },
            new[] { "CloudflareWARP" }),
        new("Menlo Security", "Web security",
            new[] { "Menlo Security" },
            Array.Empty<string>()),
        new("iboss", "Web security",
            new[] { "iboss" },
            Array.Empty<string>()),
        new("GlassWire", "Firewall",
            new[] { "GlassWire" },
            Array.Empty<string>()),
        new("Dell Data Protection", "Disk encryption",
            new[] { "Dell Encryption", "Dell Data Protection" },
            Array.Empty<string>()),
        new("WinMagic", "Disk encryption",
            new[] { "WinMagic", "SecureDoc" },
            Array.Empty<string>()),
        new("Qualys", "Security agent",
            new[] { "Qualys" },
            new[] { "QualysAgent" }),
        new("Tenable Nessus", "Security agent",
            new[] { "Nessus Agent", "Tenable Nessus" },
            new[] { "Tenable Nessus Agent", "NessusAgent" }),
        new("Rapid7", "Security agent",
            new[] { "Insight Agent", "Rapid7" },
            new[] { "ir_agent" }),
        new("Tanium", "Security agent",
            new[] { "Tanium" },
            new[] { "Tanium Client" }),
        new("Absolute", "Security agent",
            new[] { "Absolute Software", "Computrace", "Absolute Persistence" },
            Array.Empty<string>()),
        new("Microsoft Intune", "Device management",
            new[] { "Intune Management Extension" },
            new[] { "IntuneManagementExtension" }),
        new("Omnissa Workspace ONE", "Device management",
            new[] { "Workspace ONE", "AirWatch" },
            Array.Empty<string>()),
        new("Heimdal", "Web security",
            new[] { "Heimdal" },
            Array.Empty<string>()),
        new("Dr.Web", "Antivirus",
            new[] { "Dr.Web" },
            Array.Empty<string>()),
        new("AhnLab", "Antivirus",
            new[] { "AhnLab", "V3 Lite", "V3 Internet Security" },
            Array.Empty<string>()),
        new("360 Total Security", "Antivirus",
            new[] { "360 Total Security", "Qihoo 360" },
            Array.Empty<string>()),
        new("TotalAV", "Antivirus",
            new[] { "TotalAV" },
            Array.Empty<string>()),
        new("SUPERAntiSpyware", "Antivirus",
            new[] { "SUPERAntiSpyware" },
            Array.Empty<string>()),
        new("Spybot", "Antivirus",
            new[] { "Spybot" },
            Array.Empty<string>()),
        new("ClamWin", "Antivirus",
            new[] { "ClamWin" },
            Array.Empty<string>()),
        new("Microsoft Security Essentials", "Antivirus",
            new[] { "Security Essentials" },
            Array.Empty<string>()),
        new("LimaCharlie", "EDR",
            new[] { "LimaCharlie" },
            Array.Empty<string>()),
        new("Red Canary", "EDR",
            new[] { "Red Canary" },
            Array.Empty<string>()),
        new("Proofpoint", "Email security",
            new[] { "Proofpoint" },
            Array.Empty<string>()),
        new("Mimecast", "Email security",
            new[] { "Mimecast" },
            Array.Empty<string>())
    };

    private static readonly Dictionary<string, SecurityProductSignature> ServiceIndex = BuildServiceIndex();

    public static IReadOnlyList<SecurityProductSignature> All => Products;

    public static bool TryGetService(string serviceName, out SecurityProductSignature signature)
    {
        return ServiceIndex.TryGetValue(serviceName, out signature!);
    }

    public static SecurityProductSignature? BestNameMatch(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        SecurityProductSignature? best = null;
        var bestLength = 0;
        foreach (var product in Products)
        {
            foreach (var token in product.NameTokens)
            {
                if (token.Length <= bestLength)
                {
                    continue;
                }

                if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    best = product;
                    bestLength = token.Length;
                }
            }
        }

        return best;
    }

    private static Dictionary<string, SecurityProductSignature> BuildServiceIndex()
    {
        var index = new Dictionary<string, SecurityProductSignature>(StringComparer.OrdinalIgnoreCase);
        foreach (var product in Products)
        {
            foreach (var serviceName in product.ServiceNames)
            {
                if (!index.ContainsKey(serviceName))
                {
                    index.Add(serviceName, product);
                }
            }
        }

        return index;
    }
}
