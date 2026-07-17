using System.Text.Json;
using OrbSpoofer.Exceptions;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public class DiscordDatabase
{
    public List<DiscordGame> Games { get; } = [];
    public string? Source { get; private set; }

    private static readonly string[] SkipExePatterns =
        ["_be.exe", "_eac.exe", "launcher", "unins", "crash", "report", "update", "setup", "install"];

    public async Task LoadAsync(Action<string>? statusCallback = null)
    {
        statusCallback?.Invoke("Connecting to Discord API...");
        if (await LoadFromDiscordApiAsync(statusCallback))
            return;

        statusCallback?.Invoke("Discord API unavailable, using GitHub backup...");
        if (await LoadFromGithubAsync(statusCallback))
            return;

        throw new DatabaseLoadError("Could not load games database from any source.");
    }

    private async Task<bool> LoadFromDiscordApiAsync(Action<string>? statusCallback)
    {
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(Config.DiscordApiUrl, headers: Config.DiscordHeaders);
            ParseGames(json);
            Source = "Discord Official API";
            statusCallback?.Invoke($"Loaded {Games.Count} games from Discord API");
            return true;
        }
        catch (NetworkError)
        {
            return false;
        }
    }

    private async Task<bool> LoadFromGithubAsync(Action<string>? statusCallback)
    {
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(Config.GitHubBackupUrl);
            ParseGames(json);
            Source = "GitHub Backup";
            statusCallback?.Invoke($"Loaded {Games.Count} games from GitHub");
            return true;
        }
        catch (NetworkError)
        {
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
            var nameLower = game.Name.ToLowerInvariant();
            var aliasLower = game.Aliases.Select(a => a.ToLowerInvariant()).ToList();

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
