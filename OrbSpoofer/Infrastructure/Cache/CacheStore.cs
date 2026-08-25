using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Infrastructure.Cache;

/// <summary>Centralized JSON cache with TTL and atomic writes. Replaces dispersed File.ReadAllText logic.</summary>
public static class CacheStore
{
    public static bool TryLoad<T>(string path, int maxAgeDays, out T? value)
    {
        value = default;
        try
        {
            if (!File.Exists(path)) return false;
            var age = (DateTime.Now - File.GetLastWriteTime(path)).TotalDays;
            if (age > maxAgeDays) return false;
            var json = File.ReadAllText(path);
            value = JsonSerializer.Deserialize<T>(json);
            return value is not null;
        }
        catch { return false; }
    }

    public static void Save<T>(string path, T value)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(value));
            File.Move(tmp, path, overwrite: true);
        }
        catch { }
    }

    public static void CleanupExpired(string directory, int maxAgeDays)
    {
        try
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                try
                {
                    var age = (DateTime.Now - File.GetLastWriteTime(file)).TotalDays;
                    if (age > maxAgeDays) File.Delete(file);
                }
                catch { }
            }
        }
        catch { }
    }
}
