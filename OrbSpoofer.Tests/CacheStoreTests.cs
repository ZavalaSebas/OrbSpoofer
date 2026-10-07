using OrbSpoofer.Infrastructure.Cache;

namespace OrbSpoofer.Tests;

public class CacheStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "orbspoofer-tests-" + Guid.NewGuid());
    private readonly List<string> _files = [];

    private string NewPath()
    {
        Directory.CreateDirectory(_dir);
        var p = Path.Combine(_dir, Guid.NewGuid() + ".json");
        _files.Add(p);
        return p;
    }

    public void Dispose()
    {
        foreach (var f in _files) try { File.Delete(f); } catch { }
        try { Directory.Delete(_dir); } catch { }
    }

    [Fact]
    public void TryLoad_FreshFile_ReturnsValue()
    {
        var path = NewPath();
        CacheStore.Save(path, new List<int> { 1, 2, 3 });

        Assert.True(CacheStore.TryLoad<List<int>>(path, 30, out var value));
        Assert.Equal(new List<int> { 1, 2, 3 }, value);
    }

    [Fact]
    public void TryLoad_ExpiredFile_RejectsButAnyAgeAccepts()
    {
        var path = NewPath();
        CacheStore.Save(path, "hello");
        File.SetLastWriteTime(path, DateTime.Now.AddDays(-40));

        Assert.False(CacheStore.TryLoad<string>(path, 30, out _));
        Assert.True(CacheStore.TryLoadAnyAge<string>(path, out var value, out var ageDays));
        Assert.Equal("hello", value);
        Assert.True(ageDays >= 39);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsFalse()
    {
        var path = NewPath();
        Assert.False(CacheStore.TryLoad<string>(path, 30, out _));
        Assert.False(CacheStore.TryLoadAnyAge<string>(path, out _, out _));
    }
}
