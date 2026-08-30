using System.Text.Json;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public static class QuestService
{
    public static async Task<List<QuestItem>> GetActivePlayQuestsAsync()
    {
        var json = await NetworkHelper.FetchJsonAsync(Config.QuestApiUrl, headers: Config.DiscordHeaders);
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

                // Tasks: prefer task_config_v2, fallback to task_config
                JsonElement tasks;
                if (config.TryGetProperty("task_config_v2", out var v2) && v2.TryGetProperty("tasks", out var v2Tasks))
                    tasks = v2Tasks;
                else if (config.TryGetProperty("task_config", out var tc) && tc.TryGetProperty("tasks", out var tcTasks))
                    tasks = tcTasks;
                else
                    continue;

                if (!tasks.TryGetProperty("PLAY_ON_DESKTOP", out var playTask)) continue;

                if (!playTask.TryGetProperty("target", out var targetProp)) continue;
                int target;
                try { target = targetProp.GetInt32(); } catch { continue; }

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
