namespace OrbSpoofer.Models;

public class SteamGameDisplayItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string ImageUrl => $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{Id}/header.jpg";
}
