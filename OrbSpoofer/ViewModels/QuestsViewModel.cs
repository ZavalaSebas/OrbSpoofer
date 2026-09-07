using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OrbSpoofer.Helpers;
using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.ViewModels;

public partial class QuestsViewModel : ObservableObject
{
    private readonly DiscordDatabase _db;
    private readonly GameFaker _faker;
    private readonly SteamSearchViewModel _steamVm;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private ObservableCollection<QuestItem> _quests = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasNoQuests;
    [ObservableProperty] private string _emptyText = "";
    [ObservableProperty] private bool _canRunAll;
    [ObservableProperty] private string _runAllText = "Run all quests";
    [ObservableProperty] private bool _isRunningAll;
    [ObservableProperty] private bool _canAutoVideos;
    [ObservableProperty] private string _autoVideosText = "Auto videos";
    [ObservableProperty] private bool _isAutoRunning;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string? _activeSpoofQuestName;

    private CancellationTokenSource? _runAllCts;
    private CancellationTokenSource? _autoCts;
    private FileSystemWatcher? _watcher;
    private string _regionPref = "";
    public ICollectionView QuestsView { get; }

    public QuestsViewModel(DiscordDatabase db, GameFaker faker, SteamSearchViewModel steamVm, IDialogService dialogs)
    {
        _db = db;
        _faker = faker;
        _steamVm = steamVm;
        _dialogs = dialogs;
        QuestsView = CollectionViewSource.GetDefaultView(Quests);
        // Region filter only — inserts/removes filter live, no reset needed on load.
        QuestsView.Filter = o => o is not QuestItem q || q.IsRegionMatch;
        // Group by quest type (play / video / …) with dividers in the view.
        // No SortDescriptions: source order already puts playable first, completed last.
        QuestsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(QuestItem.TaskLabel)));
        // No SortDescriptions to avoid Refresh/Reset black flash.
        // Order is maintained manually in Quests (smooth Move) and completion
        // opacity is animated via DataTrigger (0.32s) in the view.
    }

    public void StartWatcher()
    {
        try
        {
            Directory.CreateDirectory(Config.AppDataPath);
            _watcher = new FileSystemWatcher
            {
                Path = Config.AppDataPath,
                Filter = Config.CompletedQuestsFile,
                NotifyFilter = NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnCompletedChanged;
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to set up watcher: {ex.Message}"); }
    }

    private void OnCompletedChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                _ = HandleQuestCompletedAsync();
            });
        }
        catch (Exception ex) { Debug.WriteLine($"OnCompletedChanged failed: {ex.Message}"); }
    }

    private async Task HandleQuestCompletedAsync()
    {
        try
        {
            string? finishedName = ActiveSpoofQuestName;
            if (finishedName is not null)
            {
                StatusMessage = $"Quest completed: {finishedName}";
                ActiveSpoofQuestName = null;
            }
            else StatusMessage = "Quest completed";

            try { Services.SteamService.DeleteTrackedManifests(); } catch { }

            // if currently visible, reload
            await LoadAsync();

            // No popups here by design — completions accumulate quietly and the
            // header Claim button (done/left counts) opens Discord when ready.
        }
        catch (Exception ex) { Debug.WriteLine($"HandleQuestCompleted failed: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        bool isInitial = Quests.Count == 0;
        if (isInitial) IsLoading = true;
        // don't touch HasNoQuests until we have data -> avoids black toggle
        try
        {
            var appSettings = new Infrastructure.Settings.AppSettingsStore().Load();
            List<QuestItem> all;
            var officialFailed = false;
            if (appSettings.UseOfficialApi && !string.IsNullOrWhiteSpace(appSettings.DiscordToken))
            {
                try { all = await QuestService.GetOfficialQuestsAsync(appSettings.DiscordToken.Trim()); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Official API failed, falling back to mirror: {ex.Message}");
                    officialFailed = true;
                    all = await QuestService.GetActivePlayQuestsAsync();
                }
            }
            else
            {
                all = await QuestService.GetActivePlayQuestsAsync();
            }
            var completedIds = Config.LoadCompletedQuestIds();
            var sourceSuffix = officialFailed ? " (official API failed — using mirror)"
                : appSettings.UseOfficialApi && !string.IsNullOrWhiteSpace(appSettings.DiscordToken) ? " (official API)" : "";

            // Keep all API quests visible - don't hide those not in DB (DB may be stale)
            // Instead annotate with NeedsSteamMode so user knows to use Steam/Manual mode
            var filtered = all;

            _regionPref = new Infrastructure.Settings.AppSettingsStore().Load().PreferredRegion ?? "";
            var regionPref = _regionPref;

            foreach (var q in filtered)
            {
                q.IsCompleted = completedIds.Contains(q.Id);
                q.IsRegionMatch = RegionMatcher.IsMatch(q.RegionKind, q.RegionInclude, q.RegionExclude, regionPref);
                if (!q.IsSpoofable)
                {
                    q.NeedsSteamMode = false; // video/stream/activity open Discord instead
                    continue;
                }
                var matching = _db.Games.FirstOrDefault(g => g.Id == q.ApplicationId || g.Name.Contains(q.GameName, StringComparison.OrdinalIgnoreCase) || q.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase));
                if (matching == null)
                    q.NeedsSteamMode = true; // unknown game -> likely needs Steam/manual
                else
                    q.NeedsSteamMode = DiscordDatabase.GetWin32Executable(matching) == null;
            }

            // Playable first (actionable in-app), then video/stream (open Discord), completed last
            var sorted = filtered.OrderBy(q => q.IsCompleted).ThenBy(q => !q.IsSpoofable).ThenBy(q => q.ExpiresAt).ToList();

            if (sorted.Count == 0)
            {
                // clear without animation if already empty
                if (Quests.Count != 0) Quests.Clear();
                EmptyText = "No active quests found. Try Search to spoof a game manually.";
                HasNoQuests = true;
                CanRunAll = false;
                CanAutoVideos = false;
            }
            else
            {
                var playable = sorted.Count(q => !q.IsCompleted && q.IsSpoofable && q.IsRegionMatch);
                if (isInitial)
                {
                    Quests.Clear();
                    foreach (var q in sorted) Quests.Add(q);
                    HasNoQuests = false;
                    RefreshActionButtons();
                    StatusMessage = playable > 0
                        ? $"{sorted.Count} active quest(s) loaded ({playable} playable){RegionSuffix(sorted.Count)}{sourceSuffix}"
                        : $"{sorted.Count} active quest(s) loaded{RegionSuffix(sorted.Count)}{sourceSuffix}";
                    OnQuestsLoaded?.Invoke();
                }
                else
                {
                    // In-place PATCH: no Clear/Reset -> no black flash
                    var byId = Quests.ToDictionary(x => x.Id);
                    var sortedIds = new HashSet<string>(sorted.Select(x => x.Id));

                    // remove missing
                    for (int i = Quests.Count - 1; i >= 0; i--)
                        if (!sortedIds.Contains(Quests[i].Id))
                            Quests.RemoveAt(i);

                    // update existing + insert new in order without abrupt Move
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        var src = sorted[i];
                        if (byId.TryGetValue(src.Id, out var existing))
                        {
                            // patch INPC properties (no container recreation)
                            existing.GameName = src.GameName;
                            existing.QuestName = src.QuestName;
                            existing.Reward = src.Reward;
                            existing.TaskMinutes = src.TaskMinutes;
                            existing.ExpiresAt = src.ExpiresAt;
                            existing.ImageUrl = src.ImageUrl;
                            existing.ApplicationId = src.ApplicationId;
                            existing.IsCompleted = src.IsCompleted;
                            existing.NeedsSteamMode = src.NeedsSteamMode;
                            existing.TaskType = src.TaskType;
                            existing.TaskMinutes = src.TaskMinutes;
                            existing.TaskSeconds = src.TaskSeconds;
                            existing.RegionText = src.RegionText;
                            existing.RegionKind = src.RegionKind;
                            existing.RegionInclude = src.RegionInclude;
                            existing.RegionExclude = src.RegionExclude;
                            existing.IsRegionMatch = src.IsRegionMatch;
                            int cur = Quests.IndexOf(existing);
                            if (cur != i) Quests.Move(cur, i);
                        }
                        else
                        {
                            Quests.Insert(i, src);
                        }
                    }
                    HasNoQuests = false;
                    RefreshActionButtons();
                    StatusMessage = playable > 0
                        ? $"{sorted.Count} active quest(s) loaded ({playable} playable){RegionSuffix(sorted.Count)}{sourceSuffix}"
                        : $"{sorted.Count} active quest(s) loaded{RegionSuffix(sorted.Count)}{sourceSuffix}";
                    // no OnQuestsLoaded on patch -> avoids stagger second flash
                }
            }
        }
        catch
        {
            if (Quests.Count == 0)
            {
                EmptyText = "No active quests found. The API may be unavailable — use Search to spoof a game.";
                HasNoQuests = true;
            }
            CanRunAll = false;
            CanAutoVideos = false;
            StatusMessage = EmptyText;
        }
        finally { IsLoading = false; }
    }

    private string RegionSuffix(int total)
    {
        if (string.IsNullOrWhiteSpace(_regionPref)) return "";
        var shown = Quests.Count(q => q.IsRegionMatch);
        return shown == total ? "" : $" — showing {shown} of {total} (region {_regionPref.Trim().ToUpperInvariant()})";
    }

    /// <summary>Re-applies the region filter after settings change. One explicit refresh (user action).</summary>
    public void ApplyRegionFilter()
    {
        try
        {
            _regionPref = new Infrastructure.Settings.AppSettingsStore().Load().PreferredRegion ?? "";
            foreach (var q in Quests)
                q.IsRegionMatch = RegionMatcher.IsMatch(q.RegionKind, q.RegionInclude, q.RegionExclude, _regionPref);
            QuestsView.Refresh();
            RefreshActionButtons();
            if (Quests.Count > 0 && !HasNoQuests)
            {
                var playable = VisibleQuests().Count(q => !q.IsCompleted && q.IsSpoofable);
                StatusMessage = playable > 0
                    ? $"{VisibleQuests().Count()} active quest(s) loaded ({playable} playable){RegionSuffix(Quests.Count)}"
                    : $"{VisibleQuests().Count()} active quest(s) loaded{RegionSuffix(Quests.Count)}";
            }
        }
        catch (Exception ex) { Debug.WriteLine($"ApplyRegionFilter failed: {ex.Message}"); }
    }

    private IEnumerable<QuestItem> VisibleQuests() => QuestsView.OfType<QuestItem>();

    public int CompletedCount { get; private set; }
    public int PendingCount { get; private set; }
    public bool HasCompletedToClaim => CompletedCount > 0;
    public string ClaimText => $"Claim · {CompletedCount} done, {PendingCount} left";

    private void RefreshActionButtons()
    {
        var visible = VisibleQuests().ToList();
        CanRunAll = !IsRunningAll && visible.Any(q => !q.IsCompleted && q.IsSpoofable);
        CanAutoVideos = !IsAutoRunning && visible.Any(q => !q.IsCompleted && q.IsVideoQuest);
        // Claim counts track play quests only (the spoofable ones).
        CompletedCount = Quests.Count(q => q.IsCompleted && q.IsSpoofable);
        PendingCount = Quests.Count(q => !q.IsCompleted && q.IsSpoofable);
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasCompletedToClaim));
        OnPropertyChanged(nameof(ClaimText));
    }

    [RelayCommand]
    private void Claim()
    {
        UrlLauncher.OpenDiscordQuestHome();
        StatusMessage = CompletedCount > 0
            ? $"Opened Discord — claim your {CompletedCount} completed quest(s)"
            : "Opened Discord quest home";
    }

    public event Action? OnQuestsLoaded;

    [RelayCommand]
    private void OpenQuestHome()
    {
        UrlLauncher.OpenDiscordQuestHome();
        StatusMessage = "Opened Discord quest home — accept the quest there to earn the reward";
    }

    [RelayCommand]
    private async Task SpoofAsync(QuestItem? quest)
    {
        if (quest == null) return;
        // Only desktop play is spoofable. Video / stream / activity quests
        // must be accepted and watched inside Discord (progress is reported
        // by the client to your account) — open quest home for those.
        if (!quest.IsSpoofable)
        {
            UrlLauncher.OpenDiscordQuestHome();
            StatusMessage = $"Opened Discord quests for: {quest.GameName} — accept & watch it there to earn the reward";
            return;
        }
        StatusMessage = $"Looking up game: {quest.GameName}...";
        var matches = _db.Games.Where(g => g.Id == quest.ApplicationId).ToList();
        if (matches.Count == 0)
            matches = _db.Games.Where(g => g.Name.Contains(quest.GameName, StringComparison.OrdinalIgnoreCase) || quest.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) { StatusMessage = $"No matching game found for: {quest.GameName}"; return; }

        var game = matches[0];
        if (DiscordDatabase.NeedsSteamSpoof(game, out var steamAppId))
        {
            StatusMessage = $"{quest.GameName} has no Discord executable — using Steam mode...";
            await _steamVm.SpoofInternalAsync(new SteamGameDisplayItem { Id = steamAppId, Name = quest.GameName }, quest.Id);
            StatusMessage = _steamVm.StatusMessage;
            if (_steamVm.StatusMessage.Contains("active")) ActiveSpoofQuestName = quest.GameName;
            return;
        }

        var exeName = DiscordDatabase.GetWin32Executable(game);
        if (exeName == null)
        {
            _dialogs.ShowInfo("No executable found", $"{quest.GameName} has no executable registered in Discord's database, so process spoofing won't work.", "Use Steam Quest mode or Manual mode to spoof this game.");
            StatusMessage = $"{quest.GameName} has no executable in Discord's database";
            return;
        }
        StatusMessage = $"Creating fake process for quest: {exeName}...";
        var path = _faker.CreateFakeGame(exeName);
        if (path != null && _faker.LaunchExecutable(path, out var proc, game.Name, quest.Id))
        {
            ActiveSpoofQuestName = quest.GameName;
            StatusMessage = $"Quest spoof active: {quest.GameName}";
            if (proc != null)
            {
                try
                {
                    proc.EnableRaisingEvents = true;
                    proc.Exited += (_, _) =>
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (proc.ExitCode != 0 && ActiveSpoofQuestName == quest.GameName)
                                {
                                    ActiveSpoofQuestName = null;
                                    StatusMessage = "Spoof cancelled";
                                    try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                                }
                                else if (ActiveSpoofQuestName == quest.GameName)
                                {
                                    // natural finish will be handled by watcher, but ensure status cleared if watcher missed
                                }
                            }
                            catch { }
                        });
                    };
                }
                catch { }
            }
        }
        else StatusMessage = $"Failed to launch spoof for: {quest.GameName}";
    }

    [RelayCommand]
    private Task ToggleCompletedAsync(QuestItem? quest)
    {
        if (quest == null) return Task.CompletedTask;
        var newValue = !quest.IsCompleted;
        quest.IsCompleted = newValue;
        var ids = Config.LoadCompletedQuestIds();
        if (newValue) ids.Add(quest.Id); else ids.Remove(quest.Id);
        Config.SaveCompletedQuestIds(ids);

        // Smooth fade only (DataTrigger 0.32s). No Move/Refresh/Reset -> no
        // disappearance or black flash. Reorder is deferred to next
        // LoadAsync (patch with Move) if needed, not instant.
        RefreshActionButtons();
        StatusMessage = newValue ? $"Marked \"{quest.GameName}\" as completed" : $"Marked \"{quest.GameName}\" as not completed";
        return Task.CompletedTask;
    }

    private string? RequireToken()
    {
        string token;
        try { token = new Infrastructure.Settings.AppSettingsStore().Load().DiscordToken?.Trim() ?? ""; }
        catch { token = ""; }
        if (string.IsNullOrEmpty(token))
        {
            _dialogs.ShowInfo("Token required",
                "Automation needs your Discord token to report progress to your account.",
                "Paste it in Settings → Official Discord API, then use Check token.");
            StatusMessage = "Set your Discord token in Settings first.";
            return null;
        }
        return token;
    }

    private CancellationTokenSource? _singleAutoCts;
    private QuestItem? _singleAutoQuest;

    [RelayCommand]
    private async Task AutoVideoAsync(QuestItem? quest)
    {
        if (quest == null || !quest.IsVideoQuest) return;
        // Re-click a running quest to stop it.
        if (quest.IsAutomating && ReferenceEquals(_singleAutoQuest, quest))
        {
            try { _singleAutoCts?.Cancel(); } catch { }
            return;
        }
        if (quest.IsAutomating) return;
        var token = RequireToken();
        if (token == null) return;
        if (quest.TaskSeconds <= 0)
        {
            StatusMessage = $"Unknown video length for: {quest.GameName} — watch it in Discord instead";
            return;
        }
        // One automation stream at a time — starting a single stops the bulk run.
        try { _autoCts?.Cancel(); } catch { }
        var cts = new CancellationTokenSource();
        _singleAutoCts = cts;
        _singleAutoQuest = quest;
        quest.IsAutomating = true;
        quest.AutoProgress = 0;
        RefreshActionButtons();
        try
        {
            var progress = new Progress<double>(done =>
                quest.AutoProgress = quest.TaskSeconds > 0 ? Math.Min(1, done / quest.TaskSeconds) : 0);
            var ok = await VideoQuestAutomator.RunAsync(token, quest.Id, quest.TaskType, quest.TaskSeconds, progress, cts.Token);
            if (ok)
            {
                MarkCompleted(quest);
                StatusMessage = $"Completed via automation: {quest.GameName} — claim it from the header Claim button when ready";
            }
            else StatusMessage = $"Stopped: {quest.GameName} — no progress was faked halfway";
        }
        catch (OperationCanceledException) { StatusMessage = $"Stopped: {quest.GameName}"; }
        catch (Exception ex) { StatusMessage = $"Automation failed for {quest.GameName}: {ex.Message}"; }
        finally
        {
            if (ReferenceEquals(_singleAutoCts, cts)) { _singleAutoCts = null; _singleAutoQuest = null; }
            try { cts.Dispose(); } catch { }
            quest.IsAutomating = false;
            quest.AutoProgress = 0;
            RefreshActionButtons();
        }
    }

    // Auto videos — AllowConcurrentExecutions so the same button stops the run
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AutoAllVideosAsync()
    {
        if (IsAutoRunning)
        {
            _autoCts?.Cancel();
            StatusMessage = "Stopping video automation after the current post…";
            return;
        }
        var token = RequireToken();
        if (token == null) return;
        var pending = VisibleQuests().Where(q => !q.IsCompleted && q.IsVideoQuest && q.TaskSeconds > 0).ToList();
        if (pending.Count == 0) { StatusMessage = "No video quests to automate."; return; }

        // Starting the bulk run stops any single run.
        try { _singleAutoCts?.Cancel(); } catch { }
        IsAutoRunning = true;
        var cts = new CancellationTokenSource();
        _autoCts = cts;
        CanAutoVideos = true;
        AutoVideosText = $"Running 0/{pending.Count} — click to stop";
        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var quest = pending[i];
                if (cts.Token.IsCancellationRequested) break;
                quest.IsAutomating = true;
                quest.AutoProgress = 0;
                StatusMessage = $"[{i + 1}/{pending.Count}] Auto-watching: {quest.GameName}…";
                AutoVideosText = $"Running {i + 1}/{pending.Count} — click to stop";
                var progress = new Progress<double>(done =>
                    quest.AutoProgress = quest.TaskSeconds > 0 ? Math.Min(1, done / quest.TaskSeconds) : 0);
                try
                {
                    if (await VideoQuestAutomator.RunAsync(token, quest.Id, quest.TaskType, quest.TaskSeconds, progress, cts.Token))
                    {
                        MarkCompleted(quest);
                        StatusMessage = $"[{i + 1}/{pending.Count}] Completed: {quest.GameName}";
                    }
                }
                catch (OperationCanceledException)
                {
                    StatusMessage = $"[{i + 1}/{pending.Count}] Stopped: {quest.GameName}";
                    break;
                }
                catch (Exception ex)
                {
                    StatusMessage = $"[{i + 1}/{pending.Count}] Failed: {quest.GameName} — {ex.Message}";
                    continue;
                }
                finally
                {
                    quest.IsAutomating = false;
                    quest.AutoProgress = 0;
                }
            }
        }
        finally
        {
            IsAutoRunning = false;
            if (ReferenceEquals(_autoCts, cts)) _autoCts = null;
            try { cts.Dispose(); } catch { }
            AutoVideosText = "Auto videos";
            RefreshActionButtons();
            StatusMessage = "Video automation finished — claim from the header Claim button when ready.";
            await LoadAsync();
        }
    }

    // Run All — AllowConcurrentExecutions so the same button can be used to Stop while running
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RunAllAsync()
    {
        if (IsRunningAll)
        {
            _runAllCts?.Cancel();
            StatusMessage = "Stopping quest sequence after the current one...";
            return;
        }
        var pending = VisibleQuests().Where(q => !q.IsCompleted && q.IsSpoofable).ToList();
        if (pending.Count == 0) { StatusMessage = "All quests are already completed."; return; }

        IsRunningAll = true;
        _runAllCts = new CancellationTokenSource();
        CanRunAll = true;
        RunAllText = $"Running 0/{pending.Count} — click to stop";
        var anyCompleted = false;

        try
        {
            for (int i = 0; i < pending.Count; i++)
            {
                var quest = pending[i];
                if (_runAllCts.Token.IsCancellationRequested) break;
                StatusMessage = $"[{i + 1}/{pending.Count}] Running quest: {quest.GameName} (15 min)...";
                RunAllText = $"Running {i + 1}/{pending.Count} — click to stop";

                var launched = await RunSingleAsync(quest);
                if (launched == null) { StatusMessage = $"[{i + 1}/{pending.Count}] Could not run: {quest.GameName} — skipping"; continue; }

                bool wasCancelled = false;
                if (launched.Value.process != null)
                {
                    try { await launched.Value.process.WaitForExitAsync(_runAllCts.Token); }
                    catch (OperationCanceledException)
                    {
                        try { launched.Value.process.Kill(true); } catch { }
                        // ensure cleanup even when cancelled via button
                        try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                        try { launched.Value.process.WaitForExit(2000); } catch { }
                        StatusMessage = $"[{i + 1}/{pending.Count}] Stopped: {quest.GameName}";
                        ActiveSpoofQuestName = null;
                        wasCancelled = true;
                        break;
                    }
                    // manual close detection via exit code 1
                    if (launched.Value.process.ExitCode != 0)
                    {
                        StatusMessage = $"[{i + 1}/{pending.Count}] Cancelled: {quest.GameName} — stopping sequence";
                        try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                        ActiveSpoofQuestName = null;
                        break;
                    }
                    // natural finish: also cleanup manifest immediately (don't wait for app close)
                    try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                }
                else
                {
                    await WaitForSpoofFinishAsync(_runAllCts.Token);
                    if (_runAllCts.Token.IsCancellationRequested) { wasCancelled = true; break; }
                    // steam mode without process: check if quest was marked completed (natural finish writes file)
                    var ids = Config.LoadCompletedQuestIds();
                    if (!ids.Contains(quest.Id))
                    {
                        StatusMessage = $"[{i + 1}/{pending.Count}] Cancelled: {quest.GameName} — stopping sequence";
                        try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                        break;
                    }
                    try { Services.SteamService.DeleteTrackedManifests(); } catch { }
                }
                if (wasCancelled) break;
                MarkCompleted(quest);
                anyCompleted = true;
                StatusMessage = $"[{i + 1}/{pending.Count}] Completed: {quest.GameName}";
            }
        }
        finally
        {
            IsRunningAll = false;
            _runAllCts?.Dispose();
            _runAllCts = null;
            RunAllText = "Run all quests";
            RefreshActionButtons();
            StatusMessage = anyCompleted
                ? "Quest sequence finished — claim from the header Claim button when ready."
                : "Quest sequence finished.";
            if (anyCompleted) await LoadAsync();
        }
    }

    private async Task<(bool ok, System.Diagnostics.Process? process)?> RunSingleAsync(QuestItem quest)
    {
        try
        {
            var matches = _db.Games.Where(g => g.Id == quest.ApplicationId).ToList();
            if (matches.Count == 0)
                matches = _db.Games.Where(g => g.Name.Contains(quest.GameName, StringComparison.OrdinalIgnoreCase) || quest.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0) return (false, null);
            var game = matches[0];
            if (DiscordDatabase.NeedsSteamSpoof(game, out var steamAppId))
            {
                await _steamVm.SpoofInternalAsync(new SteamGameDisplayItem { Id = steamAppId, Name = quest.GameName }, quest.Id);
                return (true, null);
            }
            var exeName = DiscordDatabase.GetWin32Executable(game);
            if (exeName == null) return (false, null);
            var path = _faker.CreateFakeGame(exeName);
            if (path == null) return (false, null);
            if (_faker.LaunchExecutable(path, out var proc, game.Name, quest.Id)) return (true, proc);
            return (false, null);
        }
        catch { return (false, null); }
    }

    private async Task WaitForSpoofFinishAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(5000, ct); } catch (TaskCanceledException) { break; }
            if (ct.IsCancellationRequested) break;
            var alive = System.Diagnostics.Process.GetProcessesByName("OrbSpoofer").Any(p => { try { return p.MainWindowTitle.Contains("Timer", StringComparison.OrdinalIgnoreCase); } catch { return false; } });
            if (!alive) break;
        }
    }

    private void MarkCompleted(QuestItem quest)
    {
        try { var ids = Config.LoadCompletedQuestIds(); ids.Add(quest.Id); Config.SaveCompletedQuestIds(ids); quest.IsCompleted = true; } catch { }
    }

    public void DisposeWatcher() => _watcher?.Dispose();
}
