using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class UnifiedSearchViewModel : ObservableObject
{
    private readonly DiscordDatabase _db;
    private readonly GameFaker _faker;
    private readonly SteamSearchViewModel _steamVm;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _debounce;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private ObservableCollection<UnifiedSearchItem> _results = [];
    [ObservableProperty] private bool _hasNoResults = true;
    [ObservableProperty] private string _noResultsText = "Type a game name or process.exe and press Search";
    [ObservableProperty] private string _statusMessage = "";

    public UnifiedSearchViewModel(DiscordDatabase db, GameFaker faker, SteamSearchViewModel steamVm, IDialogService dialogs)
    {
        _db = db;
        _faker = faker;
        _steamVm = steamVm;
        _dialogs = dialogs;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); _ = PerformSearchAsync(); };
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
            NoResultsText = "Type a game name or process.exe and press Search";
            HasNoResults = true;
            return;
        }
        StatusMessage = $"Searching '{q}'...";
        List<SteamSearchResult> steamHits = [];
        try { steamHits = await SteamService.SearchGamesAsync(q); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Steam search failed: {ex.Message}"); }

        var discordHits = _db.SearchGames(q);
        var items = UnifiedSearch.Merge(q, discordHits, steamHits);

        foreach (var item in items.Where(i => i.DiscordGame != null && string.IsNullOrEmpty(i.ImageUrl)))
        {
            var game = item.DiscordGame!;
            _ = GameImageService.GetImageUrlAsync(game).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully && t.Result != null)
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => item.ImageUrl = t.Result);
            });
        }

        Results = new ObservableCollection<UnifiedSearchItem>(items);
        HasNoResults = items.Count == 0;
        NoResultsText = items.Count == 0 ? $"No results for '{q}'" : "";
        StatusMessage = items.Count > 0 ? $"Found {items.Count} result(s) for '{q}'" : $"No results for '{q}'";
        if (items.Count > 0) OnSearchPerformed?.Invoke();
    }

    public event Action? OnSearchPerformed;

    [RelayCommand]
    private async Task SpoofAsync(UnifiedSearchItem? item)
    {
        if (item == null) return;
        await SpoofInternalAsync(item);
    }

    public async Task SpoofInternalAsync(UnifiedSearchItem item)
    {
        if (!string.IsNullOrEmpty(item.ManualExe))
        {
            var exeName = item.ManualExe;
            StatusMessage = $"Creating fake process: {exeName}...";
            var path = _faker.CreateFakeGame(exeName);
            if (path != null && _faker.LaunchExecutable(path, out var proc2, exeName))
            {
                StatusMessage = $"Running: {exeName}";
                if (proc2 != null) { try { proc2.EnableRaisingEvents = true; proc2.Exited += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() => { try { StatusMessage = proc2.ExitCode != 0 ? "Spoof ended" : "Spoof completed"; } catch { } }); } catch { } }
            }
            else StatusMessage = $"Failed to launch: {exeName}";
            return;
        }
        if (item.DiscordGame != null && DiscordDatabase.GetWin32Executable(item.DiscordGame) is { } exe)
        {
            var display = new GameDisplayItem { Name = item.Name, Game = item.DiscordGame };
            var exe2 = DiscordDatabase.GetWin32Executable(display.Game)!;
            StatusMessage = $"Creating fake process: {exe2}...";
            var path = _faker.CreateFakeGame(exe2);
            if (path != null && _faker.LaunchExecutable(path, out var proc3, exe2))
            {
                StatusMessage = $"Running: {exe2} — Discord should detect the game";
                if (proc3 != null) { try { proc3.EnableRaisingEvents = true; proc3.Exited += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() => { try { StatusMessage = proc3.ExitCode != 0 ? "Spoof ended" : "Spoof completed"; } catch { } }); } catch { } }
            }
            else StatusMessage = $"Failed to launch: {exe2}";
            return;
        }
        if (item.SteamAppId is int sid and > 0)
        {
            await _steamVm.SpoofInternalAsync(new SteamGameDisplayItem { Id = sid, Name = item.Name }, questId: null);
            StatusMessage = _steamVm.StatusMessage;
            return;
        }
        _dialogs.ShowInfo("No executable found", $"{item.Name} has no Discord executable or Steam AppID.", "Try Advanced → Manual Mode with the exact process name.");
        StatusMessage = $"{item.Name} cannot be spoofed automatically";
    }
}
