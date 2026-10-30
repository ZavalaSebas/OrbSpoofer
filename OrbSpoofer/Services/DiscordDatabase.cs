using System.Diagnostics;
using System.IO;
using System.Text.Json;
using OrbSpoofer.Exceptions;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public class DiscordDatabase
{
    public List<DiscordGame> Games { get; } = [];
    public string? Source { get; private set; }
    public int? CacheAgeDays { get; private set; }

    private static readonly string[] SkipExePatterns =
        ["_be.exe", "_eac.exe", "launcher", "unins", "crash", "report", "update", "setup", "install"];

    private static readonly string CachePath =
        Path.Combine(Config.AppDataPath, Config.DbCacheFile);

    public async Task LoadAsync(Action<string>? statusCallback = null)
    {
        Games.Clear();
        CacheAgeDays = null;

        statusCallback?.Invoke("Connecting to Discord API...");
        var json = await FetchJsonFromUrlAsync(Config.DiscordApiUrl, Config.DiscordHeaders, statusCallback);
        if (json != null)
        {
            ParseGames(json.Value);
            Source = "Discord Official API";
            SaveCache(json.Value);
            statusCallback?.Invoke($"Loaded {Games.Count} games from Discord API");
            return;
        }

        statusCallback?.Invoke("Discord API unavailable, using GitHub backup...");
        json = await FetchJsonFromUrlAsync(Config.GitHubBackupUrl, null, statusCallback);
        if (json != null)
        {
            ParseGames(json.Value);
            Source = "GitHub Backup";
            SaveCache(json.Value);
            statusCallback?.Invoke($"Loaded {Games.Count} games from GitHub");
            return;
        }

        statusCallback?.Invoke(" APIs unavailable, trying local cache...");
        if (LoadFromCache())
            return;

        throw new DatabaseLoadError("Could not load games database from any source.");
    }

    private static async Task<JsonElement?> FetchJsonFromUrlAsync(
        string url, Dictionary<string, string>? headers, Action<string>? statusCallback)
    {
        try
        {
            return await NetworkHelper.FetchJsonAsync(url, headers: headers);
        }
        catch (NetworkError)
        {
            statusCallback?.Invoke($"Failed to fetch from {url}");
            return null;
        }
    }

    private void SaveCache(JsonElement json)
    {
        try { Infrastructure.Cache.CacheStore.Save(CachePath, json.GetRawText()); }
        catch (Exception ex) { Debug.WriteLine($"Failed to save DB cache: {ex.Message}"); }
    }

    private bool LoadFromCache()
    {
        try
        {
            // Fresh copy first; expired copy as last resort (preservation over
            // freshness — an old game→exe mapping still spoofs fine).
            if (TryParseCache(Config.MaxCacheAgeDays)) return true;
            return TryParseCache(int.MaxValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load DB cache: {ex.Message}");
            return false;
        }
    }

    private bool TryParseCache(int maxAgeDays)
    {
        if (!Infrastructure.Cache.CacheStore.TryLoad<string>(CachePath, maxAgeDays, out var rawJson) || rawJson == null)
            return false;
        var json = JsonSerializer.Deserialize<JsonElement>(rawJson);
        ParseGames(json);
        Source = "Local Cache";
        CacheAgeDays = (int)(DateTime.Now - File.GetLastWriteTime(CachePath)).TotalDays;
        return true;
    }

    private void ParseGames(JsonElement json)
    {
        Games.Clear();
        if (json.ValueKind != JsonValueKind.Array) return;

        foreach (var item in json.EnumerateArray())
        {
            var game = new DiscordGame
            {
                Id = item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                IconHash = item.TryGetProperty("icon", out var icon) ? icon.GetString() : null,
            };

            if (item.TryGetProperty("aliases", out var aliases) && aliases.ValueKind == JsonValueKind.Array)
            {
                foreach (var alias in aliases.EnumerateArray())
                    game.Aliases.Add(alias.GetString() ?? "");
            }

            if (item.TryGetProperty("executables", out var execs) && execs.ValueKind == JsonValueKind.Array)
            {
                foreach (var exe in execs.EnumerateArray())
                {
                    game.Executables.Add(new DiscordExecutable
                    {
                        Os = exe.TryGetProperty("os", out var os) ? os.GetString() ?? "" : "",
                        Name = exe.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    });
                }
            }

            if (item.TryGetProperty("third_party_skus", out var skus) && skus.ValueKind == JsonValueKind.Array)
            {
                foreach (var sku in skus.EnumerateArray())
                {
                    var dist = sku.TryGetProperty("distributor", out var d) ? d.GetString() : null;
                    if (!string.Equals(dist, "steam", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (sku.TryGetProperty("id", out var steamId) &&
                        int.TryParse(steamId.GetString(), out var parsed))
                    {
                        game.SteamAppId = parsed;
                        break;
                    }
                }
            }

            Games.Add(game);
        }
    }

    public List<DiscordGame> SearchGames(string query)
    {
        var queryLower = query.ToLowerInvariant();
        var exact = new Dictionary<string, DiscordGame>();
        var partial = new Dictionary<string, DiscordGame>();

        foreach (var game in Games)
        {
            var nameLower = game.NameLower;
            var aliasLower = game.AliasesLower;

            if (queryLower == nameLower || aliasLower.Contains(queryLower))
                exact.TryAdd(game.Id, game);
            else if (nameLower.Contains(queryLower) || aliasLower.Any(a => a.Contains(queryLower)))
                partial.TryAdd(game.Id, game);
        }

        var results = exact.Values.ToList();
        results.AddRange(partial.Values.Where(g => !exact.ContainsKey(g.Id)));
        return results.Take(Config.MaxSearchResults).ToList();
    }

    public static List<string> FilterWin32Exes(DiscordGame game, bool skipPatterns = true)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();

        foreach (var exe in game.Executables)
        {
            if (exe.Os != "win32") continue;
            var name = exe.Name;
            if (name.StartsWith('>'))
                name = name[1..];
            name = name.Replace('\\', '/');

            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
            if (skipPatterns && SkipExePatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            result.Add(name);
        }

        return result;
    }

    public static string? GetWin32Executable(DiscordGame game)
    {
        var candidates = FilterWin32Exes(game, skipPatterns: true);
        return candidates.Count > 0 ? candidates[0] : null;
    }

    private readonly Dictionary<string, List<string>> _appExecutableCache = [];

    /// <summary>Public app metadata endpoint (no auth).</summary>
    public static string BuildAppRpcUrl(string appId) =>
        $"{Config.DiscordApiBase}/applications/{appId}/rpc";

    /// <summary>win32 executable names from an app metadata payload. Launchers go last.</summary>
    public static List<string> ParseWin32Executables(JsonElement json)
    {
        var primary = new List<string>();
        var launchers = new List<string>();
        var seen = new HashSet<string>();

        if (json.ValueKind != JsonValueKind.Object ||
            !json.TryGetProperty("executables", out var exes) ||
            exes.ValueKind != JsonValueKind.Array)
            return primary;

        foreach (var exe in exes.EnumerateArray())
        {
            if (exe.ValueKind != JsonValueKind.Object) continue;
            var os = exe.TryGetProperty("os", out var osProp) ? osProp.GetString() : null;
            if (!string.Equals(os, "win32", StringComparison.OrdinalIgnoreCase)) continue;
            var name = exe.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;

            name = name!.Replace('\\', '/').TrimStart('/');
            if (!seen.Add(name)) continue;

            var isLauncher = exe.TryGetProperty("is_launcher", out var l) && l.ValueKind == JsonValueKind.True;
            (isLauncher ? launchers : primary).Add(name);
        }

        primary.AddRange(launchers);
        return primary;
    }

    /// <summary>Executables for an app missing from the detectable list (e.g. browser
    /// quests). Cached per app id; returns an empty list on failure.</summary>
    public async Task<List<string>> GetAppExecutablesAsync(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return [];
        if (_appExecutableCache.TryGetValue(appId, out var cached)) return cached;

        var result = new List<string>();
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(BuildAppRpcUrl(appId), Config.DiscordHeaders);
            result = ParseWin32Executables(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App executable lookup failed for {appId}: {ex.Message}");
        }

        _appExecutableCache[appId] = result;
        return result;
    }

    public DiscordGame? FindBySteamAppId(int steamAppId) =>
        Games.FirstOrDefault(g => g.SteamAppId == steamAppId);

    public static bool NeedsSteamSpoof(DiscordGame game, out int steamAppId)
    {
        steamAppId = game.SteamAppId ?? 0;
        return GetWin32Executable(game) == null && steamAppId > 0;
    }
}
