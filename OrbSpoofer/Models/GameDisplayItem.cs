using System.Collections.Generic;

namespace OrbSpoofer.Models;

public class GameDisplayItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public string AliasDisplay { get; set; } = "";
    public DiscordGame Game { get; set; } = new();
}
