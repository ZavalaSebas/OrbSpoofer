using System.Diagnostics;
using System.IO;

namespace OrbSpoofer.Services;

/// <summary>
/// Restarts Steam and Discord so they pick up a freshly written fake appmanifest.
/// Discord scans the Steam library when it starts, so a manifest created while it
/// is already running is invisible until both apps are reopened. This is the
/// difference between a Steam quest that shows presence and one that makes progress.
/// </summary>
public static class AppRestarter
{
    private static readonly (string Process, string Folder, string Exe)[] DiscordClients =
    [
        ("Discord", "Discord", "Discord.exe"),
        ("DiscordPTB", "DiscordPTB", "DiscordPTB.exe"),
        ("DiscordCanary", "DiscordCanary", "DiscordCanary.exe"),
    ];

    public static bool IsSteamRunning() => Process.GetProcessesByName("steam").Length > 0;

    public static bool IsDiscordRunning() =>
        DiscordClients.Any(client => Process.GetProcessesByName(client.Process).Length > 0);

    /// <summary>Restarts Steam then Discord. Returns which of them came back up.
    /// Never throws; failures leave the caller free to continue without a restart.</summary>
    public static async Task<(bool steam, bool discord)> RestartAsync(IProgress<string>? progress = null)
    {
        var steam = await RestartSteamAsync(progress).ConfigureAwait(false);
        var discord = await RestartDiscordAsync(progress).ConfigureAwait(false);
        return (steam, discord);
    }

    private static async Task<bool> RestartSteamAsync(IProgress<string>? progress)
    {
        var steamPath = SteamService.GetSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return false;
        var steamExe = Path.Combine(steamPath, "steam.exe");
        if (!File.Exists(steamExe)) return false;

        if (IsSteamRunning())
        {
            progress?.Report("Closing Steam...");
            try
            {
                Process.Start(new ProcessStartInfo(steamExe, "-shutdown")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Steam shutdown failed: {ex.Message}"); }

            if (!await WaitForExitAsync("steam", TimeSpan.FromSeconds(30)).ConfigureAwait(false))
            {
                Debug.WriteLine("Steam did not exit in time — skipping restart");
                return false;
            }
        }

        progress?.Report("Opening Steam...");
        try
        {
            Process.Start(new ProcessStartInfo(steamExe, "-silent")
            {
                UseShellExecute = false,
                WorkingDirectory = steamPath,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Steam start failed: {ex.Message}");
            return false;
        }

        if (!await WaitForProcessAsync("steam", TimeSpan.FromSeconds(40)).ConfigureAwait(false))
            return false;

        // Steam needs a moment to read steamapps before Discord queries the library.
        await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
        progress?.Report("Steam ready");
        return true;
    }

    private static async Task<bool> RestartDiscordAsync(IProgress<string>? progress)
    {
        var running = DiscordClients
            .Where(client => Process.GetProcessesByName(client.Process).Length > 0)
            .ToList();
        // Reopen the same branch the user had; default to stable when Discord was closed.
        var target = running.Count > 0 ? running[0] : DiscordClients[0];

        if (running.Count > 0)
        {
            progress?.Report("Closing Discord...");
            foreach (var client in running)
            {
                foreach (var process in Process.GetProcessesByName(client.Process))
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (Exception ex) { Debug.WriteLine($"Discord kill failed: {ex.Message}"); }
                }
                await WaitForExitAsync(client.Process, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
            }
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var updateExe = Path.Combine(local, target.Folder, "Update.exe");
        if (!File.Exists(updateExe))
        {
            Debug.WriteLine($"Discord Update.exe not found at {updateExe}");
            return false;
        }

        progress?.Report("Opening Discord...");
        try
        {
            Process.Start(new ProcessStartInfo(updateExe, $"--processStart {target.Exe}")
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(updateExe)!,
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Discord start failed: {ex.Message}");
            return false;
        }

        if (!await WaitForProcessAsync(target.Process, TimeSpan.FromSeconds(40)).ConfigureAwait(false))
            return false;

        // Wait for the IPC socket so the timer process can attach its presence.
        await WaitForDiscordPipeAsync(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
        progress?.Report("Discord ready");
        return true;
    }

    private static async Task<bool> WaitForProcessAsync(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(processName).Length > 0) return true;
            await Task.Delay(250).ConfigureAwait(false);
        }
        return false;
    }

    private static async Task<bool> WaitForExitAsync(string processName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (Process.GetProcessesByName(processName).Length == 0) return true;
            await Task.Delay(250).ConfigureAwait(false);
        }
        return false;
    }

    // Discord exposes its RPC socket as \\.\pipe\discord-ipc-N. File.Exists works on
    // the pipe namespace, which is safer than enumerating \\.\pipe as a directory.
    private static async Task WaitForDiscordPipeAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            for (var index = 0; index < 10; index++)
            {
                if (File.Exists($@"\\.\pipe\discord-ipc-{index}")) return;
            }
            await Task.Delay(250).ConfigureAwait(false);
        }
    }
}
