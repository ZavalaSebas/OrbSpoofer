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

    private string _taskType = "PLAY_ON_DESKTOP";
    public string TaskType
    {
        get => _taskType;
        set { if (_taskType != value) { _taskType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsVideoQuest)); OnPropertyChanged(nameof(TaskLabel)); } }
    }

    public bool IsVideoQuest => TaskType is "WATCH_VIDEO" or "WATCH_VIDEO_ON_MOBILE";

    // Only desktop play quests can be spoofed with a fake process.
    // Stream / video / activity / console quests open Discord quest home instead.
    public bool IsSpoofable => TaskType == "PLAY_ON_DESKTOP";

    private int _taskSeconds;
    public int TaskSeconds
    {
        get => _taskSeconds;
        set { if (_taskSeconds != value) { _taskSeconds = value; OnPropertyChanged(); OnPropertyChanged(nameof(TaskDurationLabel)); } }
    }

    private bool _isAutomating;
    public bool IsAutomating
    {
        get => _isAutomating;
        set { if (_isAutomating != value) { _isAutomating = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutoButtonText)); } }
    }

    private double _autoProgress;
    /// <summary>Automation progress 0..1.</summary>
    public double AutoProgress
    {
        get => _autoProgress;
        set { if (_autoProgress != value) { _autoProgress = value; OnPropertyChanged(); OnPropertyChanged(nameof(AutoProgressText)); } }
    }

    public string AutoButtonText => IsAutomating ? "Stop ⏹" : "Auto ▶";
    public string AutoProgressText => IsAutomating ? $"{(int)(AutoProgress * TaskSeconds)}/{TaskSeconds}s" : "";

    public string TaskDurationLabel => TaskMinutes > 0 ? $"{TaskMinutes} min" : $"{TaskSeconds} sec";

    public string TaskLabel => TaskType switch
    {
        "PLAY_ON_DESKTOP" => "🎮 Play",
        "STREAM_ON_DESKTOP" => "📡 Stream",
        "WATCH_VIDEO" => "📺 Video",
        "WATCH_VIDEO_ON_MOBILE" => "📱 Mobile video",
        "PLAY_ON_XBOX" => "🎮 Xbox",
        "PLAY_ON_PLAYSTATION" => "🎮 PlayStation",
        "PLAY_ACTIVITY" => "🎯 Activity",
        _ => "▫ " + TaskType,
    };

    private string _regionText = "🌍 Global";
    public string RegionText
    {
        get => _regionText;
        set { if (_regionText != value) { _regionText = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRegionSpecific)); } }
    }

    public bool IsRegionSpecific => RegionKind != "Global";

    public List<string> RegionInclude { get; set; } = [];
    public List<string> RegionExclude { get; set; } = [];

    private bool _isRegionMatch = true;
    public bool IsRegionMatch
    {
        get => _isRegionMatch;
        set { if (_isRegionMatch != value) { _isRegionMatch = value; OnPropertyChanged(); } }
    }

    private string _regionKind = "Global";
    /// <summary>Global | Include (region-locked) | Exclude (blocked in listed regions).</summary>
    public string RegionKind
    {
        get => _regionKind;
        set { if (_regionKind != value) { _regionKind = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRegionSpecific)); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
