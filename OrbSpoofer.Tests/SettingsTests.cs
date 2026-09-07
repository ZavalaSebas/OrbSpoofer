using OrbSpoofer.Infrastructure.Settings;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class SettingsTests
{
    [Fact]
    public void AppSettings_DefaultsAreSane()
    {
        var s = new AppSettings();
        Assert.Equal("", s.PreferredRegion);
        Assert.Equal("", s.DiscordToken);
        Assert.False(s.UseOfficialApi);
        Assert.False(s.NotifyNewQuests);
        Assert.Equal(30, s.PollIntervalMinutes);
        Assert.True(s.UseDiscordAppLink);
    }

    [Theory]
    [InlineData("Global", new string[] { }, new string[] { }, "US", true)]
    [InlineData("Global", new string[] { }, new string[] { }, "", true)]
    [InlineData("Include", new[] { "US" }, new string[] { }, "US", true)]
    [InlineData("Include", new[] { "US" }, new string[] { }, "UK", false)]
    [InlineData("Include", new[] { "US", "UK" }, new string[] { }, "uk", true)]
    [InlineData("Include", new[] { "US" }, new string[] { }, "", true)]
    [InlineData("Exclude", new string[] { }, new[] { "AU", "UK" }, "UK", false)]
    [InlineData("Exclude", new string[] { }, new[] { "AU", "UK" }, "US", true)]
    [InlineData("Exclude", new string[] { }, new[] { "AU" }, "", true)]
    public void RegionMatcher_MatchesCorrectly(string kind, string[] include, string[] exclude, string pref, bool expected)
    {
        Assert.Equal(expected, RegionMatcher.IsMatch(kind, include, exclude, pref));
    }
}
