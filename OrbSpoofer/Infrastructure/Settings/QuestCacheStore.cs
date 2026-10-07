using OrbSpoofer.Models;

namespace OrbSpoofer.Infrastructure.Settings;

public sealed class QuestCacheStore : SettingsStoreBase<QuestCacheData>
{
    protected override string CurrentFilePath => System.IO.Path.Combine(Config.AppDataPath, "quests_cache.json");
    protected override QuestCacheData DefaultValue => new(DateTime.MinValue, "", "", []);
}
