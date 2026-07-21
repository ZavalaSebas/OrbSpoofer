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
}
