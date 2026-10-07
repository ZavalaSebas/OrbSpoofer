namespace OrbSpoofer.Models;

/// <summary>One entry of the quest history log: every quest ever seen, kept for
/// preservation + quick access even after it expires or the API goes down.</summary>
public sealed record QuestHistoryEntry(
    string Id,
    string GameName,
    string QuestName,
    string Reward,
    string? ApplicationId,
    string TaskType,
    DateTime ExpiresAtUtc,
    DateTime FirstSeenAtUtc)
{
    public bool IsExpired => ExpiresAtUtc <= DateTime.UtcNow;
}

/// <summary>Disk cache of the quest feed: raw API payloads + fetch time + history.
/// Raw payloads (not parsed items) so expiry filtering re-applies on every load.</summary>
public sealed record QuestCacheData(
    DateTime FetchedAtUtc,
    string QuestsJson,
    string RegionsJson,
    List<QuestHistoryEntry> History)
{
    public bool HasPayload => !string.IsNullOrWhiteSpace(QuestsJson);
}
