using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Services;

/// <summary>Total download counter across all GitHub releases (best-effort, cached 24h).</summary>
public static class GitHubStats
{
    private const string CacheFile = "github_stats.json";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public static string FormatDownloads(long total) => total <= 0 ? "" : $"📥 {total:N0} downloads";

    /// <summary>Pure sum over a /releases API payload (unit-testable).</summary>
    public static long ParseTotalDownloads(string releasesJson)
    {
        long total = 0;
        try
        {
            using var doc = JsonDocument.Parse(releasesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return 0;
            foreach (var release in doc.RootElement.EnumerateArray())
            {
                if (!release.TryGetProperty("assets", out var assets)) continue;
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("download_count", out var count)
                        && count.TryGetInt64(out var n))
                        total += n;
                }
            }
        }
        catch { }
        return total;
    }

    public static async Task<long> GetTotalDownloadsAsync()
    {
        try
        {
            var cached = ReadCache();
            if (cached.HasValue) return cached.Value;
            var json = await NetworkHelper.FetchJsonAsync(
                $"https://api.github.com/repos/{Config.GitHubRepoOwner}/{Config.GitHubRepoName}/releases?per_page=100",
                headers: new() { ["User-Agent"] = "OrbSpoofer", ["Accept"] = "application/vnd.github+json" });
            // Re-serialize for the pure parser (keeps one code path).
            var total = ParseTotalDownloads(JsonSerializer.Serialize(json));
            WriteCache(total);
            return total;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GitHubStats failed: {ex.Message}");
            return 0;
        }
    }

    private static long? ReadCache()
    {
        try
        {
            var path = Path.Combine(Config.AppDataPath, CacheFile);
            if (!File.Exists(path)) return null;
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > CacheTtl) return null;
            var raw = File.ReadAllText(path).Trim();
            return long.TryParse(raw, out var n) && n >= 0 ? n : null;
        }
        catch { return null; }
    }

    private static void WriteCache(long total)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(Path.Combine(Config.AppDataPath, CacheFile), total.ToString());
        }
        catch { }
    }
}
