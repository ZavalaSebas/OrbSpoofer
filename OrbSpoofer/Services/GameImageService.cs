using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using OrbSpoofer.Exceptions;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public static class GameImageService
{
    private static readonly ConcurrentDictionary<string, string?> Cache = new();
    private static readonly ConcurrentDictionary<string, int?> SteamIdCache = new();
    private static readonly object _steamIdLock = new();
    private static bool _steamIdCacheLoaded;
    private static bool _steamIdCacheDirty;
    private static bool _imageCacheDirty;
    private static bool _exitHooked;
    private static readonly DispatcherTimer _steamIdSaveTimer;

    private static readonly string SteamIdCachePath =
        Path.Combine(Config.AppDataPath, Config.SteamIdCacheFile);
    private static readonly string ImageCachePath =
        Path.Combine(Config.AppDataPath, "cache", "image_urls.json");

    static GameImageService()
    {
        _steamIdSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _steamIdSaveTimer.Tick += (_, _) =>
        {
            _steamIdSaveTimer.Stop();
            SaveSteamIdCache();
            SaveImageCache();
        };
        LoadSteamIdCache();
        LoadImageCache();
        // background cleanup of expired steam_search caches
        try { Infrastructure.Cache.CacheStore.CleanupExpired(Path.Combine(Config.AppDataPath, Config.SteamSearchCacheDir), Config.MaxCacheAgeDays); } catch { }
    }

    public static async Task<string?> GetImageUrlAsync(DiscordGame game)
    {
        if (string.IsNullOrEmpty(game.Id)) return null;

        if (Cache.TryGetValue(game.Id, out var cached))
            return cached;

        var url = await ResolveImageUrlAsync(game);
        Cache[game.Id] = url;
        _imageCacheDirty = true;
        ScheduleSteamIdSave();
        return url;
    }

    public static async Task PreloadAsync(IEnumerable<DiscordGame> games)
    {
        var uncached = games.Where(g => !Cache.ContainsKey(g.Id)).ToList();
        if (uncached.Count == 0) return;

        await Parallel.ForEachAsync(uncached, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (game, _) =>
        {
            if (string.IsNullOrEmpty(game.Id)) return;
            var url = await ResolveImageUrlAsync(game);
            Cache[game.Id] = url;
        });
    }

    public static async Task<int?> GetSteamAppIdAsync(DiscordGame game)
    {
        if (SteamIdCache.TryGetValue(game.Id, out var cachedId))
            return cachedId;

        var appId = await FindSteamAppIdAsync(game);
        lock (_steamIdLock)
        {
            SteamIdCache[game.Id] = appId;
            _steamIdCacheDirty = true;
        }
        ScheduleSteamIdSave();
        return appId;
    }

    private static void ScheduleSteamIdSave()
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (!_exitHooked && System.Windows.Application.Current != null)
                {
                    _exitHooked = true;
                    System.Windows.Application.Current.Exit += (_, _) => { SaveSteamIdCache(); SaveImageCache(); };
                }
                _steamIdSaveTimer.Stop();
                _steamIdSaveTimer.Start();
            });
        }
        catch
        {
            SaveSteamIdCache();
            SaveImageCache();
        }
    }

    private static async Task<string?> ResolveImageUrlAsync(DiscordGame game)
    {
        if (!string.IsNullOrEmpty(game.IconHash))
            return $"{Config.DiscordCdnBase}app-icons/{game.Id}/{game.IconHash}.png";

        var appId = await FindSteamAppIdAsync(game);
        if (appId > 0)
            return $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg";

        return null;
    }

    private static async Task<int?> FindSteamAppIdAsync(DiscordGame game)
    {
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(
                Config.SteamStoreSearchUrl,
                queryParams: new Dictionary<string, string>
                {
                    ["term"] = game.Name,
                    ["l"] = "english",
                    ["cc"] = "US"
                });

            if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var name = item.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!NameMatches(name, game.Name, game.Aliases)) continue;

                    var appId = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
                    if (appId > 0)
                        return appId;
                }
            }
        }
        catch (NetworkError) { }

        return null;
    }

    private static bool NameMatches(string candidate, string gameName, List<string> aliases)
    {
        if (string.Equals(candidate, gameName, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var alias in aliases)
        {
            if (string.Equals(candidate, alias, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (candidate.Contains(gameName, StringComparison.OrdinalIgnoreCase) ||
            gameName.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void LoadSteamIdCache()
    {
        if (_steamIdCacheLoaded) return;
        _steamIdCacheLoaded = true;
        try
        {
            if (Infrastructure.Cache.CacheStore.TryLoad<Dictionary<string, int?>>(SteamIdCachePath, Config.MaxCacheAgeDays, out var dict) && dict != null)
                foreach (var kv in dict) SteamIdCache[kv.Key] = kv.Value;
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to load Steam ID cache: {ex.Message}"); }
    }

    private static void SaveSteamIdCache()
    {
        lock (_steamIdLock)
        {
            if (!_steamIdCacheDirty) return;
            _steamIdCacheDirty = false;
        }
        try
        {
            Dictionary<string, int?> dict;
            lock (_steamIdLock) dict = SteamIdCache.ToDictionary(kv => kv.Key, kv => kv.Value);
            Infrastructure.Cache.CacheStore.Save(SteamIdCachePath, dict);
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to save Steam ID cache: {ex.Message}"); }
    }

    private static void LoadImageCache()
    {
        try
        {
            if (Infrastructure.Cache.CacheStore.TryLoad<Dictionary<string, string?>>(ImageCachePath, Config.MaxCacheAgeDays, out var dict) && dict != null)
                foreach (var kv in dict) Cache[kv.Key] = kv.Value;
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to load image cache: {ex.Message}"); }
    }

    private static void SaveImageCache()
    {
        // no lock needed for ConcurrentDictionary snapshot, but check dirty
        if (!_imageCacheDirty) return;
        _imageCacheDirty = false;
        try
        {
            var dict = Cache.ToDictionary(kv => kv.Key, kv => kv.Value);
            Infrastructure.Cache.CacheStore.Save(ImageCachePath, dict);
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to save image cache: {ex.Message}"); }
    }
}
