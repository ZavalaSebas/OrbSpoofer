using System.Net.Http;
using System.Text.Json;

namespace OrbSpoofer.Services;

/// <summary>GamerPower giveaways client — Epic + Steam, 1h cache (ported from Bridge).</summary>
public sealed class FreeGamesService(HttpClient? httpClient = null)
{
    private const string ApiBase = "https://www.gamerpower.com/api/giveaways";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
    private static DateTime _lastFetch = DateTime.MinValue;
    private static List<FreeGameNotification>? _cache;
    private static readonly object CacheLock = new();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<List<FreeGameNotification>> GetFreeGamesAsync()
    {
        lock (CacheLock)
        {
            if (_cache != null && DateTime.UtcNow - _lastFetch < CacheDuration)
                return _cache;
        }

        try
        {
            var client = httpClient ?? SharedHttp;
            var epicTask = FetchPlatformAsync(client, "epic-games-store");
            var steamTask = FetchPlatformAsync(client, "steam");
            await Task.WhenAll(epicTask, steamTask);

            var results = epicTask.Result.Concat(steamTask.Result)
                .Where(g => g.Type.Equals("Game", StringComparison.OrdinalIgnoreCase))
                .Where(g => g.Worth != "N/A" && !string.IsNullOrWhiteSpace(g.Worth))
                .GroupBy(g => g.Id).Select(g => g.First())
                .OrderByDescending(g => g.PublishedDate)
                .ToList();

            lock (CacheLock)
            {
                _cache = results;
                _lastFetch = DateTime.UtcNow;
            }
            return results;
        }
        catch
        {
            lock (CacheLock) return _cache ?? [];
        }
    }

    private static readonly HttpClient SharedHttp = CreateClient();
    private static HttpClient CreateClient()
    {
        var c = new HttpClient();
        c.Timeout = TimeSpan.FromSeconds(10);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("OrbSpoofer/1.0");
        return c;
    }

    private async Task<List<FreeGameNotification>> FetchPlatformAsync(HttpClient client, string platform)
    {
        try
        {
            var json = await client.GetStringAsync($"{ApiBase}?platform={platform}");
            var dtos = JsonSerializer.Deserialize<List<GamerPowerDto>>(json, JsonOptions) ?? [];
            return dtos.Where(d => d.Status == "Active").Select(Map).ToList();
        }
        catch { return []; }
    }

    private static FreeGameNotification Map(GamerPowerDto d) => new()
    {
        Id = d.Id,
        Title = d.Title ?? string.Empty,
        Worth = d.Worth ?? string.Empty,
        Thumbnail = d.Thumbnail ?? string.Empty,
        Image = d.Image ?? string.Empty,
        Description = d.Description ?? string.Empty,
        OpenGiveawayUrl = d.OpenGiveawayUrl ?? d.GamerpowerUrl ?? string.Empty,
        GamerpowerUrl = d.GamerpowerUrl ?? string.Empty,
        Platforms = d.Platforms ?? string.Empty,
        Type = d.Type ?? string.Empty,
        EndDate = d.EndDate ?? string.Empty,
        PublishedDate = DateTime.TryParse(d.PublishedDate, out var dt) ? dt : DateTime.MinValue,
    };

    private sealed class GamerPowerDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Worth { get; set; }
        public string? Thumbnail { get; set; }
        public string? Image { get; set; }
        public string? Description { get; set; }
        public string? OpenGiveawayUrl { get; set; }
        public string? GamerpowerUrl { get; set; }
        public string? Platforms { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public string? EndDate { get; set; }
        public string? PublishedDate { get; set; }
    }
}
