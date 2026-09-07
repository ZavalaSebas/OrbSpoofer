using System.Diagnostics;
using System.Windows.Threading;
using OrbSpoofer.Infrastructure.Settings;
using OrbSpoofer.Models;

namespace OrbSpoofer.Services;

/// <summary>
/// Polls the quest list while the app is open and reports fresh arrivals.
/// First tick seeds the known set silently (no flood on startup).
/// Runs on the UI dispatcher so ViewModels can update directly.
/// </summary>
public static class QuestWatcher
{
    private static DispatcherTimer? _timer;
    private static bool _seeded;

    public static event Action<List<QuestItem>>? NewQuestsFound;

    public static bool IsRunning => _timer?.IsEnabled == true;

    public static void Restart()
    {
        Stop();
        _seeded = false;
        AppSettings settings;
        try { settings = new AppSettingsStore().Load(); }
        catch { return; }
        if (!settings.NotifyNewQuests) return;
        var minutes = settings.PollIntervalMinutes is >= 5 and <= 480 ? settings.PollIntervalMinutes : 30;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
        _timer.Tick += async (_, _) => await CheckAsync();
        _timer.Start();
    }

    public static void Stop()
    {
        try { _timer?.Stop(); } catch { }
        _timer = null;
    }

    /// <summary>Pure diff: fetched IDs not in the known set.</summary>
    public static List<QuestItem> DiffNewQuests(HashSet<string> knownIds, List<QuestItem> fetched, bool orbsOnly)
    {
        var fresh = new List<QuestItem>();
        foreach (var q in fetched)
        {
            if (!knownIds.Contains(q.Id)) { knownIds.Add(q.Id); fresh.Add(q); }
        }
        if (orbsOnly)
            fresh.RemoveAll(q => !q.Reward.Contains("orb", StringComparison.OrdinalIgnoreCase));
        return fresh;
    }

    private static async Task CheckAsync()
    {
        try
        {
            var store = new AppSettingsStore();
            var settings = store.Load();
            List<QuestItem> fetched;
            if (settings.UseOfficialApi && !string.IsNullOrWhiteSpace(settings.DiscordToken))
            {
                try { fetched = await QuestService.GetOfficialQuestsAsync(settings.DiscordToken.Trim()); }
                catch { fetched = await QuestService.GetActivePlayQuestsAsync(); }
            }
            else
            {
                fetched = await QuestService.GetActivePlayQuestsAsync();
            }

            var knownStore = new KnownQuestsStore();
            var known = knownStore.Load();
            if (!_seeded)
            {
                // First tick after (re)start: learn the current list, don't notify.
                foreach (var q in fetched) known.Add(q.Id);
                try { knownStore.Save(known); } catch { }
                _seeded = true;
                return;
            }

            var fresh = DiffNewQuests(known, fetched, settings.NotifyOrbsOnly);
            try { knownStore.Save(known); } catch { }
            if (fresh.Count > 0)
            {
                try { NewQuestsFound?.Invoke(fresh); } catch (Exception ex) { Debug.WriteLine($"Watcher notify failed: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Watcher check failed: {ex.Message}"); }
    }
}
