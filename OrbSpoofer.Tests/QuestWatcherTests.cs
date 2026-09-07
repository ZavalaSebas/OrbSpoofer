using OrbSpoofer.Models;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class QuestWatcherTests
{
    private static QuestItem Item(string id, string reward = "200 Orbs") => new() { Id = id, Reward = reward };

    [Fact]
    public void DiffNewQuests_ReturnsOnlyUnknown_AndRemembersThem()
    {
        var known = new HashSet<string> { "a" };
        var fresh = QuestWatcher.DiffNewQuests(known, [Item("a"), Item("b"), Item("c")], orbsOnly: false);
        Assert.Equal(["b", "c"], fresh.Select(q => q.Id).Order());
        // Second pass with same list: nothing new.
        Assert.Empty(QuestWatcher.DiffNewQuests(known, [Item("a"), Item("b"), Item("c")], orbsOnly: false));
    }

    [Fact]
    public void DiffNewQuests_OrbsOnly_FiltersNonOrbRewards()
    {
        var known = new HashSet<string>();
        var fresh = QuestWatcher.DiffNewQuests(known,
            [Item("a", "200 Orbs"), Item("b", "Avatar Decoration"), Item("c", "700 ORBS")],
            orbsOnly: true);
        Assert.Equal(["a", "c"], fresh.Select(q => q.Id).Order());
        // Non-orb quest was still marked known (not re-reported later).
        Assert.Contains("b", known);
    }

    [Fact]
    public void DiffNewQuests_EmptyFetch_ReturnsEmpty()
    {
        Assert.Empty(QuestWatcher.DiffNewQuests([], [], orbsOnly: true));
    }
}
