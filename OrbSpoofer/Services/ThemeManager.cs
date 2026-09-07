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
        try { var s = Store.Load(); s.AccentHex = hex; Store.Save(s); } catch { }
    }

    public static string CurrentTheme { get; private set; } = "Dark";

    public static string LoadSavedTheme()
    {
        try
        {
            var t = Store.Load().Theme;
            return string.Equals(t, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        }
        catch { return "Dark"; }
    }

    /// <summary>Swaps the Dark/Light palette dictionary, syncs the WPF-UI theme, and re-applies the accent.</summary>
    public static void ApplyTheme(string? theme = null)
    {
        theme ??= LoadSavedTheme();
        theme = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
        try
        {
            var app = Application.Current;
            if (app == null) return;
            var merged = app.Resources.MergedDictionaries;
            ResourceDictionary? slot = null;
            foreach (var d in merged)
            {
                var src = d.Source?.OriginalString ?? "";
                if (src.Contains("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                    src.Contains("LightTheme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    slot = d;
                    break;
                }
            }
            var next = new ResourceDictionary { Source = new Uri($"/OrbSpoofer;component/Themes/{theme}Theme.xaml", UriKind.Relative) };
            if (slot != null) merged[merged.IndexOf(slot)] = next;
            else merged.Add(next);
            try { Wpf.Ui.Appearance.ApplicationThemeManager.Apply(theme == "Light" ? Wpf.Ui.Appearance.ApplicationTheme.Light : Wpf.Ui.Appearance.ApplicationTheme.Dark); } catch { }
            CurrentTheme = theme;
            try { var s = Store.Load(); s.Theme = theme; Store.Save(s); } catch { }
            ApplyAccent();
            foreach (Window w in app.Windows) RefreshWindow(w);
        }
        catch { }
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
            // Unconditional indexer assignment: Application-level entries shadow the
            // merged-dictionary defaults and always raise change notifications, so
            // every DynamicResource binding updates live. (ResourceDictionary.Contains
            // does not see merged-dictionary keys, so guarded updates silently skip.)
            void Replace(string key, Color c)
            {
                app.Resources[key] = c;
                SetBrush(key + "Brush", c);
            }
            void SetBrush(string brushKey, Color c)
            {
                var brush = new SolidColorBrush(c);
                if (brush.CanFreeze) brush.Freeze();
                app.Resources[brushKey] = brush;
            }
            var secondary = Lighten(color, 0.28);
            var tertiary = Darken(color, 0.30);
            Replace("SystemAccentColorPrimary", color);
            Replace("SystemAccentColorSecondary", secondary);
            Replace("SystemAccentColorTertiary", tertiary);
            // Alpha variants for glow gradients (same RGB, fixed alpha)
            app.Resources["SystemAccentColorPrimaryFaint"] = Color.FromArgb(0x18, color.R, color.G, color.B);
            app.Resources["SystemAccentColorPrimaryTransparent"] = Color.FromArgb(0x00, color.R, color.G, color.B);
            app.Resources["SystemAccentColorSecondaryFaint"] = Color.FromArgb(0x18, secondary.R, secondary.G, secondary.B);
            app.Resources["SystemAccentColorSecondaryTransparent"] = Color.FromArgb(0x00, secondary.R, secondary.G, secondary.B);
            Replace("PrimaryColor", color);
            Replace("SecondaryColor", secondary);
            app.Resources["Orb.Accent.Primary.Color"] = color;
            app.Resources["Orb.Accent.Secondary.Color"] = secondary;
            // Brushes are replaced (not mutated) so every DynamicResource binding picks the new accent.
            // NOTE: this must cover every accent-derived brush key — Replace(key) only handles
            // "<key>Brush", but DarkTheme names the main brush "PrimaryBrush", not "PrimaryColorBrush".
            SetBrush("Orb.SystemAccentBrush", color);
            SetBrush("PrimaryBrush", color);
            SetBrush("SecondaryBrush", secondary);
            SetBrush("SystemAccentColorPrimaryBrush", color);
            SetBrush("SystemAccentColorSecondaryBrush", secondary);
            SetBrush("SystemAccentColorTertiaryBrush", tertiary);
            SetBrush("Orb.Accent.SecondaryBrush", secondary);
            SetBrush("AccentFillColorDefaultBrush", color);
            SetBrush("AccentFillColorSecondaryBrush", color);
            SetBrush("AccentFillColorTertiaryBrush", color);
            foreach (Window w in app.Windows) RefreshWindow(w);
            // persist
            try { SaveAccent(colorHex); } catch { }
        }
        catch { }
    }

    public static Color Lighten(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)(c.R + (255 - c.R) * amount),
            (byte)(c.G + (255 - c.G) * amount),
            (byte)(c.B + (255 - c.B) * amount));
    }

    public static Color Darken(Color c, double amount)
    {
        return Color.FromRgb(
            (byte)(c.R * (1 - amount)),
            (byte)(c.G * (1 - amount)),
            (byte)(c.B * (1 - amount)));
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
