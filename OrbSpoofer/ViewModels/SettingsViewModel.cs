using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Infrastructure.Settings;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsStore _store = new();
    private readonly QuestsViewModel _quests;
    private readonly IDialogService _dialogs;
    private bool _consentRunning;

    [ObservableProperty] private string _preferredRegion = "";
    [ObservableProperty] private string _discordToken = "";
    [ObservableProperty] private bool _useOfficialApi;
    [ObservableProperty] private bool _notifyNewQuests;
    [ObservableProperty] private int _pollIntervalMinutes = 30;
    [ObservableProperty] private bool _notifyOrbsOnly = true;
    [ObservableProperty] private bool _useDiscordAppLink = true;

    public List<string> RegionOptions { get; } =
        ["", "US", "UK", "CA", "AU", "DE", "FR", "ES", "IT", "BR", "MX", "JP", "KR", "IN", "NL", "SE"];
    public List<int> PollOptions { get; } = [15, 30, 60, 120];

    public bool HasToken => !string.IsNullOrWhiteSpace(DiscordToken);
    public string TokenStatus => HasToken ? "Token saved ✓" : "No token saved";

    [ObservableProperty] private string _tokenCheckResult = "";
    [ObservableProperty] private bool _tokenCheckOk;
    [ObservableProperty] private bool _isCheckingToken;

    public SettingsViewModel(QuestsViewModel quests, IDialogService dialogs)
    {
        _quests = quests;
        _dialogs = dialogs;
        try
        {
            var s = _store.Load();
            _preferredRegion = s.PreferredRegion ?? "";
            _discordToken = s.DiscordToken ?? "";
            _useOfficialApi = s.UseOfficialApi;
            _notifyNewQuests = s.NotifyNewQuests;
            _pollIntervalMinutes = s.PollIntervalMinutes is >= 5 and <= 480 ? s.PollIntervalMinutes : 30;
            _notifyOrbsOnly = s.NotifyOrbsOnly;
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
                NotifyOrbsOnly = NotifyOrbsOnly,
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
        TokenCheckResult = "";
        OnPropertyChanged(nameof(HasToken));
        OnPropertyChanged(nameof(TokenStatus));
    }

    [RelayCommand]
    private async Task CheckTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(DiscordToken))
        {
            TokenCheckOk = false;
            TokenCheckResult = "Paste your token first.";
            return;
        }
        IsCheckingToken = true;
        TokenCheckResult = "Checking…";
        try
        {
            var (username, display) = await DiscordApiClient.ValidateTokenAsync(DiscordToken.Trim());
            TokenCheckOk = true;
            TokenCheckResult = $"✓ Token works — @{username}" + (string.IsNullOrEmpty(display) ? "" : $" ({display})");
        }
        catch (Exception ex)
        {
            TokenCheckOk = false;
            TokenCheckResult = "✗ " + ex.Message;
        }
        finally { IsCheckingToken = false; }
    }

    private bool _enablingFlow;

    partial void OnUseOfficialApiChanged(bool value)
    {
        if (!value || _enablingFlow) { Save(); _quests.UpdateAutomationAvailability(); return; }
        // Turning ON requires the informed-consent gauntlet — revert until passed.
        UseOfficialApi = false;
        _ = RunConsentFlowAsync();
    }

    private async Task RunConsentFlowAsync()
    {
        if (_consentRunning) return;
        _consentRunning = true;
        try
        {
            var ok = await _dialogs.ShowInformedConsentAsync(
                "⚠ Danger zone — read carefully",
                "The official API mode was built as a proof of concept for testing only. "
                + "It is NOT safe and NOT recommended, even though it is more precise: "
                + "your personal Discord token bypasses password and 2FA, can get your "
                + "account flagged or banned, and a leak means full account takeover.",
                5, "I understand — continue");
            if (ok) ok = await _dialogs.ShowConfirmAsync("Secondary account required",
                "If you want to try it and leave feedback, NEVER use your main account. Use a secondary account only. Do you agree?",
                "I agree — secondary only", "Go back");
            if (ok) ok = await _dialogs.ShowConfirmAsync("Token = full access",
                "A token bypasses password and 2FA. Anyone holding it owns the account. I accept this risk.",
                "I accept the risk", "Go back");
            if (ok) ok = await _dialogs.ShowConfirmAsync("Testing only",
                "Not safe, not recommended — testing and feedback only. Enable the official API mode anyway?",
                "Enable anyway", "Go back");
            if (!ok) return;
            _enablingFlow = true;
            try { UseOfficialApi = true; } // saves + refreshes via the change handler
            finally { _enablingFlow = false; }
        }
        finally { _consentRunning = false; }
    }
    partial void OnNotifyNewQuestsChanged(bool value) { Save(); QuestWatcher.Restart(); }
    partial void OnPollIntervalMinutesChanged(int value) { Save(); QuestWatcher.Restart(); }
    partial void OnNotifyOrbsOnlyChanged(bool value) => Save();
    partial void OnUseDiscordAppLinkChanged(bool value) => Save();
}
