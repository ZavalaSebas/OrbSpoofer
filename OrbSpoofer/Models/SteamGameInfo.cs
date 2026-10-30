namespace OrbSpoofer.Models;

public class SteamSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsDlc { get; set; }
}

public class SteamAppInfo
{
    public string Name { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public string Executable { get; set; } = "";
    public string? DepotId { get; set; }
    /// <summary>False when Steam publishes no real .exe (games launched through the
    /// EA app ship a steam2ea:// URI instead), i.e. <see cref="Executable"/> is a guess.</summary>
    public bool HasLaunchExecutable { get; set; }
}
