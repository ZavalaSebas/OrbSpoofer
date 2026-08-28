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
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string? _activeSpoofQuestName;

    private CancellationTokenSource? _runAllCts;
    private FileSystemWatcher? _watcher;
    public ICollectionView QuestsView { get; }

    public QuestsViewModel(DiscordDatabase db, GameFaker faker, SteamSearchViewModel steamVm, IDialogService dialogs)
    {
        _db = db;
        _faker = faker;
        _steamVm = steamVm;
        _dialogs = dialogs;
        QuestsView = CollectionViewSource.GetDefaultView(Quests);
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
                if (ActiveSpoofQuestName is not null)
                {
                    StatusMessage = $"Quest completed: {ActiveSpoofQuestName}";
                    ActiveSpoofQuestName = null;
                }
                else StatusMessage = "Quest completed";

                try { Services.SteamService.DeleteTrackedManifests(); } catch { }

                // if currently visible, reload
                _ = LoadAsync();
            });
        }
        catch (Exception ex) { Debug.WriteLine($"OnCompletedChanged failed: {ex.Message}"); }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        bool isInitial = Quests.Count == 0;
        if (isInitial) IsLoading = true;
        // don't touch HasNoQuests until we have data -> avoids black toggle
        try
        {
            var all = await QuestService.GetActivePlayQuestsAsync();
            var spoofableIds = new HashSet<string>(_db.Games.Where(g => DiscordDatabase.GetWin32Executable(g) != null).Select(g => g.Id));
            var completedIds = Config.LoadCompletedQuestIds();

            var filtered = all.Where(q =>
            {
                if (spoofableIds.Contains(q.ApplicationId ?? "")) return true;
                return _db.Games.Any(g => g.Name.Contains(q.GameName, StringComparison.OrdinalIgnoreCase) || q.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase));
            }).ToList();

            foreach (var q in filtered)
            {
                q.IsCompleted = completedIds.Contains(q.Id);
                var matching = _db.Games.FirstOrDefault(g => g.Id == q.ApplicationId || g.Name.Contains(q.GameName, StringComparison.OrdinalIgnoreCase) || q.GameName.Contains(g.Name, StringComparison.OrdinalIgnoreCase));
                q.NeedsSteamMode = matching != null && DiscordDatabase.GetWin32Executable(matching) == null;
            }

            var sorted = filtered.OrderBy(q => q.IsCompleted).ThenBy(q => q.ExpiresAt).ToList();

            if (sorted.Count == 0)
            {
                // clear without animation if already empty
                if (Quests.Count != 0) Quests.Clear();
                EmptyText = "No active quests found. Try Search to spoof a game manually.";
                HasNoQuests = true;
                CanRunAll = false;
            }
            else
            {
                if (isInitial)
                {
                    Quests.Clear();
                    foreach (var q in sorted) Quests.Add(q);
                    HasNoQuests = false;
                    CanRunAll = !IsRunningAll && sorted.Any(q => !q.IsCompleted);
                    StatusMessage = $"{sorted.Count} active quest(s) loaded";
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
                            int cur = Quests.IndexOf(existing);
                            if (cur != i) Quests.Move(cur, i);
                        }
                        else
                        {
                            Quests.Insert(i, src);
                        }
                    }
                    HasNoQuests = false;
                    CanRunAll = !IsRunningAll && Quests.Any(q => !q.IsCompleted);
                    StatusMessage = $"{sorted.Count} active quest(s) loaded";
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
            StatusMessage = EmptyText;
        }
        finally { IsLoading = false; }
    }

    public event Action? OnQuestsLoaded;

    [RelayCommand]
    private async Task SpoofAsync(QuestItem? quest)
    {
        if (quest == null) return;
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
        CanRunAll = !IsRunningAll && Quests.Any(q => !q.IsCompleted);
        StatusMessage = newValue ? $"Marked \"{quest.GameName}\" as completed" : $"Marked \"{quest.GameName}\" as not completed";
        return Task.CompletedTask;
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
        var pending = Quests.Where(q => !q.IsCompleted).ToList();
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
            CanRunAll = Quests.Any(q => !q.IsCompleted);
            StatusMessage = "Quest sequence finished.";
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
