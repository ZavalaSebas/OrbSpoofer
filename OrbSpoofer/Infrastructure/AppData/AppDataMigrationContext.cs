using System.IO;

namespace OrbSpoofer.Infrastructure.AppData;

public sealed class AppDataMigrationContext
{
    public AppDataMigrationContext(string root) { ArgumentException.ThrowIfNullOrWhiteSpace(root); Root = root; }
    public string Root { get; }
    public string Combine(params string[] segments) => Path.Combine([Root, .. segments]);
    public void EnsureDirectory(params string[] segments) => Directory.CreateDirectory(Combine(segments));
    public void DeleteFileIfExists(params string[] segments) { var p = Combine(segments); if (File.Exists(p)) File.Delete(p); }
}
