using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OrbSpoofer.Models;

public class QuestItem : INotifyPropertyChanged
{
    public string Id { get; set; } = "";

    private string _gameName = "";
    public string GameName { get => _gameName; set { if (_gameName != value) { _gameName = value; OnPropertyChanged(); } } }

    private string _questName = "";
    public string QuestName { get => _questName; set { if (_questName != value) { _questName = value; OnPropertyChanged(); } } }

    private string _reward = "";
    public string Reward { get => _reward; set { if (_reward != value) { _reward = value; OnPropertyChanged(); } } }

    private int _taskMinutes;
    public int TaskMinutes { get => _taskMinutes; set { if (_taskMinutes != value) { _taskMinutes = value; OnPropertyChanged(); } } }

    private DateTime _expiresAt;
    public DateTime ExpiresAt { get => _expiresAt; set { if (_expiresAt != value) { _expiresAt = value; OnPropertyChanged(); } } }

    private string? _imageUrl;
    public string? ImageUrl { get => _imageUrl; set { if (_imageUrl != value) { _imageUrl = value; OnPropertyChanged(); } } }

    private string? _applicationId;
    public string? ApplicationId { get => _applicationId; set { if (_applicationId != value) { _applicationId = value; OnPropertyChanged(); } } }

    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set { if (_isCompleted != value) { _isCompleted = value; OnPropertyChanged(); } }
    }

    private bool _needsSteamMode;
    public bool NeedsSteamMode
    {
        get => _needsSteamMode;
        set { if (_needsSteamMode != value) { _needsSteamMode = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
