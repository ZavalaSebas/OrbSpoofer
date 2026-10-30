using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class DiscordDatabaseTests
{
    private static DiscordDatabase CreateDbWithGame(string name, string id, string? alias = null)
    {
        var db = new DiscordDatabase();
        var game = new DiscordGame { Id = id, Name = name };
        if (alias != null) game.Aliases.Add(alias);
        db.Games.Add(game);
        return db;
    }

    private static DiscordDatabase CreateDbWithExeGame(string exeName)
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame
        {
            Id = "1",
            Name = "Test",
            Executables = { new DiscordExecutable { Os = "win32", Name = exeName } }
        });
        return db;
    }

    [Fact]
    public void SearchGames_EmptyQuery_ReturnsNullSource()
    {
        var db = new DiscordDatabase();
        var result = db.SearchGames("");
        Assert.Empty(result);
    }

    [Fact]
    public void SearchGames_ExactName_ReturnsGame()
    {
        var db = CreateDbWithGame("Fortnite", "fortnite_id");
        var result = db.SearchGames("Fortnite");
        Assert.Single(result);
        Assert.Equal("Fortnite", result[0].Name);
    }

    [Fact]
    public void SearchGames_ExactNameCaseInsensitive_ReturnsGame()
    {
        var db = CreateDbWithGame("Fortnite", "fortnite_id");
        var result = db.SearchGames("fortnite");
        Assert.Single(result);
    }

    [Fact]
    public void SearchGames_ExactAlias_ReturnsGame()
    {
        var db = CreateDbWithGame("Fortnite", "fortnite_id", "FN");
        var result = db.SearchGames("FN");
        Assert.Single(result);
    }

    [Fact]
    public void SearchGames_PartialName_ReturnsGame()
    {
        var db = CreateDbWithGame("Grand Theft Auto V", "gta5");
        var result = db.SearchGames("Theft");
        Assert.Single(result);
    }

    [Fact]
    public void SearchGames_NoMatch_ReturnsEmpty()
    {
        var db = CreateDbWithGame("Fortnite", "fortnite_id");
        var result = db.SearchGames("Minecraft");
        Assert.Empty(result);
    }

    [Fact]
    public void SearchGames_MultipleMatches_AllReturned()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame { Id = "1", Name = "Apex Legends" });
        db.Games.Add(new DiscordGame { Id = "2", Name = "League of Legends" });
        var result = db.SearchGames("Legends");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SearchGames_ExactTakesPriorityOverPartial()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame { Id = "1", Name = "Apex Legends" });
        db.Games.Add(new DiscordGame { Id = "2", Name = "Legends" });
        var result = db.SearchGames("Legends");
        Assert.Equal(2, result.Count);
        Assert.Equal("Legends", result[0].Name);
    }

    [Fact]
    public void FilterWin32Exes_OnlyWin32_ReturnsWin32()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        game.Executables.Add(new DiscordExecutable { Os = "osx", Name = "game-osx" });
        var result = DiscordDatabase.FilterWin32Exes(game);
        Assert.Single(result);
        Assert.Contains("game.exe", result);
    }

    [Fact]
    public void FilterWin32Exes_SkipPatterns_ExcludesKnownPatterns()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "launcher.exe" });
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        var result = DiscordDatabase.FilterWin32Exes(game, skipPatterns: true);
        Assert.Single(result);
        Assert.Contains("game.exe", result);
    }

    [Fact]
    public void FilterWin32Exes_SkipDisabled_ReturnsAll()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "launcher.exe" });
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        var result = DiscordDatabase.FilterWin32Exes(game, skipPatterns: false);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void FilterWin32Exes_Duplicates_Deduplicates()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        var result = DiscordDatabase.FilterWin32Exes(game);
        Assert.Single(result);
    }

    [Fact]
    public void GetWin32Executable_ReturnsFirstCandidate()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "win32", Name = "game.exe" });
        var result = DiscordDatabase.GetWin32Executable(game);
        Assert.Equal("game.exe", result);
    }

    [Fact]
    public void GetWin32Executable_NoWin32Exes_ReturnsNull()
    {
        var game = new DiscordGame();
        game.Executables.Add(new DiscordExecutable { Os = "osx", Name = "game-osx" });
        var result = DiscordDatabase.GetWin32Executable(game);
        Assert.Null(result);
    }

    [Fact]
    public void FindBySteamAppId_ReturnsMatchingGame()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame { Id = "1428207854153633852", Name = "Tokon", SteamAppId = 3787240 });
        var found = db.FindBySteamAppId(3787240);
        Assert.NotNull(found);
        Assert.Equal("1428207854153633852", found!.Id);
        Assert.Null(db.FindBySteamAppId(1));
    }

    [Fact]
    public void NeedsSteamSpoof_NoWin32ButSteamSku_ReturnsTrue()
    {
        var game = new DiscordGame { Id = "tokon", SteamAppId = 3787240 };
        Assert.True(DiscordDatabase.NeedsSteamSpoof(game, out var steamId));
        Assert.Equal(3787240, steamId);
    }

    [Fact]
    public void NeedsSteamSpoof_HasWin32Exe_ReturnsFalse()
    {
        var game = new DiscordGame
        {
            SteamAppId = 730,
            Executables = { new DiscordExecutable { Os = "win32", Name = "cs2.exe" } }
        };
        Assert.False(DiscordDatabase.NeedsSteamSpoof(game, out _));
    }

    [Fact]
    public void NeedsSteamSpoof_NoExeNoSteamSku_ReturnsFalse()
    {
        var game = new DiscordGame { Id = "unknown" };
        Assert.False(DiscordDatabase.NeedsSteamSpoof(game, out var steamId));
        Assert.Equal(0, steamId);
    }

    [Fact]
    public void ParseWin32Executables_FirefoxPayload_ReturnsWin32ExeOnly()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"id":"1491208267928703226","name":"Firefox","executables":[
              {"os":"win32","name":"firefox.exe","is_launcher":false},
              {"os":"darwin","name":"firefox.app","is_launcher":false}
            ]}
            """);

        var result = DiscordDatabase.ParseWin32Executables(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("firefox.exe", result[0]);
    }

    [Fact]
    public void ParseWin32Executables_LaunchersGoLast()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"executables":[
              {"os":"win32","name":"launcher.exe","is_launcher":true},
              {"os":"win32","name":"game.exe","is_launcher":false}
            ]}
            """);

        var result = DiscordDatabase.ParseWin32Executables(doc.RootElement);

        Assert.Equal(2, result.Count);
        Assert.Equal("game.exe", result[0]);
        Assert.Equal("launcher.exe", result[1]);
    }

    [Fact]
    public void ParseWin32Executables_NormalizesBackslashesAndDeduplicates()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {"executables":[
              {"os":"win32","name":"Mozilla Firefox\\firefox.exe"},
              {"os":"win32","name":"Mozilla Firefox/firefox.exe"}
            ]}
            """);

        var result = DiscordDatabase.ParseWin32Executables(doc.RootElement);

        Assert.Single(result);
        Assert.Equal("Mozilla Firefox/firefox.exe", result[0]);
    }

    [Fact]
    public void ParseWin32Executables_MissingArray_ReturnsEmpty()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{"id":"1","name":"Test"}""");
        Assert.Empty(DiscordDatabase.ParseWin32Executables(doc.RootElement));
    }

    [Fact]
    public void BuildAppRpcUrl_PointsToPublicAppMetadata()
    {
        Assert.Equal(
            "https://discord.com/api/v9/applications/1491208267928703226/rpc",
            DiscordDatabase.BuildAppRpcUrl("1491208267928703226"));
    }

    [Fact]
    public void DeriveSeriesExecutable_UsesPreviousRelease()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame
        {
            Id = "fc26",
            Name = "EA Sports FC 26",
            Executables = { new DiscordExecutable { Os = "win32", Name = "ea sports fc 26/fc26.exe" } }
        });

        Assert.Equal("ea sports fc 27/fc27.exe", db.DeriveSeriesExecutable("EA Sports FC 27"));
    }

    [Fact]
    public void DeriveSeriesExecutable_PicksClosestRelease_AndIgnoresTrademarks()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame
        {
            Id = "fc24",
            Name = "EA Sports FC™ 24",
            Executables = { new DiscordExecutable { Os = "win32", Name = "ea sports fc 24/fc24.exe" } }
        });
        db.Games.Add(new DiscordGame
        {
            Id = "fc26",
            Name = "EA Sports FC™ 26",
            Executables = { new DiscordExecutable { Os = "win32", Name = "ea sports fc 26/fc26.exe" } }
        });

        Assert.Equal("ea sports fc 27/fc27.exe", db.DeriveSeriesExecutable("EA Sports FC 27"));
    }

    [Fact]
    public void DeriveSeriesExecutable_NoSibling_ReturnsNull()
    {
        var db = new DiscordDatabase();
        Assert.Null(db.DeriveSeriesExecutable("EA Sports FC 27"));
    }

    [Fact]
    public void DeriveSeriesExecutable_NameWithoutNumber_ReturnsNull()
    {
        var db = new DiscordDatabase();
        db.Games.Add(new DiscordGame
        {
            Id = "1",
            Name = "Helldivers 2",
            Executables = { new DiscordExecutable { Os = "win32", Name = "bin/helldivers2.exe" } }
        });

        Assert.Null(db.DeriveSeriesExecutable("Helldivers"));
    }
}
