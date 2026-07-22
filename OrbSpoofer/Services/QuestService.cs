using System.Text.Json;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

public static class QuestService
{
    public static async Task<List<QuestItem>> GetActivePlayQuestsAsync()
    {
        var json = await NetworkHelper.FetchJsonAsync(Config.QuestApiUrl);
        var results = new List<QuestItem>();
        var now = DateTime.UtcNow;

        foreach (var element in json.EnumerateArray())
        {
            var config = element.GetProperty("config");
            var expiresAt = config.GetProperty("expires_at").GetDateTime();

            if (expiresAt <= now) continue;

            var tasks = config.TryGetProperty("task_config_v2", out var v2)
                ? v2.GetProperty("tasks")
                : config.GetProperty("task_config").GetProperty("tasks");

            if (!tasks.TryGetProperty("PLAY_ON_DESKTOP", out var playTask)) continue;

            var app = config.GetProperty("application");
            var messages = config.GetProperty("messages");
            var gameTitle = messages.GetProperty("game_title").GetString() ?? "";
            var questName = messages.GetProperty("quest_name").GetString() ?? "";
            var publisher = messages.TryGetProperty("game_publisher", out var pub) ? pub.GetString() : null;

            // Skip promotional quests published by Discord itself (not spoofable)
            if (string.Equals(publisher, "Discord", StringComparison.OrdinalIgnoreCase))
                continue;

            var reward = "Unknown reward";
            if (config.TryGetProperty("rewards_config", out var rewardsConfig) &&
                rewardsConfig.TryGetProperty("rewards", out var rewards) &&
                rewards.GetArrayLength() > 0 &&
                rewards[0].TryGetProperty("messages", out var rewardMsg))
            {
                reward = rewardMsg.GetProperty("name").GetString() ?? "Unknown reward";
            }

            var target = playTask.GetProperty("target").GetInt32();

            var assets = config.GetProperty("assets");
            var imagePath = PickAsset(assets);

            var imageUrl = imagePath != null ? Config.DiscordCdnBase + imagePath : null;
            var appId = app.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

            results.Add(new QuestItem
            {
                Id = element.GetProperty("id").GetString() ?? "",
                GameName = gameTitle,
                QuestName = questName,
                Reward = reward,
                TaskMinutes = target / 60,
                ExpiresAt = expiresAt,
                ImageUrl = imageUrl,
                ApplicationId = appId,
            });
        }

        // Deduplicate by game + quest name
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deduped = new List<QuestItem>(results.Count);
        foreach (var q in results)
        {
            if (seen.Add($"{q.GameName}|{q.QuestName}"))
                deduped.Add(q);
        }

        return deduped;
    }

    private static string? PickAsset(JsonElement assets)
    {
        foreach (var key in new[] { "game_tile_dark", "game_tile_light", "logotype_dark", "logotype_light" })
        {
            if (assets.TryGetProperty(key, out var prop) && prop.GetString() is { Length: > 0 } val && val != "PLACEHOLDER")
                return val;
        }
        return null;
    }
}
