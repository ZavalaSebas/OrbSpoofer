using System.IO;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public static class UnifiedSearch
{
    public static bool LooksLikeExe(string query)
    {
        var t = query.Trim().Trim('"');
        if (t.Length == 0) return false;
        return t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || t.Contains('\\') || t.Contains('/');
    }

    public static string NormalizeExeName(string query)
    {
        var t = query.Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar);
        t = Path.GetFileName(t);
        if (string.IsNullOrEmpty(t))
            t = "game";
        if (!t.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            t += ".exe";
        return t;
    }

    public static List<UnifiedSearchItem> Merge(
        string query,
        IReadOnlyList<DiscordGame> discordHits,
        IReadOnlyList<SteamSearchResult> steamHits,
        int maxResults = Config.MaxSearchResults)
    {
        var items = new List<UnifiedSearchItem>();
        var usedSteamIds = new HashSet<int>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var game in discordHits)
        {
            if (items.Count >= maxResults) break;

            var hasExe = DiscordDatabase.GetWin32Executable(game) != null;
            DiscordDatabase.NeedsSteamSpoof(game, out var sku);
            var steam = FindSteamMatch(game, steamHits);
            var steamId = sku > 0 ? sku : game.SteamAppId ?? steam?.Id;

            if (steam != null)
                usedSteamIds.Add(steam.Id);
            if (steamId is int sid and > 0)
                usedSteamIds.Add(sid);

            if (hasExe)
            {
                items.Add(new UnifiedSearchItem
                {
                    Name = game.Name,
                    Badge = steam != null ? "Both" : "Discord",
                    Subtitle = AliasSubtitle(game),
                    DiscordGame = game,
                    SteamAppId = steamId,
                    ImageUrl = steamId is int id and > 0
                        ? $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{id}/header.jpg"
                        : null,
                });
                usedNames.Add(game.Name);
                continue;
            }

            if (steamId is not > 0)
                continue;

            items.Add(new UnifiedSearchItem
            {
                Name = game.Name,
                Badge = steam != null ? "Both" : "Steam",
                Subtitle = $"Steam AppID {steamId}",
                DiscordGame = game,
                SteamAppId = steamId,
                ImageUrl = $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{steamId}/header.jpg",
            });
            usedNames.Add(game.Name);
        }

        foreach (var steam in steamHits)
        {
            if (items.Count >= maxResults) break;
            if (!usedSteamIds.Add(steam.Id)) continue;
            if (usedNames.Contains(steam.Name)) continue;
            if (items.Any(i => i.SteamAppId == steam.Id)) continue;

            items.Add(new UnifiedSearchItem
            {
                Name = steam.Name,
                Badge = "Steam",
                Subtitle = $"Steam AppID {steam.Id}",
                SteamAppId = steam.Id,
                ImageUrl = $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{steam.Id}/header.jpg",
            });
        }

        var trimmed = query.Trim();
        if (!string.IsNullOrEmpty(trimmed) && (LooksLikeExe(trimmed) || items.Count == 0))
        {
            var exe = NormalizeExeName(trimmed);
            items.Add(new UnifiedSearchItem
            {
                Name = exe,
                Badge = "Manual",
                Subtitle = "Run this process name",
                ManualExe = exe,
            });
        }

        return items;
    }

    private static SteamSearchResult? FindSteamMatch(DiscordGame game, IReadOnlyList<SteamSearchResult> steamHits)
    {
        if (game.SteamAppId is int sku)
        {
            var byId = steamHits.FirstOrDefault(s => s.Id == sku);
            if (byId != null) return byId;
        }

        return steamHits.FirstOrDefault(s => NamesMatch(s.Name, game.Name)
            || game.Aliases.Any(a => NamesMatch(a, s.Name)));
    }

    private static bool NamesMatch(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string AliasSubtitle(DiscordGame game)
    {
        if (game.Aliases.Count == 0) return "";
        var shown = string.Join(", ", game.Aliases.Take(Config.MaxDisplayedAliases));
        if (game.Aliases.Count > Config.MaxDisplayedAliases)
            shown += $" (+{game.Aliases.Count - Config.MaxDisplayedAliases} more)";
        return "Aliases: " + shown;
    }
}
