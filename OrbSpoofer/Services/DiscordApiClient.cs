using System.Text.Json;

namespace OrbSpoofer.Services;

/// <summary>
/// Minimal official Discord API client (user-token mode).
/// Read-only: validates the token and fetches the personalized quest list.
/// The token is only ever sent to discord.com — never logged or stored elsewhere.
/// </summary>
public static class DiscordApiClient
{
    private static Dictionary<string, string> Headers(string token) => new(Config.DiscordHeaders)
    {
        ["Authorization"] = token,
        ["X-Discord-Locale"] = "en-US",
    };

    /// <returns>(username, displayName) or throws with a friendly message.</returns>
    public static async Task<(string Username, string? DisplayName)> ValidateTokenAsync(string token)
    {
        JsonElement me;
        try
        {
            me = await NetworkHelper.FetchJsonAsync($"{Config.DiscordApiBase}/users/@me", Headers(token));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FriendlyError(ex), ex);
        }
        var username = me.TryGetProperty("username", out var u) ? u.GetString() ?? "?" : "?";
        string? display = null;
        try { if (me.TryGetProperty("global_name", out var g)) display = g.GetString(); } catch { }
        return (username, display);
    }

    /// <summary>Enrolls in a quest. Returns (enrolledAt, secondsAlreadyDone) best-effort.</summary>
    public static async Task<(DateTime EnrolledAt, double SecondsDone)> EnrollAsync(string token, string questId, string taskType)
    {
        JsonElement root;
        try
        {
            root = await NetworkHelper.PostJsonAsync($"{Config.DiscordApiBase}/quests/{questId}/enroll", new { }, Headers(token));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FriendlyError(ex), ex);
        }
        return ParseUserStatus(root, taskType, DateTime.UtcNow);
    }

    /// <summary>Reports video progress. Returns (completed, secondsDone).</summary>
    public static async Task<(bool Completed, double SecondsDone)> SendVideoProgressAsync(string token, string questId, double timestamp)
    {
        JsonElement root;
        try
        {
            root = await NetworkHelper.PostJsonAsync(
                $"{Config.DiscordApiBase}/quests/{questId}/video-progress",
                new { timestamp },
                Headers(token));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FriendlyError(ex), ex);
        }
        var completed = false;
        try { completed = root.TryGetProperty("completed_at", out var c) && c.ValueKind != JsonValueKind.Null; } catch { }
        double done = timestamp;
        try
        {
            if (root.TryGetProperty("progress", out var p) && p.TryGetProperty("WATCH_VIDEO", out var w) && w.TryGetProperty("value", out var v))
                done = v.GetDouble();
            else if (root.TryGetProperty("progress", out var p2) && p2.TryGetProperty("WATCH_VIDEO_ON_MOBILE", out var w2) && w2.TryGetProperty("value", out var v2))
                done = v2.GetDouble();
        }
        catch { }
        return (completed, done);
    }

    /// <summary>Pure extraction of (enrolledAt, done) from an enroll/heartbeat body.</summary>
    public static (DateTime EnrolledAt, double SecondsDone) ParseUserStatus(JsonElement root, string taskType, DateTime fallback)
    {
        var enrolledAt = fallback;
        double done = 0;
        try
        {
            JsonElement status = root;
            if (root.TryGetProperty("user_status", out var us)) status = us;
            else if (root.TryGetProperty("userStatus", out var us2)) status = us2;
            if (status.TryGetProperty("enrolled_at", out var ea) && ea.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(ea.GetString(), out var parsed)) enrolledAt = parsed.ToUniversalTime();
            }
            else if (status.TryGetProperty("enrolledAt", out var ea2) && ea2.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(ea2.GetString(), out var parsed2)) enrolledAt = parsed2.ToUniversalTime();
            }
            JsonElement progress = default;
            var hasProgress = status.TryGetProperty("progress", out progress);
            if (hasProgress && progress.TryGetProperty(taskType, out var task) && task.TryGetProperty("value", out var val))
                done = val.GetDouble();
        }
        catch { }
        return (enrolledAt, done);
    }

    /// <returns>Raw quest array element for <see cref="QuestService.ParseQuests"/>.</returns>
    public static async Task<JsonElement> GetMyQuestsAsync(string token)
    {
        JsonElement root;
        try
        {
            root = await NetworkHelper.FetchJsonAsync($"{Config.DiscordApiBase}/quests/@me", Headers(token));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(FriendlyError(ex), ex);
        }
        return ExtractQuestArray(root);
    }

    /// <summary>Pure extraction: <c>{quests: [...]}</c> or a bare array (mirror shape).</summary>
    public static JsonElement ExtractQuestArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("quests", out var quests)
            && quests.ValueKind == JsonValueKind.Array)
            return quests;
        throw new InvalidOperationException("Unexpected quest response shape (no quests array).");
    }

    private static string FriendlyError(Exception ex)
    {
        var msg = ex.Message;
        if (msg.Contains("401", StringComparison.OrdinalIgnoreCase) || msg.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
            return "Invalid or expired token (401). Paste a fresh token from your Discord session.";
        if (msg.Contains("403", StringComparison.OrdinalIgnoreCase))
            return "Token rejected (403). The account may be locked or flagged.";
        if (msg.Contains("429", StringComparison.OrdinalIgnoreCase))
            return "Rate limited (429). Wait a few minutes and try again.";
        return $"Discord request failed: {msg}";
    }
}
