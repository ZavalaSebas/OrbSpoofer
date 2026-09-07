using System.Windows.Media;
using OrbSpoofer.Infrastructure.Settings;
using OrbSpoofer.Services;

namespace OrbSpoofer.Tests;

public class ThemeTests
{
    [Fact]
    public void ThemeSettings_DefaultsToDarkBlurple()
    {
        var s = new ThemeSettings();
        Assert.Equal("Dark", s.Theme);
        Assert.Equal("#5865F2", s.AccentHex);
    }

    [Fact]
    public void Lighten_MovesTowardWhite()
    {
        var c = (Color)ColorConverter.ConvertFromString("#5865F2");
        var lighter = ThemeManager.Lighten(c, 0.28);
        Assert.True(lighter.R >= c.R && lighter.G >= c.G && lighter.B >= c.B);
        Assert.Equal(Color.FromRgb(134, 144, 245), lighter);
    }

    [Fact]
    public void Darken_MovesTowardBlack()
    {
        var c = (Color)ColorConverter.ConvertFromString("#5865F2");
        var darker = ThemeManager.Darken(c, 0.30);
        Assert.True(darker.R <= c.R && darker.G <= c.G && darker.B <= c.B);
        Assert.Equal(Color.FromRgb(61, 70, 169), darker);
    }

    [Fact]
    public void Lighten_Darken_Boundaries()
    {
        var c = (Color)ColorConverter.ConvertFromString("#123456");
        Assert.Equal(Color.FromRgb(255, 255, 255), ThemeManager.Lighten(c, 1.0));
        Assert.Equal(Color.FromRgb(0, 0, 0), ThemeManager.Darken(c, 1.0));
        Assert.Equal(c, ThemeManager.Lighten(c, 0.0));
        Assert.Equal(c, ThemeManager.Darken(c, 0.0));
    }
}
