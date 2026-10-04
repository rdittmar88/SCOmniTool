using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SCOmniTool.Models;

namespace SCOmniTool.Discovery;

internal static class FileDiscovery
{
    private static readonly string ProgramFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    private static readonly string UsersRoot = @"C:\Users";
    private static readonly string SystemConfigRoot = @"C:\Windows\SysWOW64\config\systemprofile\AppData\Local";
    private static readonly string SystemTempRoot = @"C:\Windows\SystemTemp\ScreenConnect";

    public static List<FileItemInfo> FindAll(ICollection<string> notes)
    {
        var userDirectories = GetUserDirectories(notes);
        var results = new List<FileItemInfo>();
        results.AddRange(FindProgramFolders(notes));
        results.AddRange(FindClickOnceFolders(userDirectories));
        results.AddRange(FindUserConfigFolders(userDirectories, notes));
        results.AddRange(FindSystemConfigFolders(notes));
        results.AddRange(FindSystemTempFolders());
        results.AddRange(FindDownloadFiles(userDirectories, notes));
        return results;
    }

    public static List<FileItemInfo> FindProgramFolders(ICollection<string> notes)
    {
        return FindClientPrefixDirectories(ProgramFilesX86, FileItemType.ProgramFolder, notes);
    }

    public static List<FileItemInfo> FindClickOnceFolders(IReadOnlyList<string> userDirectories)
    {
        var results = new List<FileItemInfo>();

        foreach (var userDirectory in userDirectories)
        {
            var clickOncePath = Path.Combine(userDirectory, "AppData", "Local", "Apps", "2.0");
            if (!Directory.Exists(clickOncePath))
            {
                continue;
            }

            results.Add(new FileItemInfo
            {
                Type = FileItemType.ClickOnceFolder,
                Path = clickOncePath,
                IsDirectory = true
            });
        }

        return results;
    }

    public static List<FileItemInfo> FindUserConfigFolders(IReadOnlyList<string> userDirectories, ICollection<string> notes)
    {
        var results = new List<FileItemInfo>();

        foreach (var userDirectory in userDirectories)
        {
            var localAppData = Path.Combine(userDirectory, "AppData", "Local");
            results.AddRange(FindClientPrefixDirectories(localAppData, FileItemType.UserConfigFolder, notes));
        }

        return results;
    }

    public static List<FileItemInfo> FindSystemConfigFolders(ICollection<string> notes)
    {
        return FindClientPrefixDirectories(SystemConfigRoot, FileItemType.SystemConfigFolder, notes);
    }

    public static List<FileItemInfo> FindSystemTempFolders()
    {
        var results = new List<FileItemInfo>();

        if (!Directory.Exists(SystemTempRoot))
        {
            return results;
        }

        results.Add(new FileItemInfo
        {
            Type = FileItemType.SystemTempFolder,
            Path = SystemTempRoot,
            IsDirectory = true
        });

        return results;
    }

    public static List<FileItemInfo> FindDownloadFiles(IReadOnlyList<string> userDirectories, ICollection<string> notes)
    {
        var results = new List<FileItemInfo>();

        foreach (var userDirectory in userDirectories)
        {
            var downloadsPath = Path.Combine(userDirectory, "Downloads");
            if (!Directory.Exists(downloadsPath))
            {
                continue;
            }

            try
            {
                foreach (var file in Directory.GetFiles(downloadsPath))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.IndexOf(Constants.ScreenConnectToken, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        results.Add(new FileItemInfo
                        {
                            Type = FileItemType.DownloadFile,
                            Path = file,
                            IsDirectory = false
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                notes.Add($"Skipped {downloadsPath}: {ex.Message}");
            }
        }

        return results;
    }

    private static List<FileItemInfo> FindClientPrefixDirectories(
        string rootPath,
        FileItemType itemType,
        ICollection<string> notes)
    {
        var results = new List<FileItemInfo>();

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
        {
            return results;
        }

        try
        {
            foreach (var directory in Directory.GetDirectories(rootPath))
            {
                var directoryName = Path.GetFileName(directory);
                if (directoryName.StartsWith(Constants.ClientPrefix, StringComparison.Ordinal))
                {
                    results.Add(new FileItemInfo
                    {
                        Type = itemType,
                        Path = directory,
                        IsDirectory = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            notes.Add($"Skipped {rootPath}: {ex.Message}");
        }

        return results;
    }

    public static IReadOnlyList<string> ListUserDirectories(ICollection<string> notes)
    {
        return GetUserDirectories(notes);
    }

    private static IReadOnlyList<string> GetUserDirectories(ICollection<string> notes)
    {
        if (!Directory.Exists(UsersRoot))
        {
            return new List<string>();
        }

        try
        {
            return Directory.GetDirectories(UsersRoot)
                .Where(path =>
                {
                    var profileName = Path.GetFileName(path);
                    return !Constants.SkippedUserProfiles.Contains(profileName, StringComparer.OrdinalIgnoreCase);
                })
                .ToList();
        }
        catch (Exception ex)
        {
            notes.Add($"Could not list {UsersRoot}: {ex.Message}");
            return new List<string>();
        }
    }
}
