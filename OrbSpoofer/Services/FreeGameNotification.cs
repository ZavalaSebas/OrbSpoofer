namespace OrbSpoofer.Services;

/// <summary>Free-game giveaway notification (ported from Bridge/Services/FreeGameNotification.cs).</summary>
public sealed class FreeGameNotification
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Worth { get; init; } = string.Empty;
    public string Thumbnail { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string OpenGiveawayUrl { get; init; } = string.Empty;
    public string GamerpowerUrl { get; init; } = string.Empty;
    public string Platforms { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
    public DateTime PublishedDate { get; init; }

    public bool IsEpic => Platforms.Contains("Epic Games Store", StringComparison.OrdinalIgnoreCase);
    public bool IsSteam => Platforms.Contains("Steam", StringComparison.OrdinalIgnoreCase);
    public string PlatformLabel => IsEpic ? "Epic" : IsSteam ? "Steam" : Platforms.Split(',').FirstOrDefault()?.Trim() ?? "PC";
}
