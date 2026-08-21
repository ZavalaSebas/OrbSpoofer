using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using OrbSpoofer.Models;
using OrbSpoofer.Exceptions;

namespace OrbSpoofer.Services;

public static class SteamService
{
    public static string? GetSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var value = key?.GetValue("SteamPath")?.ToString();
            if (!string.IsNullOrEmpty(value))
                return value;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read Steam registry path: {ex.Message}");
        }

        return Directory.Exists(Config.SteamDefaultPath) ? Config.SteamDefaultPath : null;
    }

    public static string GetSteamUserId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess");
            var value = key?.GetValue("ActiveUser");
            if (value is int intVal)
                return (intVal + Config.SteamIdOffset).ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to read Steam user ID: {ex.Message}");
        }

        return Config.DefaultSteamUserId;
    }

    public static async Task<List<SteamSearchResult>> SearchGamesAsync(string query)
    {
        try
        {
            var json = await NetworkHelper.FetchJsonAsync(
                Config.SteamStoreSearchUrl,
                queryParams: new Dictionary<string, string>
                {
                    ["term"] = query,
                    ["l"] = "english",
                    ["cc"] = "US"
                });

            var results = new List<SteamSearchResult>();
            if (json.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    results.Add(new SteamSearchResult
                    {
                        Id = item.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                        Name = item.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    });
                }
            }

            SaveSearchCache(query, results);
            return results;
        }
        catch (NetworkError)
        {
            return LoadSearchCache(query) ?? [];
        }
    }

    private static string GetSearchCachePath(string query)
    {
        var safeName = string.Concat(query.Where(c => char.IsLetterOrDigit(c) || c == ' ')).Trim();
        safeName = safeName.Replace(' ', '_');
        return Path.Combine(Config.AppDataPath, Config.SteamSearchCacheDir, $"{safeName}.json");
    }

    private static void SaveSearchCache(string query, List<SteamSearchResult> results)
    {
        try
        {
            var dir = Path.Combine(Config.AppDataPath, Config.SteamSearchCacheDir);
            Directory.CreateDirectory(dir);
            var path = GetSearchCachePath(query);
            File.WriteAllText(path, JsonSerializer.Serialize(results));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save Steam search cache: {ex.Message}");
        }
    }

    private static List<SteamSearchResult>? LoadSearchCache(string query)
    {
        try
        {
            var path = GetSearchCachePath(query);
            if (!File.Exists(path)) return null;

            var fileInfo = new FileInfo(path);
            var ageDays = (DateTime.Now - fileInfo.LastWriteTime).Days;
            if (ageDays > Config.MaxCacheAgeDays) return null;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<SteamSearchResult>>(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load Steam search cache: {ex.Message}");
            return null;
        }
    }

    // Discord quests sometimes look for the real game binary (Unreal Shipping, etc.),
    // not SteamCMD's launcher entry. Tokon: Binaries/Win64/MTFSSteam-Win64-Shipping.exe
    private static readonly Dictionary<int, string> DiscordVerifiedExes = new()
    {
        [3787240] = "Binaries/Win64/MTFSSteam-Win64-Shipping.exe",
    };

    public static async Task<SteamAppInfo?> FetchAppInfoAsync(int appId)
    {
        try
        {
            var url = $"{Config.SteamCmdApiUrl}/{appId}";
            var json = await NetworkHelper.FetchJsonAsync(url);
            return ParseAppInfo(json, appId);
        }
        catch (NetworkError)
        {
            return null;
        }
    }

    // Matches orbshacker steam.py: missing config is {} (not a crash), and launch
    // entries without a config block default oslist to "windows".
    public static SteamAppInfo? ParseAppInfo(JsonElement json, int appId)
    {
        if (json.ValueKind != JsonValueKind.Object ||
            !json.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty(appId.ToString(), out var appData) || appData.ValueKind != JsonValueKind.Object)
            return null;

        var name = $"App {appId}";
        if (appData.TryGetProperty("common", out var common) && common.ValueKind == JsonValueKind.Object &&
            common.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            name = nameProp.GetString() ?? name;

        var hasConfig = appData.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object;
        var installDir = name;
        if (hasConfig && config.TryGetProperty("installdir", out var dirProp) && dirProp.ValueKind == JsonValueKind.String)
            installDir = dirProp.GetString() ?? installDir;

        var executable = "";
        if (DiscordVerifiedExes.TryGetValue(appId, out var verifiedExe))
            executable = verifiedExe;
        else if (hasConfig && config.TryGetProperty("launch", out var launch) && launch.ValueKind == JsonValueKind.Object)
            executable = PickWindowsExe(launch);

        if (string.IsNullOrEmpty(executable))
            executable = installDir.Split('/').LastOrDefault() + ".exe";

        string? depotId = null;
        if (appData.TryGetProperty("depots", out var depots) && depots.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in depots.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out _) || prop.Value.ValueKind != JsonValueKind.Object)
                    continue;
                // Skip Steamworks redistributables (VC++, DirectX, etc.)
                if (prop.Value.TryGetProperty("depotfromapp", out _) ||
                    prop.Value.TryGetProperty("sharedinstall", out _))
                    continue;
                depotId = prop.Name;
                break;
            }
        }

        return new SteamAppInfo
        {
            Name = name,
            InstallDir = installDir,
            Executable = executable,
            DepotId = depotId
        };
    }

    public static string GetInstallExePath(string steamPath, string installDir, string executable)
    {
        var relative = Path.Combine(
            installDir,
            executable.Replace('/', Path.DirectorySeparatorChar));
        return Path.Combine(steamPath, "steamapps", "common", relative);
    }

    private static string PickWindowsExe(JsonElement launch)
    {
        var entries = launch.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Object)
            .OrderBy(p => p.Name);

        foreach (var entry in entries)
        {
            var oslist = "windows";
            if (entry.Value.TryGetProperty("config", out var cfg) && cfg.ValueKind == JsonValueKind.Object &&
                cfg.TryGetProperty("oslist", out var os) && os.ValueKind == JsonValueKind.String)
                oslist = os.GetString() ?? "windows";

            if (!oslist.Contains("windows") && oslist.Length > 0)
                continue;

            if (!entry.Value.TryGetProperty("executable", out var e) || e.ValueKind != JsonValueKind.String)
                continue;

            var exe = (e.GetString() ?? "").Replace('\\', '/');
            if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                continue;

            // Easy Anti-Cheat's launcher kills non-EAC binaries named start_protected_game.exe
            var file = Path.GetFileName(exe);
            if (file.Equals("start_protected_game.exe", StringComparison.OrdinalIgnoreCase) ||
                file.Contains("easyanticheat", StringComparison.OrdinalIgnoreCase) ||
                file.Contains("battleye", StringComparison.OrdinalIgnoreCase))
                continue;

            return exe;
        }
        return "";
    }

    public static string GenerateAppManifest(
        int appId, string name, string installDir, string steamPath, string? depotId = null)
    {
        var owner = GetSteamUserId();
        var launcher = Path.Combine(steamPath, "steam.exe").Replace("/", "\\");

        var staged = "";
        var installed = "";
        if (!string.IsNullOrEmpty(depotId))
        {
            installed =
                $"\t\t\"{depotId}\"\n" +
                "\t\t{\n" +
                "\t\t\t\"manifest\"\t\t\"0\"\n" +
                $"\t\t\t\"size\"\t\t\"{Config.ManifestOneGiB}\"\n" +
                "\t\t\t\"dlcappid\"\t\t\"0\"\n" +
                "\t\t}";
            staged = installed;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\"AppState\"");
        sb.AppendLine("{");
        sb.AppendLine($"\t\"appid\"\t\t\"{appId}\"");
        sb.AppendLine("\t\"universe\"\t\t\"1\"");
        sb.AppendLine($"\t\"LauncherPath\"\t\t\"{launcher}\"");
        sb.AppendLine($"\t\"name\"\t\t\"{name}\"");
        sb.AppendLine($"\t\"StateFlags\"\t\t\"{Config.ManifestStateFlags}\"");
        sb.AppendLine($"\t\"installdir\"\t\t\"{installDir}\"");
        sb.AppendLine("\t\"LastUpdated\"\t\t\"0\"");
        sb.AppendLine("\t\"LastPlayed\"\t\t\"0\"");
        sb.AppendLine("\t\"SizeOnDisk\"\t\t\"0\"");
        sb.AppendLine($"\t\"StagingSize\"\t\t\"{Config.ManifestOneGiB}\"");
        sb.AppendLine("\t\"buildid\"\t\t\"0\"");
        sb.AppendLine($"\t\"LastOwner\"\t\t\"{owner}\"");
        sb.AppendLine("\t\"DownloadType\"\t\t\"1\"");
        sb.AppendLine("\t\"UpdateResult\"\t\t\"4\"");
        sb.AppendLine($"\t\"BytesToDownload\"\t\t\"{Config.ManifestOneGiB}\"");
        sb.AppendLine($"\t\"BytesDownloaded\"\t\t\"{Config.ManifestPartialBytes}\"");
        sb.AppendLine($"\t\"BytesToStage\"\t\t\"{Config.ManifestOneGiB}\"");
        sb.AppendLine($"\t\"BytesStaged\"\t\t\"{Config.ManifestPartialBytes}\"");
        sb.AppendLine("\t\"TargetBuildID\"\t\t\"0\"");
        sb.AppendLine("\t\"AutoUpdateBehavior\"\t\t\"0\"");
        sb.AppendLine("\t\"AllowOtherDownloadsWhileRunning\"\t\t\"0\"");
        sb.AppendLine("\t\"ScheduledAutoUpdate\"\t\t\"0\"");
        sb.AppendLine("\t\"InstalledDepots\"");
        sb.AppendLine("\t{");
        if (!string.IsNullOrEmpty(installed))
            sb.AppendLine(installed);
        sb.AppendLine("\t}");
        sb.AppendLine("\t\"StagedDepots\"");
        sb.AppendLine("\t{");
        if (!string.IsNullOrEmpty(staged))
            sb.AppendLine(staged);
        sb.AppendLine("\t}");
        sb.AppendLine("\t\"UserConfig\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t}");
        sb.AppendLine("\t\"MountedConfig\"");
        sb.AppendLine("\t{");
        sb.AppendLine("\t}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    public static bool WriteAppManifest(int appId, string name, string installDir, string steamPath, string? depotId = null)
    {
        try
        {
            var content = GenerateAppManifest(appId, name, installDir, steamPath, depotId);
            var acfPath = Path.Combine(steamPath, "steamapps", $"appmanifest_{appId}.acf");
            var dir = Path.GetDirectoryName(acfPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(acfPath, content);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to write app manifest: {ex.Message}");
            return false;
        }
    }
}
