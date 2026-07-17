namespace OrbSpoofer.Models;

public class SteamSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class SteamAppInfo
{
    public string Name { get; set; } = "";
    public string InstallDir { get; set; } = "";
    public string Executable { get; set; } = "";
    public string? DepotId { get; set; }
}
