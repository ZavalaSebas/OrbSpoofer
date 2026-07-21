namespace OrbSpoofer.Tests;

public class ConfigTests
{
    [Fact]
    public void AssemblyVersion_ReturnsNonEmpty()
    {
        var version = Config.AssemblyVersion;
        Assert.False(string.IsNullOrEmpty(version));
    }

    [Fact]
    public void AssemblyVersion_MatchesExpectedFormat()
    {
        var version = Config.AssemblyVersion;
        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    [Fact]
    public void AssemblyVersion_ReadsFromAssembly()
    {
        var fromAssembly = typeof(Config).Assembly.GetName().Version?.ToString(3);
        Assert.Equal(fromAssembly, Config.AssemblyVersion);
    }
}
