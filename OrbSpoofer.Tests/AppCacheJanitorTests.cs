using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class AppCacheJanitorTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbspoofer-janitor-" + Guid.NewGuid());

    public AppCacheJanitorTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private void Write(params (string Rel, string Content)[] files)
    {
        foreach (var (rel, content) in files)
        {
            var full = Path.Combine(_dir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(2048, "2 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5 * 1024 * 1024, "5 MB")]
    public void FormatBytes_FormatsExpected(long bytes, string expected) =>
        Assert.Equal(expected, AppCacheJanitor.FormatBytes(bytes));

    [Fact]
    public void Measure_CountsFilesAndSubdirs()
    {
        Write(("db_cache.json", new string('x', 1000)));
        Directory.CreateDirectory(Path.Combine(_dir, "steam_search"));
        File.WriteAllText(Path.Combine(_dir, "steam_search", "q.json"), new string('y', 500));

        var (bytes, files) = AppCacheJanitor.Measure(_dir);

        Assert.Equal(2, files);
        Assert.True(bytes >= 1500);
    }

    [Fact]
    public void Clear_DeletesEntriesAndReportsCounts()
    {
        Write(("db_cache.json", "{}"), ("quests_cache.json", "{}"));
        Directory.CreateDirectory(Path.Combine(_dir, "steam_search"));
        File.WriteAllText(Path.Combine(_dir, "steam_search", "q.json"), "{}");

        var (deleted, failed) = AppCacheJanitor.Clear(_dir);

        Assert.Equal(0, failed);
        Assert.True(deleted >= 3);
        Assert.False(File.Exists(Path.Combine(_dir, "db_cache.json")));
    }

    [Fact]
    public void Measure_EmptyDir_Zeroes()
    {
        var (bytes, files) = AppCacheJanitor.Measure(_dir);
        Assert.Equal(0, bytes);
        Assert.Equal(0, files);
    }
}
