using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer;

public partial class MainWindow : Window
{
    private readonly DiscordDatabase _db = new();
    private readonly GameFaker _faker = new();
    private string? _pendingUpdateTag;
    private string? _pendingUpdateUrl;
    private bool _questsLoadedOnce;
    private readonly DispatcherTimer _searchDebounceTimer;
    private readonly DispatcherTimer _steamSearchDebounceTimer;
    private CancellationTokenSource? _imageResolutionCts;
    private Brush _textSecondaryBrush = System.Windows.Media.Brushes.Gray;

    public MainWindow()
    {
        InitializeComponent();
        _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            PerformDatabaseSearch(animate: true);
        };
        _steamSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _steamSearchDebounceTimer.Tick += (_, _) =>
        {
            _steamSearchDebounceTimer.Stop();
            _ = PerformSteamSearch();
        };
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _textSecondaryBrush = (Brush)FindResource("TextSecondaryBrush");
            await _faker.InitializeAsync(new Progress<string>(msg =>
            {
                LoadingText.Text = msg;
                StatusMessage.Text = msg;
            }));

            CleanupLeftoverFakeExes();
            Updater.CleanupOldExe();

            await _db.LoadAsync(msg =>
            {
                LoadingText.Text = msg;
                StatusMessage.Text = msg;
            });

            DbSourceText.Text = $"Database: {_db.Source} ({_db.Games.Count:N0} games)";

            if (_db.CacheAgeDays.HasValue)
            {
                StatusMessage.Text = $"Database: Local Cache ({_db.Games.Count:N0} games, {_db.CacheAgeDays.Value}d old)";
                StatusMessage.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
            }
            else
            {
                StatusMessage.Text = $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
            }
            HeaderStatusText.Text = _db.CacheAgeDays.HasValue
                ? $"{_db.Games.Count:N0} games (cached {_db.CacheAgeDays.Value}d ago)"
                : $"{_db.Games.Count:N0} games loaded from {_db.Source}";
            HeaderStatusText.Foreground = _textSecondaryBrush;
            VersionText.Text = $"v{Config.AssemblyVersion}";
            GameCount.Text = $"{_db.Games.Count:N0} games in database";

            var steamPath = SteamService.GetSteamPath();
            SteamPathText.Text = steamPath ?? "Steam not found";

            var questsOk = await LoadQuestsAsync();
            ShowView(questsOk ? QuestsView : DatabaseView);

            if (UI.Windows.WelcomeWindow.ShouldShow())
            {
                var welcome = new UI.Windows.WelcomeWindow
                {
                    Owner = this
                };
                welcome.ShowDialog();
            }

            _ = CheckForUpdateAsync();
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
        QuestsView.Visibility = Visibility.Collapsed;
        view.Visibility = Visibility.Visible;

        ResetButtonStyles();
        var btn = view switch
        {
            _ when view == DatabaseView => BtnDatabase,
            _ when view == ManualView => BtnManual,
            _ when view == SteamView => BtnSteam,
            _ when view == CreditsView => BtnCredits,
            _ when view == QuestsView => BtnQuests,
            _ => null
        };
        if (btn != null)
            btn.Background = (System.Windows.Media.Brush)FindResource("CardBrush");
    }

    private void ResetButtonStyles()
    {
        var transparent = System.Windows.Media.Brushes.Transparent;
        BtnQuests.Background = transparent;
        BtnDatabase.Background = transparent;
        BtnManual.Background = transparent;
        BtnSteam.Background = transparent;
        BtnCredits.Background = transparent;
    }

    private void ResetStatusColor()
    {
        StatusMessage.Foreground = _textSecondaryBrush;
    }

    // Navigation
    private void BtnDatabase_Click(object sender, RoutedEventArgs e) => ShowView(DatabaseView);
    private void BtnManual_Click(object sender, RoutedEventArgs e) => ShowView(ManualView);
    private void BtnSteam_Click(object sender, RoutedEventArgs e) => ShowView(SteamView);
    private void BtnCredits_Click(object sender, RoutedEventArgs e)
    {
        foreach (var card in new UIElement[] { CreditAbout, CreditHowItWorks, CreditSteam, CreditDisclaimer, CreditSupport })
        {
            card.Opacity = 0;
            card.RenderTransform = new TranslateTransform(0, 15);
        }

        ShowView(CreditsView);
        DispatchAnimation(AnimateCreditCardsAsync);
    }

    private async void BtnQuests_Click(object sender, RoutedEventArgs e)
    {
        ShowView(QuestsView);
        if (_questsLoadedOnce)
        {
            if (QuestsList.ItemsSource != null)
            {
                _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    HideAllListBoxItems(QuestsList);
                    DispatchAnimation(() => AnimateListBoxItemsAsync(QuestsList));
                }));
            }
            else if (QuestsEmptyText.Visibility != Visibility.Visible)
                QuestsEmptyText.Visibility = Visibility.Visible;
            return;
        }
        await LoadQuestsAsync();
    }

    private async Task<bool> LoadQuestsAsync()
    {
        _questsLoadedOnce = true;
        try
        {
            QuestsLoadingText.Visibility = Visibility.Visible;
            QuestsList.Visibility = Visibility.Collapsed;
            QuestsEmptyText.Visibility = Visibility.Collapsed;

            var allQuests = await QuestService.GetActivePlayQuestsAsync();

            var spoofableIds = new HashSet<string>(
                _db.Games.Where(g => DiscordDatabase.GetWin32Executable(g) != null).Select(g => g.Id));

            var quests = allQuests.Where(q => spoofableIds.Contains(q.ApplicationId ?? "")).ToList();

            if (quests.Count == 0)
            {
                QuestsEmptyText.Text = "No active quests found. Try Discord Database mode to spoof manually.";
                QuestsEmptyText.Visibility = Visibility.Visible;
                return false;
            }

            QuestsList.ItemsSource = quests;
            QuestsList.Visibility = Visibility.Visible;
            StatusMessage.Text = $"{quests.Count} active quest(s) loaded";
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                HideAllListBoxItems(QuestsList);
                DispatchAnimation(() => AnimateListBoxItemsAsync(QuestsList));
            }));
            return true;
        }
        catch (Exception)
        {
            QuestsEmptyText.Text = "No active quests found. The API may be unavailable — use Discord Database mode to spoof manually.";
            QuestsEmptyText.Visibility = Visibility.Visible;
            return false;
        }
        finally
        {
            QuestsLoadingText.Visibility = Visibility.Collapsed;
        }
    }

    private async void QuestsSpoof_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Models.QuestItem quest })
        {
            StatusMessage.Text = $"Looking up game: {quest.GameName}...";

            var matches = _db.Games.Where(g => g.Id == quest.ApplicationId).ToList();
            if (matches.Count == 0)
                matches = _db.Games.Where(g =>
                    g.Name.Contains(quest.GameName, StringComparison.OrdinalIgnoreCase) ||
                    quest.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0)
            {
                StatusMessage.Text = $"No matching game found for: {quest.GameName}";
                return;
            }

            var game = matches[0];
            var exeName = DiscordDatabase.GetWin32Executable(game);
            if (exeName == null)
            {
                StatusMessage.Text = $"No Windows executable found for: {quest.GameName}";
                return;
            }

            StatusMessage.Text = $"Creating fake process for quest: {exeName}...";
            var path = _faker.CreateFakeGame(exeName);

            if (path != null && _faker.LaunchExecutable(path, game.Name))
            {
                StatusMessage.Text = $"Quest spoof active: {quest.GameName}";
            }
            else
            {
                StatusMessage.Text = $"Failed to launch spoof for: {quest.GameName}";
            }
        }
    }

    private void Kofi_Click(object sender, RoutedEventArgs e) => OpenUrl(Config.KofiUrl);
    private void Kofi_HeartClick(object sender, MouseButtonEventArgs e) => OpenUrl(Config.KofiUrl);

    private void GitHubSponsor_Click(object sender, RoutedEventArgs e) => OpenUrl(Config.GitHubSponsorUrl);

    private void GitHubProfile_Click(object sender, MouseButtonEventArgs e) => OpenUrl(Config.RepoUrl);

    private void Strykey_Click(object sender, MouseButtonEventArgs e) => OpenUrl("https://github.com/Strykey");

    private void Orbshacker_Click(object sender, MouseButtonEventArgs e) => OpenUrl("https://github.com/strykey/orbshacker");

    private void OpenUrl(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url,
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
            PerformDatabaseSearch(animate: true);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => PerformDatabaseSearch(animate: true);

    private void PerformDatabaseSearch(bool animate = false)
    {
        _imageResolutionCts?.Cancel();
        _imageResolutionCts?.Dispose();
        _imageResolutionCts = new CancellationTokenSource();
        var ct = _imageResolutionCts.Token;
        var query = SearchBox.Text.Trim();

        if (string.IsNullOrEmpty(query))
        {
            ResultsList.ItemsSource = null;
            NoResultsText.Text = "Type the game name and click ▶ to spoof";
            NoResultsText.Visibility = Visibility.Visible;
            ResetStatusColor();
            StatusMessage.Text = _db.CacheAgeDays.HasValue
                ? $"Database: Local Cache ({_db.Games.Count:N0} games, {_db.CacheAgeDays.Value}d old)"
                : $"Ready — {_db.Games.Count:N0} games loaded from {_db.Source}";
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

            if (animate)
                DispatchAnimation(() => AnimateListBoxItemsAsync(ResultsList));

            _ = ResolveGameImagesAsync(items, ct);
        }
    }

    private async Task ResolveGameImagesAsync(List<GameDisplayItem> items, CancellationToken ct)
    {
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (item, token) =>
        {
            var url = await GameImageService.GetImageUrlAsync(item.Game);
            if (url != null && !token.IsCancellationRequested)
                item.ImageUrl = url;
        });
    }

    private static void HideAllListBoxItems(ListBox listBox)
    {
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                item.Opacity = 0;
                item.RenderTransform = new TranslateTransform(0, 16);
            }
        }
    }

    private async Task AnimateListBoxItemsAsync(ListBox listBox, int delayMs = 60)
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromSeconds(0.35);

        // Ensure everything is hidden before starting staggered animation
        HideAllListBoxItems(listBox);

        await Task.Delay(15);

        for (int i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem item)
            {
                item.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(0, 1, duration) { EasingFunction = ease });

                var translate = new TranslateTransform(0, 16);
                item.RenderTransform = translate;
                translate.BeginAnimation(TranslateTransform.YProperty,
                    new DoubleAnimation(16, 0, duration) { EasingFunction = ease });
            }

            await Task.Delay(delayMs);
        }
    }

    private void DispatchAnimation(Func<Task> animation)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(async () => await animation()));
    }

    private async Task AnimateCreditCardsAsync()
    {
        var cards = new UIElement[] { CreditAbout, CreditHowItWorks, CreditSteam, CreditDisclaimer, CreditSupport };
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        foreach (var card in cards)
        {
            card.Opacity = 0;
            card.RenderTransform = new TranslateTransform(0, 15);

            card.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)) { EasingFunction = ease });
            ((TranslateTransform)card.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(15, 0, TimeSpan.FromSeconds(0.3)) { EasingFunction = ease });

            await Task.Delay(100);
        }
    }

    private static void CleanupLeftoverFakeExes()
    {
        var fakeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Config.FakeExeDir);

        try
        {
            if (!Directory.Exists(fakeDir)) return;

            foreach (var file in Directory.GetFiles(fakeDir, "*.exe"))
            {
                try { File.Delete(file); }
                catch (Exception ex) { Debug.WriteLine($"Failed to delete leftover fake exe: {ex.Message}"); }
            }

            if (!Directory.EnumerateFileSystemEntries(fakeDir).Any())
                Directory.Delete(fakeDir);
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to cleanup fake exe directory: {ex.Message}"); }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            var (needsUpdate, tagName, downloadUrl) = await Updater.CheckForUpdateAsync();

            if (!needsUpdate || string.IsNullOrEmpty(downloadUrl)) return;

            var updateWindow = new UI.Windows.UpdateWindow(tagName!, downloadUrl)
            {
                Owner = this
            };
            updateWindow.ShowDialog();

            _pendingUpdateTag = tagName;
            _pendingUpdateUrl = downloadUrl;
            BtnUpdateReminder.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    private void UpdateReminder_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingUpdateTag == null || _pendingUpdateUrl == null) return;

        var updateWindow = new UI.Windows.UpdateWindow(_pendingUpdateTag, _pendingUpdateUrl)
        {
            Owner = this
        };
        updateWindow.ShowDialog();
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

        if (path != null && _faker.LaunchExecutable(path, exeName))
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

        if (path != null && _faker.LaunchExecutable(path, exeName))
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
        _steamSearchDebounceTimer.Stop();
        _steamSearchDebounceTimer.Start();
    }

    private async void BtnSteamSearch_Click(object sender, RoutedEventArgs e) => await PerformSteamSearch();

    private async Task PerformSteamSearch()
    {
        var query = SteamSearchBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            SteamResultsList.ItemsSource = null;
            SteamNoResultsText.Visibility = Visibility.Visible;
            return;
        }

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

            if (items.Count > 0)
                DispatchAnimation(() => AnimateListBoxItemsAsync(SteamResultsList));
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

            if (path != null && _faker.LaunchExecutable(path, info.Name))
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

