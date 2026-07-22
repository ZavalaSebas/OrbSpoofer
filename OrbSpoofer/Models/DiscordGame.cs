namespace OrbSpoofer.Models;

public class DiscordGame
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public List<DiscordExecutable> Executables { get; set; } = [];
    public string? IconHash { get; set; }

    public string NameLower => _nameLower ??= Name.ToLowerInvariant();
    public List<string> AliasesLower => _aliasesLower ??= Aliases.Select(a => a.ToLowerInvariant()).ToList();

    private string? _nameLower;
    private List<string>? _aliasesLower;
}

public class DiscordExecutable
{
    public string Os { get; set; } = "";
    public string Name { get; set; } = "";
}
