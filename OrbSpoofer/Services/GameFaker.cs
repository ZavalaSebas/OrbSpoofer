using System.Diagnostics;
using System.IO;

namespace OrbSpoofer.Services;

public class GameFaker
{
    public string DesktopPath { get; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    private string? _sourceExe;
    private bool _isSelfContained;
    private bool _initialized;

    public string? SourceExePath => _sourceExe;
    public bool IsSelfContained => _isSelfContained;
    public bool IsInitialized => _initialized;

    public async Task InitializeAsync(IProgress<string>? progress = null)
    {
        if (_initialized) return;

        progress?.Report("Preparing executable...");
        await EnsurePublishedExeAsync(progress);
        _isSelfContained = DetectSelfContained();
        _sourceExe = FindSourceExe();
        _initialized = true;
    }

    private async Task EnsurePublishedExeAsync(IProgress<string>? progress = null)
    {
        var projectDir = FindProjectRoot();
        if (projectDir == null) return;

        var publishExe = Path.Combine(projectDir, "bin", "Release", "net10.0-windows", "win-x64", "publish", "OrbSpoofer.exe");
        if (File.Exists(publishExe)) return;

        progress?.Report("Building self-contained executable (first time only)...");

        try
        {
            var csproj = Path.Combine(projectDir, "OrbSpoofer.csproj");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{csproj}\" -c Release --self-contained true -p:PublishSingleFile=true",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Config.PublishTimeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    progress?.Report("Build timed out");
                    try
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                    catch (Exception killEx)
                    {
                        Debug.WriteLine($"Failed to kill build process: {killEx.Message}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            progress?.Report($"Publish failed: {ex.Message}");
        }
    }

    private static bool DetectSelfContained()
    {
        if (Environment.ProcessPath == null) return true;
        var exeDir = Path.GetDirectoryName(Environment.ProcessPath) ?? "";
        var exeName = Path.GetFileNameWithoutExtension(Environment.ProcessPath);
        var depsFile = Path.Combine(exeDir, exeName + ".deps.json");
        return !File.Exists(depsFile);
    }

    private static string? FindSourceExe()
    {
        var currentExe = Environment.ProcessPath ?? "";

        if (File.Exists(currentExe))
        {
            var exeDir = Path.GetDirectoryName(currentExe)!;
            var depsFile = Path.Combine(exeDir, Path.GetFileNameWithoutExtension(currentExe) + ".deps.json");
            if (!File.Exists(depsFile))
                return currentExe;
        }

        var projectDir = FindProjectRoot();
        if (projectDir != null)
        {
            var publishExe = Path.Combine(projectDir, "bin", "Release", "net10.0-windows", "win-x64", "publish", "OrbSpoofer.exe");
            if (File.Exists(publishExe))
                return publishExe;
        }

        return File.Exists(currentExe) ? currentExe : null;
    }

    private static string? FindProjectRoot()
    {
        var dir = Path.GetDirectoryName(Environment.ProcessPath);
        var limit = Config.MaxProjectRootDepth;
        while (dir != null && limit-- > 0)
        {
            if (File.Exists(Path.Combine(dir, "OrbSpoofer.csproj")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    public string? CreateFakeGame(string exeName)
    {
        if (_sourceExe == null) return null;

        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            exeName += ".exe";

        exeName = exeName.Replace('\\', '/').Trim().TrimStart('/', '\\');
        // Preserve subdirectories like bin/helldivers2.exe — needed for Discord detection (HELLDIVERS 2)
        // Validate traversal and invalid chars per segment
        if (string.IsNullOrWhiteSpace(exeName) || exeName.Contains(".."))
            return null;
        var segments = exeName.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;
        foreach (var seg in segments)
            if (seg.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        if (!exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return null;

        var targetPath = Security.PathContainment.TryResolveUnderRoot(Path.Combine(DesktopPath, Config.FakeExeDir), exeName);
        if (targetPath == null) return null;

        try
        {
            var dir = Path.GetDirectoryName(targetPath)!;
            Directory.CreateDirectory(dir);

            // If a previous timer is still running, the old copy is locked and a
            // plain overwrite fails — launching the stale exe would run an OLD
            // build (pre-Fluent UI). Delete first, then copy fresh.
            if (File.Exists(targetPath))
            {
                try { File.Delete(targetPath); }
                catch (IOException)
                {
                    Debug.WriteLine($"CreateFakeGame: {targetPath} locked by a running timer — reusing existing copy");
                    return targetPath; // locked = still running; reuse is correct here
                }
            }

            File.Copy(_sourceExe, targetPath, overwrite: true);
            return targetPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateFakeGame failed: {ex.Message}");
            return null;
        }
    }

    public bool LaunchExecutable(string exePath, string? gameName = null, string? questId = null, string? discordAppId = null)
    {
        return LaunchExecutable(exePath, out _, gameName, questId, discordAppId);
    }

    // Overload that returns the launched timer Process — allows waiting for
    // it to finish (Run-all sequence) instead of polling by window title.
    public bool LaunchExecutable(string exePath, out Process? launchedProcess, string? gameName = null, string? questId = null, string? discordAppId = null)
    {
        launchedProcess = null;
        try
        {
            var args = $"--timer-mode --exe-path \"{exePath}\" --game-name \"{gameName ?? Path.GetFileNameWithoutExtension(exePath)}\"";
            if (!string.IsNullOrEmpty(questId))
                args += $" --quest-id \"{questId}\"";
            if (!string.IsNullOrEmpty(discordAppId))
                args += $" --discord-app-id \"{discordAppId}\"";

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var process = new Process { StartInfo = psi };
            process.Start();
            launchedProcess = process;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LaunchExecutable failed: {ex.Message}");
            return false;
        }
    }

    public string? CreateSteamFakeGame(string exePath)
    {
        if (_sourceExe == null) return null;
        // Validate containment under steamapps/common (prevent path traversal via crafted installDir)
        var steamRoot = Services.SteamService.GetSteamPath();
        var commonRoot = steamRoot != null ? Path.Combine(steamRoot, "steamapps", "common") : null;
        if (commonRoot != null && !Security.PathContainment.IsUnderRoot(exePath, commonRoot) && !Security.PathContainment.IsUnderRoot(exePath, Path.Combine(steamRoot!, "steamapps")))
            return null;
        try
        {
            var dir = Path.GetDirectoryName(exePath)!;
            Directory.CreateDirectory(dir);
            File.Copy(_sourceExe, exePath, overwrite: true);
            return exePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CreateSteamFakeGame failed: {ex.Message}");
            return null;
        }
    }
}
