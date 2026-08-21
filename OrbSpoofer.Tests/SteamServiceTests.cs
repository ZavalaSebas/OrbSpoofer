using System.Text.Json;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class SteamServiceTests
{
    [Fact]
    public void GenerateAppManifest_ContainsExpectedFields()
    {
        var result = SteamService.GenerateAppManifest(
            appId: 730,
            name: "Counter-Strike 2",
            installDir: "Counter-Strike Global Offensive",
            steamPath: @"C:\Program Files (x86)\Steam",
            depotId: null);

        Assert.Contains("\"appid\"\t\t\"730\"", result);
        Assert.Contains("\"name\"\t\t\"Counter-Strike 2\"", result);
        Assert.Contains("\"installdir\"\t\t\"Counter-Strike Global Offensive\"", result);
        Assert.Contains("\"LauncherPath\"\t\t\"C:\\Program Files (x86)\\Steam\\steam.exe\"", result);
        Assert.Contains("\"InstalledDepots\"", result);
        Assert.Contains("\"StagedDepots\"", result);
    }

    [Fact]
    public void GenerateAppManifest_WithDepotId_IncludesStagedDepots()
    {
        var result = SteamService.GenerateAppManifest(
            appId: 730,
            name: "CS2",
            installDir: "cs2",
            steamPath: @"C:\Steam",
            depotId: "2347771");

        Assert.Contains("\"2347771\"", result);
        Assert.Contains("\"StagedDepots\"", result);
        Assert.Contains("\"InstalledDepots\"", result);
    }

    [Fact]
    public void GenerateAppManifest_WithoutDepotId_EmptyStagedDepots()
    {
        var result = SteamService.GenerateAppManifest(
            appId: 730,
            name: "CS2",
            installDir: "cs2",
            steamPath: @"C:\Steam");

        Assert.DoesNotContain("\"manifest\"", result);
    }

    [Fact]
    public void GenerateAppManifest_LastOwner_IsNumeric()
    {
        var result = SteamService.GenerateAppManifest(
            appId: 730,
            name: "CS2",
            installDir: "cs2",
            steamPath: @"C:\Steam");

        var match = System.Text.RegularExpressions.Regex.Match(result, "\"LastOwner\"\t\t\"(\\d+)\"");
        Assert.True(match.Success);
        Assert.False(string.IsNullOrEmpty(match.Groups[1].Value));
    }

    [Fact]
    public void ParseAppInfo_MissingConfig_DoesNotThrow()
    {
        var json = JsonDocument.Parse("""
            {"data":{"4732690":{"common":{"name":"Arknights: Endfield"},"public_only":"1"}}}
            """).RootElement;

        var info = SteamService.ParseAppInfo(json, 4732690);

        Assert.NotNull(info);
        Assert.Equal("Arknights: Endfield", info!.Name);
        Assert.Equal("Arknights: Endfield.exe", info.Executable);
    }

    [Fact]
    public void ParseAppInfo_Tokon_UsesDiscordVerifiedShippingExe()
    {
        var json = JsonDocument.Parse("""
            {"data":{"3787240":{
              "common":{"name":"MARVEL Tokon: Fighting Souls"},
              "config":{
                "installdir":"MTFS",
                "launch":{
                  "0":{"description":"Launch Game","executable":"start_protected_game.exe"},
                  "1":{"config":{"betakey":"mtfs_debuggame"},"executable":"REDSteam.exe"}
                }
              },
              "depots":{
                "228989":{"config":{"oslist":"windows"},"depotfromapp":"228980","sharedinstall":"1"},
                "3787241":{"manifests":{"public":{"size":"27397244011"}}}
              }
            }}}
            """).RootElement;

        var info = SteamService.ParseAppInfo(json, 3787240);

        Assert.NotNull(info);
        Assert.Equal("Binaries/Win64/MTFSSteam-Win64-Shipping.exe", info!.Executable);
        Assert.Equal("MTFS", info.InstallDir);
        Assert.Equal("3787241", info.DepotId);
    }

    [Fact]
    public void GetInstallExePath_TokonShippingBinary()
    {
        var path = SteamService.GetInstallExePath(
            @"C:\Program Files (x86)\Steam",
            "MTFS",
            "Binaries/Win64/MTFSSteam-Win64-Shipping.exe");
        Assert.Equal(
            @"C:\Program Files (x86)\Steam\steamapps\common\MTFS\Binaries\Win64\MTFSSteam-Win64-Shipping.exe",
            path);
    }

    [Fact]
    public void GetInstallExePath_JoinsNestedExecutable()
    {
        var path = SteamService.GetInstallExePath(@"C:\Steam", "GameDir", "Bin/Game.exe");
        Assert.Equal(@"C:\Steam\steamapps\common\GameDir\Bin\Game.exe", path);
    }
}
