using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer;

public partial class MainWindow : Window
{
    private readonly DiscordDatabase _db = new();
    private readonly GameFaker _faker = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _faker.InitializeAsync(new Progress<string>(msg =>
            {
                LoadingText.Text = msg;
                StatusMessage.Text = msg;
            }));

            await _db.LoadAsync(msg =>
            {
                LoadingText.Text = msg;
                StatusMessage.Text = msg;
            });

            DbSourceText.Text = $"Database: {_db.Source} ({_db.Games.Count:N0} games)";
            StatusMessage.Text = $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
            HeaderStatusText.Text = $"{_db.Games.Count:N0} games loaded from {_db.Source}";
            HeaderStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
            VersionText.Text = $"v{Config.Version}";
            GameCount.Text = $"{_db.Games.Count:N0} games in database";

            var steamPath = SteamService.GetSteamPath();
            SteamPathText.Text = steamPath ?? "Steam not found";

            ShowView(DatabaseView);
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Failed to load: {ex.Message}";
            StatusMessage.Text = "Error during initialization";
        }
    }

    private void ShowView(FrameworkElement view)
    {
        LoadingPanel.Visibility = Visibility.Collapsed;
        DatabaseView.Visibility = Visibility.Collapsed;
        ManualView.Visibility = Visibility.Collapsed;
        SteamView.Visibility = Visibility.Collapsed;
        CreditsView.Visibility = Visibility.Collapsed;
        view.Visibility = Visibility.Visible;

        ResetButtonStyles();
        var btn = view switch
        {
            _ when view == DatabaseView => BtnDatabase,
            _ when view == ManualView => BtnManual,
            _ when view == SteamView => BtnSteam,
            _ when view == CreditsView => BtnCredits,
            _ => null
        };
        if (btn != null)
            btn.Background = (System.Windows.Media.Brush)FindResource("CardBrush");
    }

    private void ResetButtonStyles()
    {
        var transparent = System.Windows.Media.Brushes.Transparent;
        BtnDatabase.Background = transparent;
        BtnManual.Background = transparent;
        BtnSteam.Background = transparent;
        BtnCredits.Background = transparent;
    }

    // Navigation
    private void BtnDatabase_Click(object sender, RoutedEventArgs e) => ShowView(DatabaseView);
    private void BtnManual_Click(object sender, RoutedEventArgs e) => ShowView(ManualView);
    private void BtnSteam_Click(object sender, RoutedEventArgs e) => ShowView(SteamView);
    private void BtnCredits_Click(object sender, RoutedEventArgs e) => ShowView(CreditsView);

    private void OpenKofi()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Config.KofiUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Could not open link: {ex.Message}";
        }
    }

    private void Kofi_Click(object sender, RoutedEventArgs e) => OpenKofi();
    private void Kofi_HeartClick(object sender, MouseButtonEventArgs e) => OpenKofi();

    private void GitHubProfile_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Config.RepoUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Could not open link: {ex.Message}";
        }
    }

    private void Strykey_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Strykey",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Could not open link: {ex.Message}";
        }
    }

    private void Orbshacker_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/strykey/orbshacker",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Could not open link: {ex.Message}";
        }
    }

    // Database Search
    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            PerformDatabaseSearch();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        PerformDatabaseSearch();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => PerformDatabaseSearch();

    private void PerformDatabaseSearch()
    {
        var query = SearchBox.Text.Trim();

        if (string.IsNullOrEmpty(query))
        {
            ResultsList.ItemsSource = null;
            NoResultsText.Text = "Search for a game to get started";
            NoResultsText.Visibility = Visibility.Visible;
            StatusMessage.Text = $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
            return;
        }

        var results = _db.SearchGames(query);
        var items = results.Select(g => new GameDisplayItem
        {
            Id = g.Id,
            Name = g.Name,
            Aliases = g.Aliases,
            AliasDisplay = g.Aliases.Count > 0
                ? "Aliases: " + string.Join(", ", g.Aliases.Take(Config.MaxDisplayedAliases)) +
                  (g.Aliases.Count > Config.MaxDisplayedAliases
                      ? $" (+{g.Aliases.Count - Config.MaxDisplayedAliases} more)" : "")
                : "",
            Game = g
        }).ToList();

        ResultsList.ItemsSource = items;

        if (items.Count == 0)
        {
            NoResultsText.Text = $"No games found for '{query}'";
            NoResultsText.Visibility = Visibility.Visible;
            StatusMessage.Text = $"No results for '{query}'";
        }
        else
        {
            NoResultsText.Visibility = Visibility.Collapsed;
            StatusMessage.Text = $"Found {items.Count} game(s) for '{query}'";
        }
    }

    private void SpoofGame_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameDisplayItem item)
            SpoofGame(item);
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is GameDisplayItem item)
            SpoofGame(item);
    }

    private void SpoofGame(GameDisplayItem item)
    {
        var exeName = DiscordDatabase.GetWin32Executable(item.Game);
        if (exeName == null)
        {
            StatusMessage.Text = "No Windows executable found for this game";
            return;
        }

        StatusMessage.Text = $"Creating fake process: {exeName}...";
        var path = _faker.CreateFakeGame(exeName);

        if (path != null && _faker.LaunchExecutable(path))
        {
            StatusMessage.Text = $"Running: {exeName} — Discord should detect the game";
        }
        else
        {
            StatusMessage.Text = $"Failed to launch: {exeName}";
        }
    }

    // Manual Mode
    private void BtnManualSpoof_Click(object sender, RoutedEventArgs e)
    {
        var exeName = ManualExeBox.Text.Trim();
        if (string.IsNullOrEmpty(exeName)) return;

        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";

        exeName = Path.GetFileName(exeName);

        StatusMessage.Text = $"Creating fake process: {exeName}...";
        var path = _faker.CreateFakeGame(exeName);

        if (path != null && _faker.LaunchExecutable(path))
        {
            StatusMessage.Text = $"Running: {exeName}";
            ManualResultPanel.Visibility = Visibility.Visible;
            ManualResultText.Text = $"✓ {exeName} launched successfully!";
        }
        else
        {
            StatusMessage.Text = $"Failed to launch: {exeName}";
            ManualResultPanel.Visibility = Visibility.Visible;
            ManualResultText.Text = "✗ Failed to create the executable";
        }
    }

    // Steam Quest Mode
    private async void SteamSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await PerformSteamSearch();
    }

    private async void SteamSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        await PerformSteamSearch();
    }

    private async void BtnSteamSearch_Click(object sender, RoutedEventArgs e) => await PerformSteamSearch();

    private async Task PerformSteamSearch()
    {
        var query = SteamSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query)) return;

        StatusMessage.Text = $"Searching Steam for '{query}'...";
        try
        {
            var results = await SteamService.SearchGamesAsync(query);

            var items = results.Select(g => new SteamGameDisplayItem
            {
                Id = g.Id,
                Name = g.Name,
            }).ToList();

            SteamResultsList.ItemsSource = items;
            SteamNoResultsText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusMessage.Text = items.Count > 0
                ? $"Found {items.Count} results"
                : $"No results for '{query}'";
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Steam search failed: {ex.Message}";
        }
    }

    private async void SteamSpoof_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SteamGameDisplayItem item)
            await SteamSpoof(item);
    }

    private async void SteamResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SteamResultsList.SelectedItem is SteamGameDisplayItem item)
            await SteamSpoof(item);
    }

    private async Task SteamSpoof(SteamGameDisplayItem item)
    {
        try
        {
            var steamPath = SteamService.GetSteamPath();
            if (steamPath == null)
            {
                StatusMessage.Text = "Steam installation not found";
                return;
            }

            StatusMessage.Text = $"Fetching app info for {item.Name}...";
            var info = await SteamService.FetchAppInfoAsync(item.Id);
            if (info == null)
            {
                StatusMessage.Text = "Could not fetch app info from SteamCMD API";
                return;
            }

            var exePath = Path.Combine(steamPath, "steamapps", "common", info.InstallDir, info.Executable);

            StatusMessage.Text = "Generating appmanifest...";
            var acfCreated = SteamService.WriteAppManifest(
                item.Id, info.Name, info.InstallDir, steamPath, info.DepotId);

            if (!acfCreated)
            {
                StatusMessage.Text = "Failed to create appmanifest";
                return;
            }

            StatusMessage.Text = "Creating fake executable...";
            var path = _faker.CreateSteamFakeGame(exePath);

            if (path != null && _faker.LaunchExecutable(path))
            {
                StatusMessage.Text = $"Steam spoof active: {info.Name}";
            }
            else
            {
                StatusMessage.Text = $"Failed to launch Steam spoof for: {info.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage.Text = $"Steam spoof failed: {ex.Message}";
        }
    }
}

public class GameDisplayItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Aliases { get; set; } = [];
    public string AliasDisplay { get; set; } = "";
    public DiscordGame Game { get; set; } = new();
}

public class SteamGameDisplayItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
