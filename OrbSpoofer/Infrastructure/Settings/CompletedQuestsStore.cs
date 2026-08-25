using System.IO;

namespace OrbSpoofer.Infrastructure.Settings;

public sealed class CompletedQuestsStore : SettingsStoreBase<HashSet<string>>
{
    protected override string CurrentFilePath => Path.Combine(Config.AppDataPath, Config.CompletedQuestsFile);
    protected override HashSet<string> DefaultValue => [];
}
