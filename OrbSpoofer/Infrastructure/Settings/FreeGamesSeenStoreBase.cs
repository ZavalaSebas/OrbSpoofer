using System.IO;

namespace OrbSpoofer.Infrastructure.Settings;

public sealed class FreeGamesSeenStoreTyped : SettingsStoreBase<HashSet<int>>
{
    protected override string CurrentFilePath => Path.Combine(Config.AppDataPath, "free-games-seen.json");
    protected override HashSet<int> DefaultValue => [];
}
