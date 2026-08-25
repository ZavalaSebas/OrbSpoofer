using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class SteamSearchViewModel : ObservableObject
{
    private readonly DiscordDatabase _db;
    private readonly GameFaker _faker;
    private readonly DispatcherTimer _debounce;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private ObservableCollection<SteamGameDisplayItem> _results = [];
    [ObservableProperty] private bool _hasNoResults = true;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _steamPathText = "Detecting...";

    public SteamSearchViewModel(DiscordDatabase db, GameFaker faker)
    {
        _db = db;
        _faker = faker;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = PerformSearchAsync(); };
    }

    public void InitializeStatus()
    {
        var p = SteamService.GetSteamPath();
        SteamPathText = p ?? "Steam not found";
    }

    partial void OnQueryChanged(string value)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    [RelayCommand]
    private async Task SearchAsync() => await PerformSearchAsync();

    public async Task PerformSearchAsync()
    {
        var q = Query.Trim();
        if (string.IsNullOrEmpty(q))
        {
            Results = [];
            HasNoResults = true;
            return;
        }
        StatusMessage = $"Searching Steam for '{q}'...";
        try
        {
            var hits = await SteamService.SearchGamesAsync(q);
            var items = hits.Select(g => new SteamGameDisplayItem { Id = g.Id, Name = g.Name }).ToList();
            Results = new ObservableCollection<SteamGameDisplayItem>(items);
            HasNoResults = items.Count == 0;
            StatusMessage = items.Count > 0 ? $"Found {items.Count} results" : $"No results for '{q}'";
            if (items.Count > 0) OnSearchPerformed?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Steam search failed: {ex.Message}";
        }
    }

    public event Action? OnSearchPerformed;

    [RelayCommand]
    private async Task SpoofAsync(SteamGameDisplayItem? item)
    {
        if (item == null) return;
        await SpoofInternalAsync(item, questId: null);
    }

    public async Task SpoofInternalAsync(SteamGameDisplayItem item, string? questId)
    {
        try
        {
            var steamPath = SteamService.GetSteamPath();
            if (steamPath == null) { StatusMessage = "Steam installation not found"; return; }
            StatusMessage = $"Fetching app info for {item.Name}...";
            var info = await SteamService.FetchAppInfoAsync(item.Id);
            if (info == null) { StatusMessage = "Could not fetch app info from SteamCMD API"; return; }
            if (string.IsNullOrEmpty(info.Executable)) { StatusMessage = "No executable found for this game"; return; }

            StatusMessage = "Generating appmanifest...";
            var ok = SteamService.WriteAppManifest(item.Id, info.Name, info.InstallDir, steamPath, info.DepotId);
            if (!ok) { StatusMessage = "Failed to create appmanifest"; return; }

            var exePath = SteamService.GetInstallExePath(steamPath, info.InstallDir, info.Executable);
            StatusMessage = "Creating fake executable...";
            var path = _faker.CreateSteamFakeGame(exePath);
            var discordAppId = _db.FindBySteamAppId(item.Id)?.Id;

            if (path != null && _faker.LaunchExecutable(path, out var proc, info.Name, questId, discordAppId))
            {
                if (questId != null) ActiveSpoofQuestName = info.Name;
                StatusMessage = questId != null ? $"Quest spoof active (Steam): {info.Name}" : $"Steam spoof active: {info.Name} — Steam mode may not work for all games, use DB mode if it doesn't detect";
                if (proc != null)
                {
                    try
                    {
                        proc.EnableRaisingEvents = true;
                        var capturedName = info.Name;
                        var capturedQuestId = questId;
                        proc.Exited += (_, _) =>
                        {
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    if (proc.ExitCode != 0)
                                    {
                                        if (capturedQuestId != null && ActiveSpoofQuestName == capturedName) ActiveSpoofQuestName = null;
                                        StatusMessage = capturedQuestId != null ? "Spoof cancelled" : "Steam spoof ended";
                                        try { SteamService.DeleteTrackedManifests(); } catch { }
                                    }
                                }
                                catch { }
                            });
                        };
                    }
                    catch { }
                }
            }
            else StatusMessage = $"Failed to launch Steam spoof for: {info.Name}";
        }
        catch (Exception ex) { StatusMessage = $"Steam spoof failed: {ex.Message}"; }
    }

    public string? ActiveSpoofQuestName { get; set; }
}
