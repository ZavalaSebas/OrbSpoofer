using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using OrbSpoofer.Exceptions;

namespace OrbSpoofer.Services;

public static class NetworkHelper
{
    private static readonly HttpClient Client = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(Config.RequestTimeout);
        return client;
    }

    public static async Task<JsonElement> FetchJsonAsync(
        string url,
        Dictionary<string, string>? headers = null,
        Dictionary<string, string>? queryParams = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (headers != null)
            {
                foreach (var (key, value) in headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            if (queryParams != null && queryParams.Count > 0)
            {
                var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                request.RequestUri = new Uri($"{url}?{query}");
            }

            using var response = await Client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkError($"Request to {url} failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new NetworkError($"Request to {url} timed out: {ex.Message}", ex);
        }
    }

    public static async Task DownloadFileAsync(string url, string destPath, IProgress<double>? progress = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, Config.DownloadBufferSize);

            var buffer = new byte[Config.DownloadBufferSize];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cts.Token)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cts.Token);
                totalRead += bytesRead;
                if (totalBytes > 0)
                    progress?.Report((double)totalRead / totalBytes * 100);
            }
        }
        catch (OperationCanceledException)
        {
            throw new NetworkError($"Download from {url} timed out", null!);
        }
        catch (Exception ex)
        {
            throw new NetworkError($"Download from {url} failed: {ex.Message}", ex);
        }
    }
}
