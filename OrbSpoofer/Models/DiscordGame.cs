namespace OrbSpoofer.Models;

public class DiscordGame
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public List<DiscordExecutable> Executables { get; set; } = [];
}

public class DiscordExecutable
{
    public string Os { get; set; } = "";
    public string Name { get; set; } = "";
}
