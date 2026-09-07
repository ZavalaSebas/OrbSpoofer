namespace OrbSpoofer.Services;

/// <summary>Pure region-matching rules (unit-testable).</summary>
public static class RegionMatcher
{
    public static bool IsMatch(string kind, IEnumerable<string> include, IEnumerable<string> exclude, string preferredRegion)
    {
        if (string.IsNullOrWhiteSpace(preferredRegion)) return true;
        var pref = preferredRegion.Trim().ToUpperInvariant();
        if (kind == "Include") return include.Any(c => string.Equals(c, pref, StringComparison.OrdinalIgnoreCase));
        if (kind == "Exclude") return !exclude.Any(c => string.Equals(c, pref, StringComparison.OrdinalIgnoreCase));
        return true; // Global and anything unknown
    }
}
