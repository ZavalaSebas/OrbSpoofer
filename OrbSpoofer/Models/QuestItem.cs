namespace OrbSpoofer.Models;

public class QuestItem
{
    public string Id { get; set; } = "";
    public string GameName { get; set; } = "";
    public string QuestName { get; set; } = "";
    public string Reward { get; set; } = "";
    public int TaskMinutes { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? ApplicationId { get; set; }
    public bool IsCompleted { get; set; }
    public bool NeedsSteamMode { get; set; }
}
