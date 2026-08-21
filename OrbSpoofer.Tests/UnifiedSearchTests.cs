using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class UnifiedSearchTests
{
    [Fact]
    public void LooksLikeExe_DetectsExeAndPaths()
    {
        Assert.True(UnifiedSearch.LooksLikeExe("TslGame.exe"));
        Assert.True(UnifiedSearch.LooksLikeExe(@"C:\Games\game.exe"));
        Assert.False(UnifiedSearch.LooksLikeExe("Tokon"));
    }

    [Fact]
    public void NormalizeExeName_AddsExtensionAndStripsPath()
    {
        Assert.Equal("TslGame.exe", UnifiedSearch.NormalizeExeName("TslGame"));
        Assert.Equal("game.exe", UnifiedSearch.NormalizeExeName(@"C:\foo\game.exe"));
    }

    [Fact]
    public void Merge_DiscordWithExe_BadgesDiscord()
    {
        var discord = new DiscordGame
        {
            Id = "1",
            Name = "Fortnite",
            Executables = { new DiscordExecutable { Os = "win32", Name = "FortniteClient-Win64-Shipping.exe" } }
        };
        var items = UnifiedSearch.Merge("fort", [discord], []);
        Assert.Single(items);
        Assert.Equal("Discord", items[0].Badge);
        Assert.Same(discord, items[0].DiscordGame);
    }

    [Fact]
    public void Merge_DiscordExePlusSteam_OneRowBadgesBoth()
    {
        var discord = new DiscordGame
        {
            Id = "1",
            Name = "Counter-Strike 2",
            SteamAppId = 730,
            Executables = { new DiscordExecutable { Os = "win32", Name = "cs2.exe" } }
        };
        var steam = new SteamSearchResult { Id = 730, Name = "Counter-Strike 2" };
        var items = UnifiedSearch.Merge("cs", [discord], [steam]);
        Assert.Single(items);
        Assert.Equal("Both", items[0].Badge);
        Assert.Same(discord, items[0].DiscordGame);
        Assert.Equal(730, items[0].SteamAppId);
    }

    [Fact]
    public void Merge_DiscordHit_DropsSteamDuplicateByName()
    {
        var discord = new DiscordGame
        {
            Name = "Fortnite",
            Executables = { new DiscordExecutable { Os = "win32", Name = "Fortnite.exe" } }
        };
        var steamDup = new SteamSearchResult { Id = 999, Name = "Fortnite" };
        var steamOther = new SteamSearchResult { Id = 10, Name = "Other Game" };
        var items = UnifiedSearch.Merge("fort", [discord], [steamDup, steamOther]);
        Assert.Equal(2, items.Count);
        Assert.Equal("Both", items[0].Badge);
        Assert.Equal("Steam", items[1].Badge);
        Assert.Equal("Other Game", items[1].Name);
    }

    [Fact]
    public void Merge_TokonNoExe_OneSteamRow()
    {
        var discord = new DiscordGame { Id = "tokon", Name = "Marvel Tokon", SteamAppId = 3787240 };
        var steam = new SteamSearchResult { Id = 3787240, Name = "MARVEL Tokon: Fighting Souls" };
        var items = UnifiedSearch.Merge("tokon", [discord], [steam]);
        Assert.Single(items);
        Assert.Equal("Both", items[0].Badge);
        Assert.Equal(3787240, items[0].SteamAppId);
        Assert.Same(discord, items[0].DiscordGame);
    }

    [Fact]
    public void Merge_SteamOnly_AddsSteamRow()
    {
        var steam = new SteamSearchResult { Id = 10, Name = "Demo Game" };
        var items = UnifiedSearch.Merge("demo", [], [steam]);
        Assert.Equal("Steam", items[0].Badge);
        Assert.Equal(10, items[0].SteamAppId);
        Assert.True(items.TrueForAll(i => i.Badge != "Manual"));
    }

    [Fact]
    public void Merge_NoHits_AddsManualFallback()
    {
        var items = UnifiedSearch.Merge("zzzz", [], []);
        Assert.Single(items);
        Assert.Equal("Manual", items[0].Badge);
        Assert.Equal("zzzz.exe", items[0].ManualExe);
    }

    [Fact]
    public void Merge_ExeQuery_AppendsManualRow()
    {
        var discord = new DiscordGame
        {
            Name = "PUBG",
            Executables = { new DiscordExecutable { Os = "win32", Name = "TslGame.exe" } }
        };
        var items = UnifiedSearch.Merge("TslGame.exe", [discord], []);
        Assert.Equal(2, items.Count);
        Assert.Equal("Manual", items[^1].Badge);
        Assert.Equal("TslGame.exe", items[^1].ManualExe);
    }
}
