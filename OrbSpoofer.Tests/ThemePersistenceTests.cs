using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class ThemePersistenceTests
{
    [Fact]
    public void ApplyTheme_Persists_And_NoArgRespectsSaved()
    {
        Exception? error = null;
        string? afterExplicitLight = null;
        string? afterNoArg = null;
        string? currentAfterNoArg = null;
        var t = new Thread(() =>
        {
            System.Windows.Application? app = null;
            // Single Application per AppDomain — one scenario after another.
            try
            {
                var path = Path.Combine(Config.AppDataPath, "theme.json");
                var original = File.Exists(path) ? File.ReadAllText(path) : null;
                try
                {
                    app = new System.Windows.Application();

                    // 1. Explicit Light persists to disk.
                    ThemeManager.ApplyTheme("Light");
                    Assert.Equal("Light", ThemeManager.CurrentTheme);
                    Assert.Equal("Light", ThemeManager.LoadSavedTheme());
                    afterExplicitLight = File.ReadAllText(path);

                    // 2. Regression: no-arg ApplyTheme must respect the saved
                    // value (it used to default to Dark, wiping Light on startup).
                    File.WriteAllText(path, "{\"AccentHex\":\"#5865F2\",\"Theme\":\"Light\"}");
                    ThemeManager.ApplyTheme();
                    currentAfterNoArg = ThemeManager.CurrentTheme;
                    afterNoArg = File.ReadAllText(path);
                }
                finally
                {
                    if (original != null) File.WriteAllText(path, original);
                    app?.Shutdown();
                }
            }
            catch (Exception ex) { error = ex; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        Assert.True(t.Join(TimeSpan.FromSeconds(30)), "STA thread timed out");
        Assert.Null(error);
        Assert.NotNull(afterExplicitLight);
        Assert.Contains("\"Theme\":\"Light\"", afterExplicitLight);
        Assert.Equal("Light", currentAfterNoArg);
        Assert.NotNull(afterNoArg);
        Assert.Contains("\"Theme\":\"Light\"", afterNoArg);
    }
}
