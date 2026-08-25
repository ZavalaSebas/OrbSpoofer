using System.IO;

namespace OrbSpoofer.Infrastructure.AppData;

public static class AppDataMigrator
{
    public const int LatestVersion = 1;
    private static readonly Action<AppDataMigrationContext>[] Steps = [AppDataMigrations.V1_InitializeLayout];

    public static void MigrateToLatest(string? appDataPath = null)
    {
        var root = appDataPath ?? Config.AppDataPath;
        Directory.CreateDirectory(root);
        var ctx = new AppDataMigrationContext(root);
        var current = ReadVersion(root);
        for (var step = current; step < LatestVersion; step++)
        {
            Steps[step](ctx);
            WriteVersion(root, step + 1);
        }
    }

    internal static int ReadVersion(string root)
    {
        var p = Path.Combine(root, "appdata.version");
        if (!File.Exists(p)) return 0;
        try { var t = File.ReadAllText(p).Trim(); return int.TryParse(t, out var v) && v >= 0 ? v : 0; } catch { return 0; }
    }

    internal static void WriteVersion(string root, int version) => File.WriteAllText(Path.Combine(root, "appdata.version"), version.ToString());
}
