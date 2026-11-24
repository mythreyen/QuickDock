using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using QuickDock.Models;

namespace QuickDock.Services;

public static class QuickAccessService
{
    private const string QuickAccessClsid = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}";
    private static List<QuickAccessItem> _cachedItems = new();
    private static readonly object _syncLock = new();

    /// <summary>
    /// Fetches the pinned Quick Access folders from Windows File Explorer asynchronously.
    /// Executes on an STA thread to comply with COM Shell.Application requirements.
    /// </summary>
    public static Task<List<QuickAccessItem>> GetQuickAccessFoldersAsync()
    {
        var tcs = new TaskCompletionSource<List<QuickAccessItem>>();

        var thread = new Thread(() =>
        {
            try
            {
                var result = QueryQuickAccessFoldersInternal();
                lock (_syncLock)
                {
                    if (result.Count > 0)
                    {
                        _cachedItems = result;
                    }
                    else if (_cachedItems.Count > 0)
                    {
                        result = new List<QuickAccessItem>(_cachedItems);
                    }
                    else
                    {
                        result = GetFallbackFolders();
                        _cachedItems = result;
                    }
                }
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                DwmHelper.Log($"Error querying Quick Access: {ex.Message}");
                lock (_syncLock)
                {
                    tcs.SetResult(_cachedItems.Count > 0 ? _cachedItems : GetFallbackFolders());
                }
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        return tcs.Task;
    }

    private static List<QuickAccessItem> QueryQuickAccessFoldersInternal()
    {
        lock (_syncLock)
        {
            var items = new List<QuickAccessItem>();
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return items;

            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell == null) return items;

            try
            {
                dynamic? folder = shell.NameSpace(QuickAccessClsid);
                if (folder != null)
                {
                    var folderItems = folder.Items();
                    if (folderItems != null)
                    {
                        foreach (dynamic item in folderItems)
                        {
                            try
                            {
                                bool isFolder = item.IsFolder;
                                string path = (string)item.Path;
                                string name = (string)item.Name;

                                if (isFolder && !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                                {
                                    var icon = DwmHelper.ExtractFolderIcon(path);
                                    items.Add(new QuickAccessItem
                                    {
                                        Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(path) : name,
                                        Path = path,
                                        Icon = icon
                                    });
                                }
                            }
                            catch
                            {
                                // Skip any inaccessible or individual failing item
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DwmHelper.Log($"QueryQuickAccessFoldersInternal exception: {ex.Message}");
            }

            return items;
        }
    }

    private static List<QuickAccessItem> GetFallbackFolders()
    {
        var list = new List<QuickAccessItem>();
        string[] candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Code")
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                list.Add(new QuickAccessItem
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Icon = DwmHelper.ExtractFolderIcon(path)
                });
            }
        }
        return list;
    }
}
