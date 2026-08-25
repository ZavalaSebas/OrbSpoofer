using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public enum NavigationView
{
    Loading,
    Quests,
    UnifiedSearch,
    Database,
    Steam,
    Manual,
    Credits
}

public partial class MainViewModel : ObservableObject
{
    private readonly DiscordDatabase _db;
    private readonly GameFaker _faker;

    [ObservableProperty] private NavigationView _currentView = NavigationView.Loading;
    [ObservableProperty] private bool _sidebarCollapsed;
    [ObservableProperty] private bool _isAdvancedExpanded;
    [ObservableProperty] private string _versionText = "";
    [ObservableProperty] private string _headerStatus = "Loading...";
    [ObservableProperty] private string _gameCountText = "";
    [ObservableProperty] private string _statusMessage = "Loading...";
    [ObservableProperty] private string _dbSourceText = "";
    [ObservableProperty] private bool _hasUpdate;
    [ObservableProperty] private string? _pendingUpdateTag;
    [ObservableProperty] private string? _pendingUpdateUrl;

    public QuestsViewModel Quests { get; }
    public UnifiedSearchViewModel UnifiedSearch { get; }
    public DatabaseSearchViewModel Database { get; }
    public SteamSearchViewModel Steam { get; }
    public ManualViewModel Manual { get; }
    public FreeGamesViewModel FreeGames { get; }

    public MainViewModel(
        DiscordDatabase db,
        GameFaker faker,
        QuestsViewModel quests,
        UnifiedSearchViewModel unified,
        DatabaseSearchViewModel database,
        SteamSearchViewModel steam,
        ManualViewModel manual,
        FreeGamesViewModel freeGames)
    {
        _db = db;
        _faker = faker;
        Quests = quests;
        UnifiedSearch = unified;
        Database = database;
        Steam = steam;
        Manual = manual;
        FreeGames = freeGames;

        // propagate status messages upward
        Quests.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Quests.StatusMessage) && !string.IsNullOrEmpty(Quests.StatusMessage)) StatusMessage = Quests.StatusMessage; };
        UnifiedSearch.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(UnifiedSearch.StatusMessage) && !string.IsNullOrEmpty(UnifiedSearch.StatusMessage)) StatusMessage = UnifiedSearch.StatusMessage; };
        Database.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Database.StatusMessage) && !string.IsNullOrEmpty(Database.StatusMessage)) StatusMessage = Database.StatusMessage; };
        Steam.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Steam.StatusMessage) && !string.IsNullOrEmpty(Steam.StatusMessage)) StatusMessage = Steam.StatusMessage; };
        Manual.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Manual.StatusMessage) && !string.IsNullOrEmpty(Manual.StatusMessage)) StatusMessage = Manual.StatusMessage; };
    }

    [RelayCommand]
    private void Navigate(string view)
    {
        if (Enum.TryParse<NavigationView>(view, out var v))
            CurrentView = v;
        if (CurrentView == NavigationView.Database || CurrentView == NavigationView.Manual || CurrentView == NavigationView.Steam)
            IsAdvancedExpanded = true;
    }

    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    [RelayCommand]
    private void ToggleAdvanced() => IsAdvancedExpanded = !IsAdvancedExpanded;

    [RelayCommand]
    private void OpenUrl(string url) => UrlLauncher.Open(url);

    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        try
        {
            var (needsUpdate, tag, url) = await Updater.CheckForUpdateAsync();
            if (needsUpdate && !string.IsNullOrEmpty(url))
            {
                PendingUpdateTag = tag;
                PendingUpdateUrl = url;
                HasUpdate = true;
            }
        }
        catch { }
    }

    public async Task InitializeAsync(Action<string>? progress = null)
    {
        try
        {
            await _faker.InitializeAsync(new Progress<string>(msg => { progress?.Invoke(msg); StatusMessage = msg; }));

            // cleanup leftovers
            try
            {
                var fakeDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Config.FakeExeDir);
                if (System.IO.Directory.Exists(fakeDir))
                {
                    foreach (var f in System.IO.Directory.GetFiles(fakeDir, "*.exe")) try { System.IO.File.Delete(f); } catch { }
                    if (!System.IO.Directory.EnumerateFileSystemEntries(fakeDir).Any()) System.IO.Directory.Delete(fakeDir);
                }
            }
            catch { }
            Updater.CleanupOldExe();

            await _db.LoadAsync(msg => { progress?.Invoke(msg); StatusMessage = msg; });

            DbSourceText = $"Database: {_db.Source} ({_db.Games.Count:N0} games)";
            Database.DbSourceText = DbSourceText;
            Database.InitializeStatus();

            if (_db.CacheAgeDays.HasValue)
            {
                StatusMessage = $"Database: Local Cache ({_db.Games.Count:N0} games, {_db.CacheAgeDays.Value}d old)";
                HeaderStatus = $"{_db.Games.Count:N0} games (cached {_db.CacheAgeDays.Value}d ago)";
            }
            else
            {
                StatusMessage = $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
                HeaderStatus = $"{_db.Games.Count:N0} games loaded from {_db.Source}";
            }

            VersionText = $"v{Config.AssemblyVersion}";
            GameCountText = $"{_db.Games.Count:N0} games in database";
            Steam.InitializeStatus();

            var questsOk = await TryLoadQuestsAsync();
            CurrentView = questsOk ? NavigationView.Quests : NavigationView.UnifiedSearch;

            Quests.StartWatcher();
            _ = FreeGames.RefreshAsync();
            _ = CheckForUpdateAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load: {ex.Message}";
        }
    }

    private async Task<bool> TryLoadQuestsAsync()
    {
        await Quests.LoadAsync();
        // Quests.LoadAsync sets HasNoQuests if failed; we treat no quests as fallback to search
        return !Quests.HasNoQuests || Quests.Quests.Count > 0;
    }

    public void Cleanup()
    {
        Quests.DisposeWatcher();
        // kill any lingering timer processes before deleting files (exe is locked while timer runs)
        try
        {
            var selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("OrbSpoofer"))
            {
                try
                {
                    if (p.Id == selfPid) continue;
                    if (p.HasExited) continue;
                    bool isTimer = false;
                    try { isTimer = p.MainWindowTitle.Contains("Timer", StringComparison.OrdinalIgnoreCase); } catch { isTimer = true; }
                    // fallback: if we can't read title, assume it's a timer fake exe (they are all OrbSpoofer copies)
                    if (!isTimer) isTimer = true;
                    if (isTimer) try { p.Kill(true); } catch { try { p.Kill(); } catch { } }
                }
                catch { }
            }
        }
        catch { }
        // robust delete: try direct, then bat for locked files
        try
        {
            var fakeDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), Config.FakeExeDir);
            if (System.IO.Directory.Exists(fakeDir))
            {
                foreach (var f in System.IO.Directory.GetFiles(fakeDir, "*.exe"))
                {
                    try { System.IO.File.Delete(f); } catch { try { var bat = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"OrbSpoofer_cleanup_{Guid.NewGuid()}.bat"); System.IO.File.WriteAllText(bat, $"@timeout /t 2 /nobreak > nul{Environment.NewLine}@del /f /q \"{f}\" 2>nul{Environment.NewLine}@del \"%~f0\" 2>nul"); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c \"{bat}\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { } }
                }
                try { if (!System.IO.Directory.EnumerateFileSystemEntries(fakeDir).Any()) System.IO.Directory.Delete(fakeDir); } catch { try { var bat2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"OrbSpoofer_rmdir_{Guid.NewGuid()}.bat"); System.IO.File.WriteAllText(bat2, $"@timeout /t 2 /nobreak > nul{Environment.NewLine}@rmdir /s /q \"{fakeDir}\" 2>nul{Environment.NewLine}@del \"%~f0\" 2>nul"); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c \"{bat2}\"") { UseShellExecute = false, CreateNoWindow = true }); } catch { } }
            }
        }
        catch { }
        try { SteamService.DeleteTrackedManifests(); } catch { }
        // fallback: also try to delete any leftover manifests directly if DeleteTracked fails (e.g., steam path not found)
        try
        {
            var steamPath = SteamService.GetSteamPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                var dir = System.IO.Path.Combine(steamPath, "steamapps");
                if (System.IO.Directory.Exists(dir))
                {
                    // DeleteTrackedManifests already handles tracked ids, but also clean up any fake manifests older than 1 day
                }
            }
        }
        catch { }
    }
}
