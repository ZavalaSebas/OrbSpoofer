using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace OrbSpoofer.Tests;

/// <summary>
/// Guards against Color/Brush DynamicResource mix-ups, which crash at render
/// (e.g. InvalidOperationException: '#FF1E1E22' is not valid for 'Color').
/// </summary>
public class XamlResourceAuditTests
{
    private static readonly Regex DynRes = new(@"\{DynamicResource ([^}]+)\}", RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OrbSpoofer.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        Assert.Fail("Repo root (OrbSpoofer.slnx) not found from " + AppContext.BaseDirectory);
        throw new InvalidOperationException("unreachable");
    }

    private static IEnumerable<(string File, string Attr, string Key)> DynamicUsages()
    {
        var root = Path.Combine(FindRepoRoot(), "OrbSpoofer");
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch { continue; }
            foreach (var el in doc.Descendants())
            {
                foreach (var attr in el.Attributes())
                {
                    var m = DynRes.Match(attr.Value);
                    if (m.Success)
                        yield return (Path.GetFileName(file), attr.Name.LocalName, m.Groups[1].Value.Trim());
                }
            }
        }
    }

    [Fact]
    public void ColorProperties_NeverBindToBrushKeys()
    {
        var bad = DynamicUsages()
            .Where(u => u.Attr == "Color" && u.Key.EndsWith("Brush"))
            .Select(u => $"{u.File}: Color -> {u.Key}")
            .Distinct()
            .ToList();
        Assert.True(bad.Count == 0, "Color bound to Brush key(s):\n" + string.Join("\n", bad));
    }

    [Fact]
    public void BrushProperties_NeverBindToBareColorKeys()
    {
        string[] brushAttrs = ["Foreground", "Background", "BorderBrush", "Fill", "Stroke"];
        var bad = DynamicUsages()
            .Where(u => brushAttrs.Contains(u.Attr)
                && u.Key.EndsWith("Color")
                && !u.Key.Contains("Brush"))
            .Select(u => $"{u.File}: {u.Attr} -> {u.Key}")
            .Distinct()
            .ToList();
        Assert.True(bad.Count == 0, "Brush property bound to Color key(s):\n" + string.Join("\n", bad));
    }
}
