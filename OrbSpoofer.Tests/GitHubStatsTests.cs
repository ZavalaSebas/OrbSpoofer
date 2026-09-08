using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class GitHubStatsTests
{
    [Fact]
    public void ParseTotalDownloads_SumsAllAssets()
    {
        const string json = """
            [
              { "tag_name": "v1", "assets": [ { "download_count": 100 }, { "download_count": 23 } ] },
              { "tag_name": "v2", "assets": [ { "download_count": 559 } ] },
              { "tag_name": "v3", "assets": [] }
            ]
            """;
        Assert.Equal(682, GitHubStats.ParseTotalDownloads(json));
    }

    [Fact]
    public void ParseTotalDownloads_ToleratesGarbage()
    {
        Assert.Equal(0, GitHubStats.ParseTotalDownloads("not json"));
        Assert.Equal(0, GitHubStats.ParseTotalDownloads("{}"));
        Assert.Equal(0, GitHubStats.ParseTotalDownloads("[]"));
        Assert.Equal(0, GitHubStats.ParseTotalDownloads("""[{ "assets": [{ "download_count": "many" }] }]"""));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-5, "")]
    [InlineData(1, "📥 1 downloads")]
    [InlineData(682, "📥 682 downloads")]
    public void FormatDownloads_FormatsCorrectly(long total, string expected)
    {
        Assert.Equal(expected, GitHubStats.FormatDownloads(total));
    }

    [Fact]
    public void FormatDownloads_UsesLocaleThousandsSeparator()
    {
        Assert.Equal(
            $"📥 {24248.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)} downloads",
            GitHubStats.FormatDownloads(24248));
    }
}
