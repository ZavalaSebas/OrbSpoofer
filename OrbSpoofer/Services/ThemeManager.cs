using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OrbSpoofer.Services;

/// <summary>ThemeManager — accent + Mica, with live refresh (Bridge parity lite).</summary>
public static class ThemeManager
{
    private const string DefaultAccent = "#5865F2";
    private static readonly Infrastructure.Settings.ThemeSettingsStore Store = new();

    public static string LoadSavedAccent()
    {
        try { return Store.Load().AccentHex ?? DefaultAccent; } catch { return DefaultAccent; }
    }

    public static void SaveAccent(string hex)
    {
        try { Store.Save(new Infrastructure.Settings.ThemeSettings { AccentHex = hex }); } catch { }
    }

    public static void ApplyAccent(string? hex = null)
    {
        hex ??= LoadSavedAccent();
        var colorHex = hex ?? DefaultAccent;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var app = Application.Current;
            if (app == null) return;
            void Replace(string key, Color c)
            {
                if (app.Resources.Contains(key)) app.Resources[key] = c;
                var brushKey = key + "Brush";
                if (app.Resources.Contains(brushKey))
                {
                    var brush = new SolidColorBrush(c);
                    if (brush.CanFreeze) brush.Freeze();
                    app.Resources[brushKey] = brush;
                }
            }
            Replace("SystemAccentColorPrimary", color);
            Replace("PrimaryColor", color);
            if (app.Resources.Contains("Orb.Accent.Primary.Color")) app.Resources["Orb.Accent.Primary.Color"] = color;
            if (app.Resources.Contains("Orb.SystemAccentBrush"))
            {
                var b = new SolidColorBrush(color);
                if (b.CanFreeze) b.Freeze();
                app.Resources["Orb.SystemAccentBrush"] = b;
            }
            // also update secondary derived
            try
            {
                var secondary = Color.FromRgb((byte)Math.Min(255, color.R + 30), (byte)Math.Min(255, color.G + 20), (byte)Math.Min(255, color.B + 10));
                if (app.Resources.Contains("SystemAccentColorSecondary")) app.Resources["SystemAccentColorSecondary"] = secondary;
                if (app.Resources.Contains("Orb.Accent.Secondary.Color")) app.Resources["Orb.Accent.Secondary.Color"] = secondary;
            }
            catch { }
            foreach (Window w in app.Windows) RefreshWindow(w);
            // persist
            try { SaveAccent(colorHex); } catch { }
        }
        catch { }
    }

    public static void RefreshWindow(Window window)
    {
        try
        {
            // Force DynamicResource re-evaluation by toggling a dummy resource (Bridge technique lite)
            var dummy = window.Resources.Contains("Orb.RefreshTick") ? (int)window.Resources["Orb.RefreshTick"] : 0;
            window.Resources["Orb.RefreshTick"] = dummy + 1;
        }
        catch { }
    }
}
