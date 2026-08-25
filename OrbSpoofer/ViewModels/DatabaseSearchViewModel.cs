using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class DatabaseSearchViewModel : ObservableObject
{
    private readonly DiscordDatabase _db;
    private readonly GameFaker _faker;
    private readonly IDialogService _dialogs;
    private readonly DispatcherTimer _debounce;
    private CancellationTokenSource? _imageCts;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private ObservableCollection<GameDisplayItem> _results = [];
    [ObservableProperty] private bool _hasNoResults = true;
    [ObservableProperty] private string _noResultsText = "Type the game name and click ▶ to spoof";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _dbSourceText = "";

    public DatabaseSearchViewModel(DiscordDatabase db, GameFaker faker, IDialogService dialogs)
    {
        _db = db;
        _faker = faker;
        _dialogs = dialogs;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); PerformSearch(animate: true); };
    }

    public void InitializeStatus()
    {
        DbSourceText = $"Database: {_db.Source} ({_db.Games.Count:N0} games)";
        StatusMessage = _db.CacheAgeDays.HasValue
            ? $"Database: Local Cache ({_db.Games.Count:N0} games, {_db.CacheAgeDays.Value}d old)"
            : $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
        NoResultsText = "Type the game name and click ▶ to spoof";
        HasNoResults = true;
    }

    partial void OnQueryChanged(string value)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    [RelayCommand]
    private void Search() => PerformSearch(animate: true);

    public void PerformSearch(bool animate = false)
    {
        _imageCts?.Cancel();
        _imageCts?.Dispose();
        _imageCts = new CancellationTokenSource();
        var ct = _imageCts.Token;
        var q = Query.Trim();

        if (string.IsNullOrEmpty(q))
        {
            Results = [];
            NoResultsText = "Type the game name and click ▶ to spoof";
            HasNoResults = true;
            StatusMessage = _db.CacheAgeDays.HasValue
                ? $"Database: Local Cache ({_db.Games.Count:N0} games, {_db.CacheAgeDays.Value}d old)"
                : $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
            return;
        }

        var hits = _db.SearchGames(q);
        var items = hits.Select(g => new GameDisplayItem
        {
            Id = g.Id,
            Name = g.Name,
            Aliases = g.Aliases,
            AliasDisplay = g.Aliases.Count > 0
                ? "Aliases: " + string.Join(", ", g.Aliases.Take(Config.MaxDisplayedAliases)) +
                  (g.Aliases.Count > Config.MaxDisplayedAliases ? $" (+{g.Aliases.Count - Config.MaxDisplayedAliases} more)" : "")
                : "",
            Game = g
        }).ToList();

        Results = new ObservableCollection<GameDisplayItem>(items);

        if (items.Count == 0)
        {
            NoResultsText = $"No games found for '{q}'";
            HasNoResults = true;
            StatusMessage = $"No results for '{q}'";
        }
        else
        {
            HasNoResults = false;
            StatusMessage = $"Found {items.Count} game(s) for '{q}'";
            _ = ResolveImagesAsync(items, ct);
        }

        OnSearchPerformed?.Invoke(animate);
    }

    public event Action<bool>? OnSearchPerformed;

    private async Task ResolveImagesAsync(List<GameDisplayItem> items, CancellationToken ct)
    {
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (item, token) =>
        {
            var url = await GameImageService.GetImageUrlAsync(item.Game);
            if (url != null && !token.IsCancellationRequested)
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => item.ImageUrl = url);
        });
    }

    [RelayCommand]
    private void Spoof(GameDisplayItem? item)
    {
        if (item == null) return;
        var exeName = DiscordDatabase.GetWin32Executable(item.Game);
        if (exeName == null)
        {
            _dialogs.ShowInfo("No executable found",
                $"{item.Name} has no executable registered in Discord's database, so process spoofing won't work.",
                "Use Steam Quest mode or Manual mode to spoof this game.");
            StatusMessage = $"{item.Name} has no executable in Discord's database";
            return;
        }
        StatusMessage = $"Creating fake process: {exeName}...";
        var path = _faker.CreateFakeGame(exeName);
        if (path != null && _faker.LaunchExecutable(path, out var proc, exeName))
        {
            StatusMessage = $"Running: {exeName} — Discord should detect the game";
            if (proc != null)
            {
                try
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (_, _) => System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        try { if (proc.ExitCode != 0) StatusMessage = "Spoof ended"; else StatusMessage = "Spoof completed"; } catch { }
                    });
                }
                catch { }
            }
        }
        else StatusMessage = $"Failed to launch: {exeName}";
    }
}
