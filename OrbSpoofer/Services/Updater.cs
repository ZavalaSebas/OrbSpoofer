using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace OrbSpoofer.Services;

public static class Updater
{
    public static bool IsFrozen()
    {
        return Environment.ProcessPath != null && File.Exists(Environment.ProcessPath);
    }

    public static async Task<(bool needsUpdate, string? tagName, string? downloadUrl)> CheckForUpdateAsync()
    {
        try
        {
            var url = $"https://api.github.com/repos/{Config.GitHubRepoOwner}/{Config.GitHubRepoName}/releases/latest";
            var json = await NetworkHelper.FetchJsonAsync(url);

            var tag = json.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(tag)) return (false, null, null);

            if (!Version.TryParse(tag.TrimStart('v'), out var remoteVersion))
                return (false, null, null);
            if (!Version.TryParse(Config.Version, out var localVersion))
                return (false, null, null);

            if (remoteVersion <= localVersion)
                return (false, null, null);

            string? downloadUrl = null;
            if (json.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var dl)
                            ? dl.GetString() : null;
                        break;
                    }
                }
            }

            return (true, tag, downloadUrl);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
            return (false, null, null);
        }
    }

    public static async Task<bool> DownloadAndApplyUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
    {
        var currentExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(currentExe)) return false;

        var tempExe = Path.Combine(Path.GetTempPath(), $"OrbSpoofer_update_{Guid.NewGuid()}.exe");
        var oldExe = currentExe + ".old";

        try
        {
            await NetworkHelper.DownloadFileAsync(downloadUrl, tempExe, progress);

            if (File.Exists(oldExe))
            {
                try { File.Delete(oldExe); }
                catch (Exception ex) { Debug.WriteLine($"Failed to delete old exe: {ex.Message}"); }
            }

            File.Move(currentExe, oldExe);
            File.Move(tempExe, currentExe);

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true,
            });

            if (process == null)
            {
                Debug.WriteLine("Failed to start updated process");
                if (File.Exists(oldExe) && File.Exists(currentExe))
                {
                    try { File.Delete(currentExe); File.Move(oldExe, currentExe); }
                    catch (Exception rollbackEx) { Debug.WriteLine($"Rollback failed: {rollbackEx.Message}"); }
                }
                return false;
            }

            process.Dispose();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update failed: {ex.Message}");
            if (File.Exists(oldExe) && !File.Exists(currentExe))
            {
                try { File.Move(oldExe, currentExe); }
                catch (Exception rollbackEx) { Debug.WriteLine($"Rollback failed: {rollbackEx.Message}"); }
            }
            return false;
        }

        return false;
    }
}
