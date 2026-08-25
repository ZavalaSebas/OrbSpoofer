using System.IO;

namespace OrbSpoofer.Infrastructure.Settings;

public sealed class CreatedManifestsStore : SettingsStoreBase<HashSet<int>>
{
    protected override string CurrentFilePath => Path.Combine(Config.AppDataPath, "created-manifests.json");
    protected override HashSet<int> DefaultValue => [];
}
