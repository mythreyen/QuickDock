using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuickDock.Models;

namespace QuickDock.Services;

public static class FolderConfigService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "QuickDock");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "folders.json");

    private static readonly object _lock = new();
    private static List<QuickAccessItem> _cachedFolders = new();
    private static bool _hasLoadedFromDisk = false;

    public sealed class FolderEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }

    public sealed class ConfigData
    {
        public int Version { get; set; } = 1;
        public List<FolderEntry> Folders { get; set; } = new();
    }

    /// <summary>
    /// Loads the configured folders.
    /// If no configuration exists yet, initializes it from Windows Quick Access.
    /// </summary>
    public static async Task<List<QuickAccessItem>> GetConfiguredFoldersAsync(bool forceReload = false)
    {
        lock (_lock)
        {
            if (!forceReload && _hasLoadedFromDisk && _cachedFolders.Count > 0)
            {
                return new List<QuickAccessItem>(_cachedFolders);
            }
        }

        if (File.Exists(ConfigFilePath))
        {
            string? json = null;
            // Retry reading in case file was being written or scanned by another process
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    json = await File.ReadAllTextAsync(ConfigFilePath);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    DwmHelper.Log($"Error reading {ConfigFilePath}: {ex.Message}");
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(json))
            {
                try
                {
                    var config = JsonSerializer.Deserialize<ConfigData>(json);
                    if (config?.Folders != null)
                    {
                        var items = new List<QuickAccessItem>();
                        foreach (var entry in config.Folders)
                        {
                            if (string.IsNullOrWhiteSpace(entry.Path)) continue;

                            string trimmedPath = entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            string resolvedName = !string.IsNullOrWhiteSpace(entry.Name)
                                ? entry.Name
                                : (Path.GetFileName(trimmedPath) is { Length: > 0 } fn ? fn : entry.Path);

                            var item = new QuickAccessItem
                            {
                                Name = resolvedName,
                                Path = entry.Path,
                                Icon = DwmHelper.ExtractFolderIcon(entry.Path)
                            };
                            items.Add(item);
                        }

                        lock (_lock)
                        {
                            _cachedFolders = items;
                            _hasLoadedFromDisk = true;
                        }
                        return items;
                    }
                }
                catch (Exception ex)
                {
                    DwmHelper.Log($"Error deserializing {ConfigFilePath}: {ex.Message}");
                }
            }

            // If file exists but read or parse failed, return cached items if available without overwriting config
            lock (_lock)
            {
                if (_cachedFolders.Count > 0)
                {
                    return new List<QuickAccessItem>(_cachedFolders);
                }
            }
        }

        // Initialize from File Explorer Quick Access only when configuration file does not exist yet
        var defaultItems = await QuickAccessService.GetQuickAccessFoldersAsync();
        lock (_lock)
        {
            _cachedFolders = defaultItems;
            _hasLoadedFromDisk = true;
            SaveFoldersInternal(_cachedFolders);
        }
        return defaultItems;
    }

    /// <summary>
    /// Ensures cached folders are loaded from disk if not yet loaded.
    /// Caller MUST hold _lock.
    /// </summary>
    private static void EnsureLoadedLocked()
    {
        if (_hasLoadedFromDisk) return;

        if (File.Exists(ConfigFilePath))
        {
            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<ConfigData>(json);
                if (config?.Folders != null)
                {
                    var items = new List<QuickAccessItem>();
                    foreach (var entry in config.Folders)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Path)) continue;

                        string trimmedPath = entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string resolvedName = !string.IsNullOrWhiteSpace(entry.Name)
                            ? entry.Name
                            : (Path.GetFileName(trimmedPath) is { Length: > 0 } fn ? fn : entry.Path);

                        items.Add(new QuickAccessItem
                        {
                            Name = resolvedName,
                            Path = entry.Path,
                            Icon = DwmHelper.ExtractFolderIcon(entry.Path)
                        });
                    }
                    _cachedFolders = items;
                    _hasLoadedFromDisk = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                DwmHelper.Log($"EnsureLoadedLocked error: {ex.Message}");
            }
        }
        _hasLoadedFromDisk = true;
    }

    /// <summary>
    /// Adds a folder by path if not already present.
    /// </summary>
    public static bool AddFolder(string folderPath, string? customName = null)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;

        folderPath = Path.GetFullPath(folderPath);

        lock (_lock)
        {
            EnsureLoadedLocked();

            if (_cachedFolders.Any(f => string.Equals(f.Path, folderPath, StringComparison.OrdinalIgnoreCase)))
            {
                return false; // Already in dock
            }

            string trimmedPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = customName ?? (Path.GetFileName(trimmedPath) is { Length: > 0 } fn ? fn : folderPath);
            var newItem = new QuickAccessItem
            {
                Name = name,
                Path = folderPath,
                Icon = DwmHelper.ExtractFolderIcon(folderPath)
            };

            _cachedFolders.Add(newItem);
            SaveFoldersInternal(_cachedFolders);
            return true;
        }
    }

    /// <summary>
    /// Removes a folder from the dock by path.
    /// </summary>
    public static void RemoveFolder(string folderPath)
    {
        lock (_lock)
        {
            EnsureLoadedLocked();
            _cachedFolders.RemoveAll(f => string.Equals(f.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            SaveFoldersInternal(_cachedFolders);
        }
    }

    /// <summary>
    /// Moves a folder up in the list.
    /// </summary>
    public static void MoveUp(string folderPath)
    {
        lock (_lock)
        {
            EnsureLoadedLocked();
            int idx = _cachedFolders.FindIndex(f => string.Equals(f.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            if (idx > 0)
            {
                var item = _cachedFolders[idx];
                _cachedFolders.RemoveAt(idx);
                _cachedFolders.Insert(idx - 1, item);
                SaveFoldersInternal(_cachedFolders);
            }
        }
    }

    /// <summary>
    /// Moves a folder down in the list.
    /// </summary>
    public static void MoveDown(string folderPath)
    {
        lock (_lock)
        {
            EnsureLoadedLocked();
            int idx = _cachedFolders.FindIndex(f => string.Equals(f.Path, folderPath, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx < _cachedFolders.Count - 1)
            {
                var item = _cachedFolders[idx];
                _cachedFolders.RemoveAt(idx);
                _cachedFolders.Insert(idx + 1, item);
                SaveFoldersInternal(_cachedFolders);
            }
        }
    }

    /// <summary>
    /// Resets the configuration back to Windows File Explorer Quick Access defaults.
    /// </summary>
    public static async Task<List<QuickAccessItem>> ResetToQuickAccessDefaultsAsync()
    {
        var items = await QuickAccessService.GetQuickAccessFoldersAsync();
        lock (_lock)
        {
            _cachedFolders = items;
            _hasLoadedFromDisk = true;
            SaveFoldersInternal(items);
        }
        return items;
    }

    /// <summary>
    /// Gets a copy of currently cached items.
    /// </summary>
    public static List<QuickAccessItem> GetCachedItems()
    {
        lock (_lock)
        {
            EnsureLoadedLocked();
            return new List<QuickAccessItem>(_cachedFolders);
        }
    }

    private static void SaveFoldersInternal(List<QuickAccessItem> items)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var config = new ConfigData
            {
                Version = 1,
                Folders = items.Select(i => new FolderEntry
                {
                    Name = i.Name,
                    Path = i.Path
                }).ToList()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);

            // Atomic write via temp file
            string tempFile = Path.Combine(ConfigDirectory, $"folders_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, ConfigFilePath, overwrite: true);

            DwmHelper.Log($"Saved {config.Folders.Count} folders to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"Error saving {ConfigFilePath}: {ex.Message}");
        }
    }
}
