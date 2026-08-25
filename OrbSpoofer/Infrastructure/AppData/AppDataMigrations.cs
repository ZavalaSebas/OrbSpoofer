namespace OrbSpoofer.Infrastructure.AppData;

public static class AppDataMigrations
{
    public static void V1_InitializeLayout(AppDataMigrationContext ctx)
    {
        ctx.EnsureDirectory();
        // Ensure subdirs for caches (steam_search)
        ctx.EnsureDirectory(Config.SteamSearchCacheDir);
        // Clean legacy files if any (example: old welcome flag location)
        ctx.DeleteFileIfExists("orb_spoofer.log");
    }
}
