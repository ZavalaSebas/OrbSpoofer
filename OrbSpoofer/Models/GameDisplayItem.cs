using System.Collections.Generic;
using System.ComponentModel;

namespace OrbSpoofer.Models;

public class GameDisplayItem : INotifyPropertyChanged
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public string AliasDisplay { get; set; } = "";
    public DiscordGame Game { get; set; } = new();

    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set { _imageUrl = value; PropertyChanged?.Invoke(this, new(nameof(ImageUrl))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
