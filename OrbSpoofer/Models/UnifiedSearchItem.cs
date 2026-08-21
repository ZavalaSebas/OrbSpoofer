using System.ComponentModel;

namespace OrbSpoofer.Models;

public class UnifiedSearchItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Badge { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public DiscordGame? DiscordGame { get; set; }
    public int? SteamAppId { get; set; }
    public string? ManualExe { get; set; }

    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set { _imageUrl = value; PropertyChanged?.Invoke(this, new(nameof(ImageUrl))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
