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
}
