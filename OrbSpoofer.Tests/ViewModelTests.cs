using Moq;
using OrbSpoofer.Helpers;
using OrbSpoofer.Services;
using OrbSpoofer.ViewModels;
using OrbSpoofer.Infrastructure.Settings;
using OrbSpoofer.Security;

namespace OrbSpoofer.Tests;

public class ManualViewModelTests
{
    [Fact]
    public void ExeName_Sets_CanSpoof()
    {
        var faker = new GameFaker();
        var vm = new ManualViewModel(faker);
        Assert.False(vm.CanSpoof);
        vm.ExeName = "test.exe";
        Assert.True(vm.CanSpoof);
        vm.ExeName = "   ";
        Assert.False(vm.CanSpoof);
    }

    [Fact]
    public void Spoof_Fails_Without_SourceExe_Sets_FailedStatus()
    {
        var faker = new GameFaker(); // not initialized, _sourceExe null
        var vm = new ManualViewModel(faker) { ExeName = "game.exe" };
        vm.SpoofCommand.Execute(null);
        Assert.Contains("Failed", vm.StatusMessage);
        Assert.True(vm.HasResult);
    }
}

public class DatabaseSearchViewModelTests
{
    [Fact]
    public void Query_Empty_Shows_NoResults()
    {
        var db = new DiscordDatabase();
        var faker = new GameFaker();
        var dialogs = new Mock<IDialogService>().Object;
        var vm = new DatabaseSearchViewModel(db, faker, dialogs);
        vm.Query = "";
        vm.PerformSearch(animate: false);
        Assert.True(vm.HasNoResults);
        Assert.NotNull(vm.NoResultsText);
    }

    [Fact]
    public void Search_With_NoGames_Returns_NoResults()
    {
        var db = new DiscordDatabase(); // empty
        var faker = new GameFaker();
        var dialogs = new Mock<IDialogService>().Object;
        var vm = new DatabaseSearchViewModel(db, faker, dialogs) { Query = "nonexistent" };
        vm.PerformSearch(false);
        Assert.True(vm.HasNoResults);
        Assert.Equal(0, vm.Results.Count);
    }
}

public class PathContainmentTests
{
    [Fact]
    public void TryResolveUnderRoot_Rejects_Traversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "OrbSpooferTestRoot");
        Assert.Null(PathContainment.TryResolveUnderRoot(root, "../evil.exe"));
        Assert.Null(PathContainment.TryResolveUnderRoot(root, "..\\evil.exe"));
        Assert.Null(PathContainment.TryResolveUnderRoot(root, "sub/../../evil.exe"));
    }

    [Fact]
    public void TryResolveUnderRoot_Accepts_Valid()
    {
        var root = Path.Combine(Path.GetTempPath(), "OrbSpooferTestRoot");
        var result = PathContainment.TryResolveUnderRoot(root, "game.exe");
        Assert.NotNull(result);
        Assert.True(result!.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryResolveUnderRoot_Rejects_Absolute()
    {
        var root = Path.Combine(Path.GetTempPath(), "OrbSpooferTestRoot");
        Assert.Null(PathContainment.TryResolveUnderRoot(root, "C:\\Windows\\System32\\evil.exe"));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void CompletedQuestsStore_RoundTrip()
    {
        var store = new CompletedQuestsStore();
        var tmp = new HashSet<string> { "a", "b" };
        var ex = Record.Exception(() => store.Save(tmp));
        Assert.Null(ex);
        var loaded = store.Load();
        Assert.NotNull(loaded);
    }

    [Fact]
    public void FreeGamesSeenStore_RoundTrip()
    {
        var store = new FreeGamesSeenStoreTyped();
        var ex = Record.Exception(() => store.Save([1, 2, 3]));
        Assert.Null(ex);
        var loaded = store.Load();
        Assert.NotNull(loaded);
    }
}

public class SteamTokonTests
{
    [Fact]
    public void ParseAppInfo_Tokon_Returns_VerifiedExe()
    {
        var json = System.Text.Json.JsonDocument.Parse("""
        {
          "data": {
            "3787240": {
              "common": { "name": "Marvel Tokon" },
              "config": { "installdir": "Marvel Tokon" },
              "depots": { "3787241": {} }
            }
          }
        }
        """).RootElement;
        var info = SteamService.ParseAppInfo(json, 3787240);
        Assert.NotNull(info);
        Assert.Equal("Binaries/Win64/MTFSSteam-Win64-Shipping.exe", info!.Executable);
        Assert.Equal("Marvel Tokon", info.InstallDir);
    }

    [Fact]
    public void GenerateAppManifest_Contains_InstalledDepots()
    {
        var manifest = SteamService.GenerateAppManifest(3787240, "Marvel Tokon", "Marvel Tokon", @"C:\Steam", "3787241");
        Assert.Contains("\"3787241\"", manifest);
        Assert.Contains("InstalledDepots", manifest);
    }
}

public class AppDataMigratorTests
{
    [Fact]
    public void Migrate_Creates_Directory_And_Version()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"OrbSpooferMigratorTest_{Guid.NewGuid():N}");
        try
        {
            OrbSpoofer.Infrastructure.AppData.AppDataMigrator.MigrateToLatest(tmp);
            Assert.True(Directory.Exists(tmp));
            Assert.True(File.Exists(Path.Combine(tmp, "appdata.version")));
            Assert.True(Directory.Exists(Path.Combine(tmp, Config.SteamSearchCacheDir)));
        }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }
}
