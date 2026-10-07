using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace OrbSpoofer.Services;

/// <summary>User-initiated wipe of everything the app accumulated on disk:
/// game database copy, quest copies + history, Steam lookups. Settings, token,
/// theme and completed quests are never touched.</summary>
public static class AppCacheJanitor
{
    /// <summary>Cache files/dirs relative to the AppData root.</summary>
    public static IReadOnlyList<string> CacheEntries(string? appDataPath = null)
    {
        var root = appDataPath ?? Config.AppDataPath;
        return
        [
            Path.Combine(root, Config.DbCacheFile),
            Path.Combine(root, Config.SteamSearchCacheDir),
            Path.Combine(root, Config.SteamIdCacheFile),
            Path.Combine(root, "cache", "image_urls.json"),
            Path.Combine(root, "quests_cache.json"),
            Path.Combine(root, "github_stats.json"),
            Path.Combine(root, "created-manifests.json"),
        ];
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.#", CultureInfo.InvariantCulture)} MB",
    };

    public static (long Bytes, int Files) Measure(string? appDataPath = null)
    {
        long bytes = 0;
        int files = 0;
        foreach (var entry in CacheEntries(appDataPath))
        {
            try
            {
                if (File.Exists(entry)) { bytes += new FileInfo(entry).Length; files++; }
                else if (Directory.Exists(entry))
                    foreach (var f in Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories))
                    {
                        try { bytes += new FileInfo(f).Length; files++; } catch { }
                    }
            }
            catch (Exception ex) { Debug.WriteLine($"Measure cache entry failed: {ex.Message}"); }
        }
        return (bytes, files);
    }

    /// <summary>Deletes tracked Steam manifests first (so no orphans stay in
    /// steamapps), then every cache entry. Returns (deleted, failed).</summary>
    public static (int Deleted, int Failed) Clear(string? appDataPath = null)
    {
        if (appDataPath == null)
        {
            try { SteamService.DeleteTrackedManifests(); }
            catch (Exception ex) { Debug.WriteLine($"DeleteTrackedManifests failed: {ex.Message}"); }
        }
        int deleted = 0, failed = 0;
        foreach (var entry in CacheEntries(appDataPath))
        {
            try
            {
                if (File.Exists(entry)) { File.Delete(entry); deleted++; }
                else if (Directory.Exists(entry))
                {
                    foreach (var f in Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); deleted++; } catch { failed++; }
                    }
                    try { if (!Directory.EnumerateFileSystemEntries(entry).Any()) Directory.Delete(entry); } catch { }
                }
            }
            catch { failed++; }
        }
        return (deleted, failed);
    }
}
