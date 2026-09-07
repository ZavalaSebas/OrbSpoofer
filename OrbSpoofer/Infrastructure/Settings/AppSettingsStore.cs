namespace OrbSpoofer.Infrastructure.Settings;

public sealed class AppSettings
{
    /// <summary>Preferred region code (e.g. "US"). Empty = all regions.</summary>
    public string PreferredRegion { get; set; } = "";
    /// <summary>Discord token for the upcoming official-API mode. Stored locally only.</summary>
    public string DiscordToken { get; set; } = "";
    /// <summary>Reserved for the upcoming official-API mode.</summary>
    public bool UseOfficialApi { get; set; } = false;
    /// <summary>Poll for new quests while the app is open.</summary>
    public bool NotifyNewQuests { get; set; } = false;
    /// <summary>Watcher polling interval in minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 30;
    /// <summary>Only alert for orb-reward quests.</summary>
    public bool NotifyOrbsOnly { get; set; } = true;
    /// <summary>Open quests in the Discord desktop app (false = browser).</summary>
    public bool UseDiscordAppLink { get; set; } = true;
}

public sealed class AppSettingsStore : SettingsStoreBase<AppSettings>
{
    protected override string CurrentFilePath => System.IO.Path.Combine(Config.AppDataPath, "settings.json");
    protected override AppSettings DefaultValue => new();
}
