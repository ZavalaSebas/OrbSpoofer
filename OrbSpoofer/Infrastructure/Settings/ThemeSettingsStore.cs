namespace OrbSpoofer.Infrastructure.Settings;

public sealed class ThemeSettings
{
    public string AccentHex { get; set; } = "#5865F2";
}

public sealed class ThemeSettingsStore : SettingsStoreBase<ThemeSettings>
{
    protected override string CurrentFilePath => System.IO.Path.Combine(Config.AppDataPath, "theme.json");
    protected override ThemeSettings DefaultValue => new();
}
