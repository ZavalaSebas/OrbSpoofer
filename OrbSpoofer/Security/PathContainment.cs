using System.IO;

namespace OrbSpoofer.Security;

/// <summary>
/// Validates that resolved file paths stay inside an expected root directory.
/// Use when combining external input with local filesystem paths.
/// </summary>
public static class PathContainment
{
    public static string? TryResolveUnderRoot(string rootDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        if (Path.IsPathRooted(relativePath))
            return null;

        if (relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is ".."))
        {
            return null;
        }

        var rootFull = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var candidate = Path.GetFullPath(Path.Combine(rootFull, relativePath));

        return IsUnderRoot(candidate, rootFull) ? candidate : null;
    }

    public static bool IsUnderRoot(string filePath, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(rootDirectory))
            return false;

        var pathFull = Path.GetFullPath(filePath);
        var rootFull = Path.GetFullPath(rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        if (pathFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            return false;

        return pathFull.Length > rootFull.Length &&
               pathFull[rootFull.Length] is ('\\' or '/');
    }
}

