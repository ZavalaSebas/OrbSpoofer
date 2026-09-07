using System.Text.Json;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class VideoAutomationTests
{
    [Theory]
    [InlineData(0, 115, 100, 7, 7)]       // normal advance
    [InlineData(100, 115, 200, 7, 107)]   // step within cap
    [InlineData(100, 115, 103, 7, 103)]   // clamped to enrollment clock
    [InlineData(110, 115, 0, 7, 110)]     // clock behind progress: hold
    [InlineData(110, 115, 500, 20, 115)]  // clamped to target
    [InlineData(115, 115, 500, 7, 115)]   // already done
    [InlineData(200, 115, 500, 7, 115)]   // over target
    public void NextTimestamp_RespectsTargetAndClock(double done, double target, double maxAllowed, double step, double expected)
    {
        Assert.Equal(expected, VideoQuestAutomator.NextTimestamp(done, target, maxAllowed, step));
    }

    [Fact]
    public void ParseUserStatus_ReadsSnakeCase()
    {
        using var doc = JsonDocument.Parse("""
            { "user_status": {
                "enrolled_at": "2026-09-07T10:00:00+00:00",
                "progress": { "WATCH_VIDEO": { "value": 42 } } } }
            """);
        var (enrolledAt, done) = DiscordApiClient.ParseUserStatus(
            doc.RootElement, "WATCH_VIDEO", new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc), enrolledAt);
        Assert.Equal(42, done);
    }

    [Fact]
    public void ParseUserStatus_FallsBackGracefully()
    {
        using var doc = JsonDocument.Parse("""{ "ok": true }""");
        var fallback = new DateTime(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
        var (enrolledAt, done) = DiscordApiClient.ParseUserStatus(doc.RootElement, "WATCH_VIDEO", fallback);
        Assert.Equal(fallback, enrolledAt);
        Assert.Equal(0, done);
    }
}
