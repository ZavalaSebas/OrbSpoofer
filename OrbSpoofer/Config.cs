namespace OrbSpoofer;

public static class Config
{
    // App identity
    public const string Version = "1.0.0";
    public const string Developer = "OrbSpoofer";

    // GitHub repo
    public const string GitHubRepoOwner = "ZavalaSebas";
    public const string GitHubRepoName = "OrbSpoofer";
    public const string RepoUrl = "https://github.com/" + GitHubRepoOwner + "/" + GitHubRepoName;
    public const string KofiUrl = "https://ko-fi.com/sebastianzavala82573";

    // Discord API
    public const string DiscordApiUrl = "https://discord.com/api/v9/applications/detectable";
    public const string GitHubBackupUrl =
        "https://gist.githubusercontent.com/Cynosphere/"
        + "c1e77f77f0e565ddaac2822977961e76/raw/gameslist.json";

    // Steam
    public const string SteamCmdApiUrl = "https://api.steamcmd.net/v1/info";
    public const string SteamStoreSearchUrl = "https://store.steampowered.com/api/storesearch";
    public const string SteamDefaultPath = @"C:\Program Files (x86)\Steam";
    public const long SteamIdOffset = 76561197960265728L;
    public const string DefaultSteamUserId = "0";

    // Steam manifest
    public const string ManifestOneGiB = "1073741824";
    public const string ManifestPartialBytes = "27262976";
    public const string ManifestStateFlags = "1026";

    // HTTP
    public const int RequestTimeout = 10;
    public const int DownloadBufferSize = 8192;

    // UI
    public const string FakeExeDir = "Win64";
    public const int MaxSearchResults = 20;
    public const int MaxDisplayedAliases = 3;
    public const int TimerDurationMinutes = 15;
    public const string TimerCompleteColor = "#ff6b6b";
    public const int MaxProjectRootDepth = 10;
    public const int PublishTimeoutSeconds = 300;

    public static readonly Dictionary<string, string> DiscordHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "application/json",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Referer"] = "https://discord.com/",
        ["Origin"] = "https://discord.com",
    };
}
