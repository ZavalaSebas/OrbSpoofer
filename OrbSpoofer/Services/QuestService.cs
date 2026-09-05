using System.Text.Json;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public static class QuestService
{
    // Preferred task order when a quest offers several tasks (e.g. Helldivers 2:
    // PLAY_ON_DESKTOP + XBOX + PLAYSTATION). Desktop-spoofable tasks first.
    private static readonly string[] TaskPreference =
    [
        "PLAY_ON_DESKTOP",
        "STREAM_ON_DESKTOP",
        "PLAY_ACTIVITY",
        "WATCH_VIDEO",
        "WATCH_VIDEO_ON_MOBILE",
        "PLAY_ON_XBOX",
        "PLAY_ON_PLAYSTATION",
    ];

    public static async Task<List<QuestItem>> GetActivePlayQuestsAsync()
        => await GetActiveQuestsAsync();

    public static async Task<List<QuestItem>> GetActiveQuestsAsync()
    {
        var json = await NetworkHelper.FetchJsonAsync(Config.QuestApiUrl, headers: Config.DiscordHeaders);
        var regions = await TryLoadRegionsAsync();
        var results = new List<QuestItem>();
        var now = DateTime.UtcNow;

        if (json.ValueKind != JsonValueKind.Array) return results;

        foreach (var element in json.EnumerateArray())
        {
            try
            {
                if (!element.TryGetProperty("id", out var idProp)) continue;
                var questId = idProp.GetString();
                if (string.IsNullOrEmpty(questId)) continue;

                if (!element.TryGetProperty("config", out var config)) continue;

                if (!config.TryGetProperty("expires_at", out var expiresProp)) continue;
                DateTime expiresAt;
                try { expiresAt = expiresProp.GetDateTime(); }
                catch { continue; }
                if (expiresAt <= now) continue;

                // Tasks: merge task_config_v2 + task_config keys, pick preferred
                var taskNames = new HashSet<string>(StringComparer.Ordinal);
                if (config.TryGetProperty("task_config_v2", out var v2) && v2.TryGetProperty("tasks", out var v2Tasks))
                    foreach (var p in v2Tasks.EnumerateObject()) taskNames.Add(p.Name);
                if (config.TryGetProperty("task_config", out var tc) && tc.TryGetProperty("tasks", out var tcTasks))
                    foreach (var p in tcTasks.EnumerateObject()) taskNames.Add(p.Name);
                if (taskNames.Count == 0) continue;

                string? taskType = null;
                foreach (var preferred in TaskPreference)
                    if (taskNames.Contains(preferred)) { taskType = preferred; break; }
                taskType ??= taskNames.First();

                int target = 0;
                if (config.TryGetProperty("task_config_v2", out var v2b) && v2b.TryGetProperty("tasks", out var v2t) &&
                    v2t.TryGetProperty(taskType, out var v2task) && v2task.TryGetProperty("target", out var v2target))
                    try { target = v2target.GetInt32(); } catch { }
                if (target == 0 && config.TryGetProperty("task_config", out var tcb) && tcb.TryGetProperty("tasks", out var tct) &&
                    tct.TryGetProperty(taskType, out var tctask) && tctask.TryGetProperty("target", out var tctarget))
                    try { target = tctarget.GetInt32(); } catch { }

                if (!config.TryGetProperty("messages", out var messages)) continue;
                var gameTitle = messages.TryGetProperty("game_title", out var gt) ? gt.GetString() ?? "" : "";
                var questName = messages.TryGetProperty("quest_name", out var qn) ? qn.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(gameTitle) && string.IsNullOrWhiteSpace(questName)) continue;

                var publisher = messages.TryGetProperty("game_publisher", out var pub) ? pub.GetString() : null;
                if (string.Equals(publisher, "Discord", StringComparison.OrdinalIgnoreCase))
                    continue;

                var reward = "Unknown reward";
                if (config.TryGetProperty("rewards_config", out var rewardsConfig) &&
                    rewardsConfig.TryGetProperty("rewards", out var rewards) &&
                    rewards.ValueKind == JsonValueKind.Array && rewards.GetArrayLength() > 0 &&
                    rewards[0].TryGetProperty("messages", out var rewardMsg) &&
                    rewardMsg.TryGetProperty("name", out var rewardName))
                {
                    reward = rewardName.GetString() ?? "Unknown reward";
                }

                string? appId = null;
                if (config.TryGetProperty("application", out var app) && app.TryGetProperty("id", out var appIdProp))
                    appId = appIdProp.GetString();

                string? imageUrl = null;
                if (config.TryGetProperty("assets", out var assets))
                {
                    var imagePath = PickAsset(assets, appId);
                    if (imagePath != null)
                        imageUrl = Config.DiscordCdnBase + imagePath;
                }

                var regionText = "🌍 Global";
                var regionKind = "Global";
                if (regions.TryGetValue(questId, out var region))
                {
                    regionText = region.Text;
                    regionKind = region.Kind;
                }
                results.Add(new QuestItem
                {
                    Id = questId,
                    GameName = gameTitle,
                    QuestName = questName,
                    Reward = reward,
                    TaskMinutes = target / 60,
                    ExpiresAt = expiresAt,
                    ImageUrl = imageUrl,
                    ApplicationId = appId,
                    TaskType = taskType,
                    RegionText = regionText,
                    RegionKind = regionKind,
                    TaskSeconds = target,
                });
            }
            catch
            {
                // Skip malformed quest entries without breaking the whole batch
                continue;
            }
        }

        // Deduplicate by Id first, then by GameName|QuestName|ExpiresAt to avoid
        // dropping valid distinct quests (ex: 2x Marvel Tokon with different Ids)
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenContent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<QuestItem>(results.Count);
        foreach (var q in results)
        {
            if (!seenIds.Add(q.Id)) continue;
            var contentKey = $"{q.GameName}|{q.QuestName}|{q.ExpiresAt:O}|{q.TaskMinutes}";
            if (!seenContent.Add(contentKey)) continue;
            deduped.Add(q);
        }

        return deduped;
    }

    // Best-effort region labels from /api/regions.
    // Never fails the quest load — on any error returns an empty map (all Global).
    private static async Task<Dictionary<string, (string Text, string Kind)>> TryLoadRegionsAsync()
    {
        var map = new Dictionary<string, (string Text, string Kind)>(StringComparer.Ordinal);
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(Config.QuestRegionsUrl, headers: Config.DiscordHeaders);
            if (json.ValueKind != JsonValueKind.Object) return map;
            if (!json.TryGetProperty("quests", out var quests) || quests.ValueKind != JsonValueKind.Array) return map;
            foreach (var q in quests.EnumerateArray())
            {
                try
                {
                    if (!q.TryGetProperty("id", out var idProp)) continue;
                    var id = idProp.GetString();
                    if (string.IsNullOrEmpty(id)) continue;
                    bool isGlobal = q.TryGetProperty("is_global", out var g) && g.ValueKind == JsonValueKind.True;
                    var includes = new List<string>();
                    var excludes = new List<string>();
                    if (q.TryGetProperty("regions", out var regions) && regions.ValueKind == JsonValueKind.Object)
                    {
                        if (regions.TryGetProperty("include", out var inc) && inc.ValueKind == JsonValueKind.Array)
                            foreach (var r in inc.EnumerateArray()) { var s = r.GetString(); if (!string.IsNullOrEmpty(s)) includes.Add(s); }
                        if (regions.TryGetProperty("exclude", out var exc) && exc.ValueKind == JsonValueKind.Array)
                            foreach (var r in exc.EnumerateArray()) { var s = r.GetString(); if (!string.IsNullOrEmpty(s)) excludes.Add(s); }
                    }
                    map[id] = (isGlobal, includes.Count, excludes.Count) switch
                    {
                        (true, _, _) or (_, 0, 0) => ("🌍 Global", "Global"),
                        (_, _, > 0) when includes.Count == 0 => ("🚫 Not in " + string.Join(", ", excludes), "Exclude"),
                        (_, 1, _) => ("📍 " + includes[0] + " only", "Include"),
                        _ => ("📍 " + string.Join(", ", includes) + " only", "Include"),
                    };
                }
                catch { }
            }
        }
        catch { }
        return map;
    }

    private static string? PickAsset(JsonElement assets, string? appId)
    {
        // Order: game_tile > hero > quest_bar_hero > logotype (dark preferred)
        var candidates = new[]
        {
            "game_tile_dark", "game_tile_light", "game_tile",
            "hero", "quest_bar_hero",
            "logotype_dark", "logotype_light", "logotype"
        };
        foreach (var key in candidates)
        {
            if (assets.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String && prop.GetString() is { Length: > 0 } val && val != "PLACEHOLDER")
            {
                // New format: quests/{questId}/xxx.png contains "/" -> use as-is
                // Old format: "141760339...png" without "/" -> needs app-assets/{appId}/
                if (!val.Contains('/') && !string.IsNullOrEmpty(appId))
                    return $"app-assets/{appId}/{val}";
                return val;
            }
        }
        return null;
    }
}
