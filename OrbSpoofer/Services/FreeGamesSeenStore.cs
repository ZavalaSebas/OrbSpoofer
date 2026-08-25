using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Services;

/// <summary>Persists seen giveaway ids (ported from Bridge FreeGamesSeenStore).</summary>
public static class FreeGamesSeenStore
{
    private static Infrastructure.Settings.FreeGamesSeenStoreTyped Store => new();

    public static HashSet<int> Load() => Store.Load();
    public static void Save(HashSet<int> ids) => Store.Save(ids);

    public static void MarkSeen(IEnumerable<int> ids)
    {
        var seen = Load();
        foreach (var id in ids) seen.Add(id);
        Save(seen);
    }
}
