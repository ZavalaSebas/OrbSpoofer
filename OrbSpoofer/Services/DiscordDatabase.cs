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
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(CachePath, json.GetRawText());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save DB cache: {ex.Message}");
        }
    }

    private bool LoadFromCache()
    {
        try
        {
            if (!File.Exists(CachePath)) return false;

            var fileInfo = new FileInfo(CachePath);
            var ageDays = (DateTime.Now - fileInfo.LastWriteTime).Days;
            if (ageDays > Config.MaxCacheAgeDays)
            {
                Debug.WriteLine($"DB cache is {ageDays} days old (max {Config.MaxCacheAgeDays}), ignoring");
                return false;
            }

            var rawJson = File.ReadAllText(CachePath);
            var json = JsonSerializer.Deserialize<JsonElement>(rawJson);
            ParseGames(json);
            Source = "Local Cache";
            CacheAgeDays = ageDays;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load DB cache: {ex.Message}");
            return false;
        }
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
}
