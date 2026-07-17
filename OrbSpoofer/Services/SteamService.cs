using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using OrbSpoofer.Models;
using OrbSpoofer.Exceptions;

namespace OrbSpoofer.Services;

public class SteamService
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
            return results;
        }
        catch (NetworkError)
        {
            return [];
        }
    }

    public static async Task<SteamAppInfo?> FetchAppInfoAsync(int appId)
    {
        try
        {
            var url = $"{Config.SteamCmdApiUrl}/{appId}";
            var json = await NetworkHelper.FetchJsonAsync(url);

            if (!json.TryGetProperty("data", out var data) ||
                !data.TryGetProperty(appId.ToString(), out var appData))
                return null;

            var name = appData.TryGetProperty("common", out var common) &&
                       common.TryGetProperty("name", out var nameProp)
                ? nameProp.GetString() ?? $"App {appId}"
                : $"App {appId}";

            var installDir = appData.TryGetProperty("config", out var config) &&
                             config.TryGetProperty("installdir", out var dirProp)
                ? dirProp.GetString() ?? name
                : name;

            var executable = "";
            if (config.TryGetProperty("launch", out var launch) && launch.ValueKind == JsonValueKind.Object)
            {
                executable = PickWindowsExe(launch);
            }

            if (string.IsNullOrEmpty(executable))
                executable = installDir.Split('/').LastOrDefault() + ".exe";

            string? depotId = null;
            if (appData.TryGetProperty("depots", out var depots) && depots.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in depots.EnumerateObject())
                {
                    if (int.TryParse(prop.Name, out _) && prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        depotId = prop.Name;
                        break;
                    }
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
        catch (NetworkError)
        {
            return null;
        }
    }

    private static string PickWindowsExe(JsonElement launch)
    {
        var entries = launch.EnumerateObject()
            .Where(p => p.Value.ValueKind == JsonValueKind.Object)
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var entry in entries)
        {
            var config = entry.Value.TryGetProperty("config", out var c) ? c : default;
            var oslist = config.TryGetProperty("oslist", out var os) ? os.GetString() ?? "windows" : "windows";

            if (oslist.Contains("windows") || string.IsNullOrEmpty(oslist))
            {
                var exe = entry.Value.TryGetProperty("executable", out var e) ? e.GetString() ?? "" : "";
                if (exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return exe.Replace('\\', '/');
            }
        }
        return "";
    }

    public static string GenerateAppManifest(
        int appId, string name, string installDir, string steamPath, string? depotId = null)
    {
        var owner = GetSteamUserId();
        var launcher = Path.Combine(steamPath, "steam.exe").Replace("/", "\\");

        var staged = "";
        if (!string.IsNullOrEmpty(depotId))
        {
            staged =
                $"\t\t\"{depotId}\"\n" +
                "\t\t{\n" +
                "\t\t\t\"manifest\"\t\t\"0\"\n" +
                $"\t\t\t\"size\"\t\t\"{Config.ManifestOneGiB}\"\n" +
                "\t\t\t\"dlcappid\"\t\t\"0\"\n" +
                "\t\t}";
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
