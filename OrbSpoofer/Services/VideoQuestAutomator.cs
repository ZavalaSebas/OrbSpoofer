namespace OrbSpoofer.Services;

/// <summary>
/// Completes WATCH_VIDEO quests through the official API: enroll, then report
/// increasing playback timestamps until the target is reached.
/// Pacing mirrors a real viewer (small steps, enrollment-clock cap, jitter);
/// rate limits back off instead of hammering.
/// </summary>
public static class VideoQuestAutomator
{
    public const int IntervalSeconds = 7;
    public const int SpeedSeconds = 7;
    public const int MaxFutureSeconds = 10;
    public const int MaxRateLimitRetries = 3;

    /// <summary>Pure pacing step: advance toward target without outrunning the enrollment clock.</summary>
    public static double NextTimestamp(double done, double targetSeconds, double maxAllowedSeconds, double stepSeconds)
    {
        if (done >= targetSeconds) return targetSeconds;
        return Math.Min(Math.Min(targetSeconds, done + stepSeconds), Math.Max(done, maxAllowedSeconds));
    }

    /// <returns>true when Discord reports completion (or target reached).</returns>
    public static async Task<bool> RunAsync(
        string token,
        string questId,
        string taskType,
        int targetSeconds,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        if (targetSeconds <= 0) return false;

        DateTime enrolledAt = DateTime.UtcNow;
        double done = 0;
        try
        {
            (enrolledAt, done) = await DiscordApiClient.EnrollAsync(token, questId, taskType);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase))
        {
            // Already enrolled — continue from scratch; progress posts will resync.
        }
        progress?.Report(done);

        var rand = new Random();
        var rateLimitRetries = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (done >= targetSeconds) return true;

            var maxAllowed = (DateTime.UtcNow - enrolledAt).TotalSeconds + MaxFutureSeconds;
            var stamp = NextTimestamp(done, targetSeconds, maxAllowed, SpeedSeconds + rand.NextDouble() * 2);
            if (stamp <= done)
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), ct);
                continue;
            }

            bool completed;
            try
            {
                (completed, done) = await DiscordApiClient.SendVideoProgressAsync(token, questId, stamp);
                rateLimitRetries = 0;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("429") && rateLimitRetries < MaxRateLimitRetries)
            {
                rateLimitRetries++;
                await Task.Delay(TimeSpan.FromSeconds(60), ct);
                continue;
            }
            progress?.Report(done);

            if (completed || done >= targetSeconds)
            {
                if (!completed)
                {
                    try { (completed, _) = await DiscordApiClient.SendVideoProgressAsync(token, questId, targetSeconds); } catch { }
                }
                return completed || done >= targetSeconds;
            }
            await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), ct);
        }
    }
}
