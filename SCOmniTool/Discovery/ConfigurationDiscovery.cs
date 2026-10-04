using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class ConfigurationDiscovery
{
    private static readonly string[] InstallFileNames = { "app.config", "system.config" };
    private static readonly string[] UserFileNames = { "user.config" };
    private static readonly string ProgramDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static List<ConfigFileRecord> FindAll(ICollection<string> notes)
    {
        var results = new List<ConfigFileRecord>();
        var users = FileDiscovery.ListUserDirectories(notes);

        AddDirectories(results, FileDiscovery.FindProgramFolders(notes), "Client install", InstallFileNames);
        AddDirectories(results, FileDiscovery.FindUserConfigFolders(users, notes), "User configuration", UserFileNames);
        AddDirectories(results, FileDiscovery.FindSystemConfigFolders(notes), "System configuration", UserFileNames);
        AddDirectories(results, FindProgramDataFolders(notes), "Machine configuration", UserFileNames);
        return results;
    }

    private static void AddDirectories(
        List<ConfigFileRecord> results,
        IEnumerable<FileItemInfo> directories,
        string locationLabel,
        string[] fileNames)
    {
        foreach (var directory in directories)
        {
            if (!directory.IsDirectory || string.IsNullOrWhiteSpace(directory.Path))
            {
                continue;
            }

            foreach (var fileName in fileNames)
            {
                results.Add(ReadFile(locationLabel, directory.Path, fileName));
            }
        }
    }

    private static List<FileItemInfo> FindProgramDataFolders(ICollection<string> notes)
    {
        var results = new List<FileItemInfo>();
        if (string.IsNullOrEmpty(ProgramDataRoot) || !Directory.Exists(ProgramDataRoot))
        {
            return results;
        }

        try
        {
            foreach (var directory in Directory.GetDirectories(ProgramDataRoot))
            {
                var directoryName = Path.GetFileName(directory);
                if (directoryName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal))
                {
                    results.Add(new FileItemInfo
                    {
                        Type = FileItemType.UserConfigFolder,
                        Path = directory,
                        IsDirectory = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            notes.Add($"Skipped {ProgramDataRoot}: {ex.Message}");
        }

        return results;
    }

    private static ConfigFileRecord ReadFile(string locationLabel, string directoryPath, string fileName)
    {
        var fullPath = Path.Combine(directoryPath, fileName);
        var record = new ConfigFileRecord
        {
            LocationLabel = locationLabel,
            DirectoryPath = directoryPath,
            FileName = fileName,
            FullPath = fullPath
        };

        if (!File.Exists(fullPath))
        {
            return record;
        }

        record.Found = true;
        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (Exception ex)
        {
            record.Error = ex.Message;
            return record;
        }

        if (TryReadXml(text, record.Settings))
        {
            record.ParsedAsXml = true;
        }
        else
        {
            record.RawText = text ?? string.Empty;
        }

        return record;
    }

    private static bool TryReadXml(string text, List<ConfigSetting> settings)
    {
        if (string.IsNullOrWhiteSpace(text) || text.TrimStart().Length == 0 || text.TrimStart()[0] != '<')
        {
            return false;
        }

        try
        {
            var document = new XmlDocument();
            document.LoadXml(text);
            if (document.DocumentElement != null)
            {
                ReadXml(document.DocumentElement, settings);
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static void ReadXml(XmlElement element, List<ConfigSetting> settings)
    {
        if (string.Equals(element.LocalName, "setting", StringComparison.OrdinalIgnoreCase))
        {
            var name = element.GetAttribute("name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                var valueNode = FindChild(element, "value");
                var text = valueNode != null ? valueNode.InnerText : element.InnerText;
                settings.Add(new ConfigSetting
                {
                    Name = name.Trim(),
                    Value = (text ?? string.Empty).Trim()
                });
                return;
            }
        }

        var children = new List<XmlElement>();
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childElement)
            {
                children.Add(childElement);
            }
        }

        if (children.Count == 0)
        {
            var text = (element.InnerText ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                settings.Add(new ConfigSetting
                {
                    Name = element.LocalName,
                    Value = text
                });
            }

            return;
        }

        foreach (var child in children)
        {
            ReadXml(child, settings);
        }
    }

    private static XmlElement? FindChild(XmlElement element, string localName)
    {
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childElement &&
                string.Equals(childElement.LocalName, localName, StringComparison.OrdinalIgnoreCase))
            {
                return childElement;
            }
        }

        return null;
    }
}
