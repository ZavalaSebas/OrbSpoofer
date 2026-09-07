namespace OrbSpoofer.Infrastructure.Settings;

public sealed class KnownQuestsStore : SettingsStoreBase<HashSet<string>>
{
    protected override string CurrentFilePath => System.IO.Path.Combine(Config.AppDataPath, "watcher_known.json");
    protected override HashSet<string> DefaultValue => [];
}
