using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

/// <summary>Pure helpers for the quest cache: freshness labels and history merge.
/// Kept static + DateTime-injected so they are unit-testable.</summary>
public static class QuestCacheHelpers
{
    public const int MaxHistoryEntries = 200;

    /// <summary>Short age label for the freshness pill ("just now", "5 min ago", "3 h ago", "2 d ago").</summary>
    public static string FormatAge(DateTime fetchedAtUtc, DateTime nowUtc)
    {
        var age = nowUtc - fetchedAtUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return $"{(int)age.TotalMinutes} min ago";
        if (age < TimeSpan.FromDays(1)) return $"{(int)age.TotalHours} h ago";
        return $"{(int)age.TotalDays} d ago";
    }

    /// <summary>Merges freshly fetched quests into the history log: new IDs are
    /// appended, known ones refresh their expiry. Oldest-first-seen entries are
    /// pruned past the cap. Returns the updated list.</summary>
    public static List<QuestHistoryEntry> MergeHistory(List<QuestHistoryEntry> existing, List<QuestItem> fetched, DateTime nowUtc, int cap = MaxHistoryEntries)
    {
        foreach (var q in fetched)
        {
            var idx = existing.FindIndex(e => e.Id == q.Id);
            if (idx >= 0)
            {
                existing[idx] = existing[idx] with { ExpiresAtUtc = q.ExpiresAt };
            }
            else
            {
                existing.Add(new QuestHistoryEntry(q.Id, q.GameName, q.QuestName, q.Reward, q.ApplicationId, q.TaskType, q.ExpiresAt, nowUtc));
            }
        }
        if (existing.Count > cap)
        {
            var drop = existing.Count - cap;
            foreach (var old in existing.OrderBy(e => e.FirstSeenAtUtc).Take(drop).ToList())
                existing.Remove(old);
        }
        return existing;
    }
}
