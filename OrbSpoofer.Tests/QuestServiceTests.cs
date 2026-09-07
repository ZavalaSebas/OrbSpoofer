using System.Text.Json;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class QuestServiceTests
{
    private const string FlatJson = """
        [
          {
            "id": "q-play",
            "expires_at": "2030-01-01T00:00:00+00:00",
            "application": { "id": "app1" },
            "assets": { "game_tile": "quests/q-play/tile.png" },
            "messages": { "quest_name": "Play It", "game_title": "Game A", "game_publisher": "Studio" },
            "task_config_v2": { "tasks": { "PLAY_ON_DESKTOP": { "target": 900 } } },
            "rewards_config": { "rewards": [ { "messages": { "name": "700 Orbs" } } ] }
          },
          {
            "id": "q-video",
            "expires_at": "2030-01-01T00:00:00+00:00",
            "application": { "id": "app2" },
            "assets": {},
            "messages": { "quest_name": "Watch It", "game_title": "Game B", "game_publisher": "Studio" },
            "task_config_v2": { "tasks": { "WATCH_VIDEO": { "target": 14 } } },
            "rewards_config": { "rewards": [ { "messages": { "name": "200 Orbs" } } ] }
          },
          {
            "id": "q-expired",
            "expires_at": "2020-01-01T00:00:00+00:00",
            "messages": { "quest_name": "Old", "game_title": "Game C", "game_publisher": "Studio" },
            "task_config_v2": { "tasks": { "PLAY_ON_DESKTOP": { "target": 900 } } }
          },
          {
            "id": "q-promo",
            "expires_at": "2030-01-01T00:00:00+00:00",
            "messages": { "quest_name": "Promo", "game_title": "Game D", "game_publisher": "Discord" },
            "task_config_v2": { "tasks": { "PLAY_ON_DESKTOP": { "target": 900 } } }
          }
        ]
        """;

    private const string LegacyJson = """
        [
          {
            "id": "q-legacy",
            "config": {
              "expires_at": "2030-01-01T00:00:00+00:00",
              "application": { "id": "app9" },
              "assets": {},
              "messages": { "quest_name": "Legacy Play", "game_title": "Game Z", "game_publisher": "Studio" },
              "task_config": { "tasks": { "PLAY_ON_DESKTOP": { "target": 1800 } } },
              "rewards_config": { "rewards": [ { "messages": { "name": "Avatar" } } ] }
            }
          }
        ]
        """;

    private static List<QuestItem> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return QuestService.ParseQuests(doc.RootElement, new(), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ParseQuests_FlatFormat_KeepsValidFiltersRest()
    {
        var quests = Parse(FlatJson);
        Assert.Equal(2, quests.Count);

        var play = quests.First(q => q.Id == "q-play");
        Assert.Equal("PLAY_ON_DESKTOP", play.TaskType);
        Assert.True(play.IsSpoofable);
        Assert.Equal(15, play.TaskMinutes);
        Assert.Equal("700 Orbs", play.Reward);
        Assert.Equal("app1", play.ApplicationId);
        Assert.Equal("https://cdn.discordapp.com/quests/q-play/tile.png", play.ImageUrl);
        Assert.Equal("🌍 Global", play.RegionText);

        var video = quests.First(q => q.Id == "q-video");
        Assert.Equal("WATCH_VIDEO", video.TaskType);
        Assert.False(video.IsSpoofable);
        Assert.True(video.IsVideoQuest);
        Assert.Equal("14 sec", video.TaskDurationLabel);
    }

    [Fact]
    public void ParseQuests_LegacyWrappedFormat_StillWorks()
    {
        var quests = Parse(LegacyJson);
        var q = Assert.Single(quests);
        Assert.Equal("PLAY_ON_DESKTOP", q.TaskType);
        Assert.Equal(30, q.TaskMinutes);
        Assert.Equal("Avatar", q.Reward);
    }

    [Fact]
    public void ParseQuests_AppliesRegionMapping()
    {
        using var doc = JsonDocument.Parse(FlatJson);
        var regions = new Dictionary<string, QuestService.QuestRegion>
        {
            ["q-play"] = new("📍 US only", "Include", ["US"], []),
        };
        var quests = QuestService.ParseQuests(doc.RootElement, regions, new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));
        var play = quests.First(q => q.Id == "q-play");
        Assert.Equal("📍 US only", play.RegionText);
        Assert.Equal("Include", play.RegionKind);
        Assert.Equal(["US"], play.RegionInclude);
    }

    [Fact]
    public void ParseQuests_DedupesIdenticalContent()
    {
        const string dup = """
            [
              { "id": "a", "expires_at": "2030-01-01T00:00:00+00:00",
                "messages": { "quest_name": "Q", "game_title": "G", "game_publisher": "S" },
                "task_config_v2": { "tasks": { "WATCH_VIDEO": { "target": 14 } } } },
              { "id": "a", "expires_at": "2030-01-01T00:00:00+00:00",
                "messages": { "quest_name": "Q", "game_title": "G", "game_publisher": "S" },
                "task_config_v2": { "tasks": { "WATCH_VIDEO": { "target": 14 } } } }
            ]
            """;
        Assert.Single(Parse(dup));
    }
}
