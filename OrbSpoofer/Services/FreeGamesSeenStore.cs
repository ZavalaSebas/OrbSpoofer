using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Services;

/// <summary>Persists seen giveaway ids (ported from Bridge FreeGamesSeenStore).</summary>
public static class FreeGamesSeenStore
{
    private static string FilePath => Path.Combine(Config.AppDataPath, "free-games-seen.json");

    public static HashSet<int> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            var json = File.ReadAllText(FilePath);
            var arr = JsonSerializer.Deserialize<HashSet<int>>(json);
            return arr ?? [];
        }
        catch { return []; }
    }

    public static void Save(HashSet<int> ids)
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(ids));
        }
        catch { }
    }

    public static void MarkSeen(IEnumerable<int> ids)
    {
        var seen = Load();
        foreach (var id in ids) seen.Add(id);
        Save(seen);
    }
}
