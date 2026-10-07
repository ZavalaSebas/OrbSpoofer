using System.Text.Json;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class QuestCacheTests
{
    [Theory]
    [InlineData(0, "just now")]
    [InlineData(5, "5 min ago")]
    [InlineData(59, "59 min ago")]
    [InlineData(60, "1 h ago")]
    [InlineData(180, "3 h ago")]
    [InlineData(2880, "2 d ago")]
    public void FormatAge_ReturnsExpectedLabel(int ageMinutes, string expected)
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(expected, QuestCacheHelpers.FormatAge(now.AddMinutes(-ageMinutes), now));
    }

    private static QuestItem Quest(string id, DateTime expires) => new()
    {
        Id = id,
        GameName = "Game " + id,
        QuestName = "Quest " + id,
        Reward = "700 Orbs",
        ApplicationId = "app-" + id,
        TaskType = "PLAY_ON_DESKTOP",
        ExpiresAt = expires,
    };

    [Fact]
    public void MergeHistory_AddsNewAndRefreshesExpiry()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var existing = new List<QuestHistoryEntry>
        {
            new("old", "Game old", "Quest old", "700 Orbs", "app-old", "PLAY_ON_DESKTOP",
                new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc)),
        };
        var fetched = new List<QuestItem>
        {
            Quest("old", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)),
            Quest("new", new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)),
        };

        var merged = QuestCacheHelpers.MergeHistory(existing, fetched, now);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), merged.First(e => e.Id == "old").ExpiresAtUtc);
        var added = merged.First(e => e.Id == "new");
        Assert.Equal(now, added.FirstSeenAtUtc);
        Assert.Equal("PLAY_ON_DESKTOP", added.TaskType);
    }

    [Fact]
    public void MergeHistory_PrunesOldestPastCap()
    {
        var now = new DateTime(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
        var existing = new List<QuestHistoryEntry>
        {
            new("a", "G", "Q", "R", null, "PLAY_ON_DESKTOP", now.AddDays(1), now.AddDays(-3)),
            new("b", "G", "Q", "R", null, "PLAY_ON_DESKTOP", now.AddDays(1), now.AddDays(-2)),
        };
        var fetched = new List<QuestItem> { Quest("c", now.AddDays(1)) };

        var merged = QuestCacheHelpers.MergeHistory(existing, fetched, now, cap: 2);

        Assert.Equal(2, merged.Count);
        Assert.DoesNotContain(merged, e => e.Id == "a");
        Assert.Contains(merged, e => e.Id == "c");
    }

    [Fact]
    public void ParseRegions_ValidPayload_MapsKinds()
    {
        using var doc = JsonDocument.Parse("""
            {"quests":[
              {"id":"q1","is_global":true},
              {"id":"q2","is_global":false,"regions":{"include":["US"],"exclude":[]}},
              {"id":"q3","is_global":false,"regions":{"include":[],"exclude":["AU","UK"]}}
            ]}
            """);
        var map = QuestService.ParseRegions(doc.RootElement);

        Assert.Equal("Global", map["q1"].Kind);
        Assert.Equal("Include", map["q2"].Kind);
        Assert.Equal("Exclude", map["q3"].Kind);
        Assert.Equal("📍 US only", map["q2"].Text);
    }

    [Fact]
    public void ParseRegions_UndefinedPayload_ReturnsEmpty()
    {
        Assert.Empty(QuestService.ParseRegions(default));
    }

    [Fact]
    public void QuestCacheData_HasPayload_ReflectsContent()
    {
        Assert.False(new QuestCacheData(DateTime.MinValue, "", "", []).HasPayload);
        Assert.True(new QuestCacheData(DateTime.UtcNow, "[]", "", []).HasPayload);
    }
}
