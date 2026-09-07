using CommunityToolkit.Mvvm.ComponentModel;
using OrbSpoofer.Infrastructure.Settings;

namespace OrbSpoofer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store = new();
    private readonly QuestsViewModel _quests;

    [ObservableProperty] private string _preferredRegion = "";
    [ObservableProperty] private string _discordToken = "";
    [ObservableProperty] private bool _useOfficialApi;
    [ObservableProperty] private bool _notifyNewQuests;
    [ObservableProperty] private int _pollIntervalMinutes = 30;
    [ObservableProperty] private bool _useDiscordAppLink = true;

    public List<string> RegionOptions { get; } =
        ["", "US", "UK", "CA", "AU", "DE", "FR", "ES", "IT", "BR", "MX", "JP", "KR", "IN", "NL", "SE"];
    public List<int> PollOptions { get; } = [15, 30, 60, 120];

    public bool HasToken => !string.IsNullOrWhiteSpace(DiscordToken);
    public string TokenStatus => HasToken ? "Token saved ✓" : "No token saved";

    public SettingsViewModel(QuestsViewModel quests)
    {
        _quests = quests;
        try
        {
            var s = _store.Load();
            _preferredRegion = s.PreferredRegion ?? "";
            _discordToken = s.DiscordToken ?? "";
            _useOfficialApi = s.UseOfficialApi;
            _notifyNewQuests = s.NotifyNewQuests;
            _pollIntervalMinutes = s.PollIntervalMinutes is >= 5 and <= 480 ? s.PollIntervalMinutes : 30;
            _useDiscordAppLink = s.UseDiscordAppLink;
        }
        catch { }
    }

    private void Save()
    {
        try
        {
            _store.Save(new AppSettings
            {
                PreferredRegion = PreferredRegion?.Trim().ToUpperInvariant() ?? "",
                DiscordToken = DiscordToken?.Trim() ?? "",
                UseOfficialApi = UseOfficialApi,
                NotifyNewQuests = NotifyNewQuests,
                PollIntervalMinutes = PollIntervalMinutes,
                UseDiscordAppLink = UseDiscordAppLink,
            });
        }
        catch { }
    }

    partial void OnPreferredRegionChanged(string value)
    {
        PreferredRegion = value?.Trim().ToUpperInvariant() ?? "";
        Save();
        _quests.ApplyRegionFilter();
    }

    partial void OnDiscordTokenChanged(string value)
    {
        Save();
        OnPropertyChanged(nameof(HasToken));
        OnPropertyChanged(nameof(TokenStatus));
    }

    partial void OnUseOfficialApiChanged(bool value) => Save();
    partial void OnNotifyNewQuestsChanged(bool value) => Save();
    partial void OnPollIntervalMinutesChanged(int value) => Save();
    partial void OnUseDiscordAppLinkChanged(bool value) => Save();
}
