using System.Windows.Media;

namespace OrbSpoofer.Services;

/// <summary>Minimal ThemeManager — mirrors Bridge/Services/ThemeManager.cs for Fluent accent persistence.</summary>
public static class ThemeManager
{
    private const string DefaultAccent = "#5865F2";

    public static void ApplyAccent(string? hex = null)
    {
        var colorHex = hex ?? DefaultAccent;
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            var app = System.Windows.Application.Current;
            if (app == null) return;
            void Replace(string key, Color c)
            {
                if (app.Resources.Contains(key))
                    app.Resources[key] = c;
                var brushKey = key + "Brush";
                if (app.Resources.Contains(brushKey))
                    app.Resources[brushKey] = new SolidColorBrush(c);
            }
            Replace("SystemAccentColorPrimary", color);
            Replace("PrimaryColor", color);
            // keep Orb tokens in sync
            if (app.Resources.Contains("Orb.Accent.Primary.Color"))
                app.Resources["Orb.Accent.Primary.Color"] = color;
            if (app.Resources.Contains("Orb.SystemAccentBrush"))
                app.Resources["Orb.SystemAccentBrush"] = new SolidColorBrush(color);
        }
        catch { }
    }
}
