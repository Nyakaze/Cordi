using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cordi.Configuration;

namespace Cordi.Services.Chatbox;

public sealed class GiphyResults
{
    private volatile IReadOnlyList<GiphyGif> _gifs = Array.Empty<GiphyGif>();
    private volatile bool _loading;
    private volatile bool _hasMore = true;
    private volatile string? _error;

    public GiphyResults(string query) => Query = query;

    public string Query { get; }
    public IReadOnlyList<GiphyGif> Gifs { get => _gifs; internal set => _gifs = value; }
    public bool Loading { get => _loading; internal set => _loading = value; }
    public bool HasMore { get => _hasMore; internal set => _hasMore = value; }
    public string? Error { get => _error; internal set => _error = value; }
    internal DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}

public sealed class GiphyClient : IDisposable
{
    public const int PageSize = 24;
    public const int HourlyLimit = 100;
    public const string DashboardUrl = "https://developers.giphy.com/dashboard/";

    private const string Rating = "pg-13";
    private const string CacheFileName = "giphy-cache.json";
    private const int MaxCachedQueries = 200;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan TrendingLifetime = TimeSpan.FromHours(2);
    private static readonly TimeSpan SearchLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan ErrorLifetime = TimeSpan.FromMinutes(1);
    private static readonly Regex GifId = new("^[A-Za-z0-9]{1,64}$", RegexOptions.Compiled);

    private readonly Func<string> _apiKey;
    private readonly CordiLogService _log;
    private readonly string _cachePath;
    private readonly ConcurrentDictionary<string, GiphyResults> _results = new(StringComparer.Ordinal);
    private readonly Queue<DateTime> _requests = new();
    private readonly object _saveGate = new();
    private readonly CancellationTokenSource _cancel = new();
    private string _key = string.Empty;

    public GiphyClient(string configDirectory, Func<string> apiKey, CordiLogService log)
    {
        _apiKey = apiKey;
        _log = log;
        _cachePath = Path.Combine(configDirectory, CacheFileName);

        LoadCache();
    }

    public bool HasKey => (_apiKey() ?? string.Empty).Trim().Length > 0;

    public int RequestsLastHour
    {
        get
        {
            lock (_requests)
            {
                TrimRequests();
                return _requests.Count;
            }
        }
    }

    public GiphyResults Get(string query)
    {
        var key = (_apiKey() ?? string.Empty).Trim();
        if (!string.Equals(key, _key, StringComparison.Ordinal))
        {
            foreach (var entry in _results)
            {
                if (entry.Value.Error != null) _results.TryRemove(entry.Key, out _);
            }

            _key = key;
        }

        query = query.Trim().ToLowerInvariant();

        if (_results.TryGetValue(query, out var cached) && (cached.Loading || !Expired(cached)))
            return cached;

        var results = new GiphyResults(query);
        _results[query] = results;
        LoadMore(results);
        return results;
    }

    public void LoadMore(GiphyResults results)
    {
        if (results.Loading || !results.HasMore || _key.Length == 0) return;

        if (!TryReserveRequest())
        {
            results.Error = $"Hourly Giphy limit reached ({HourlyLimit} requests). Try again later.";
            results.FetchedAt = DateTime.UtcNow;
            return;
        }

        results.Error = null;
        results.Loading = true;

        var key = _key;
        var offset = results.Gifs.Count;
        _ = Task.Run(() => FetchAsync(results, key, offset));
    }

    public void ClearCache()
    {
        _results.Clear();
        SaveCache();
    }

    public int CachedQueries => _results.Count(entry => entry.Value.Error == null && entry.Value.Gifs.Count > 0);

    private static bool Expired(GiphyResults results) =>
        DateTime.UtcNow - results.FetchedAt > (results.Error != null
            ? ErrorLifetime
            : results.Query.Length == 0 ? TrendingLifetime : SearchLifetime);

    private bool TryReserveRequest()
    {
        lock (_requests)
        {
            TrimRequests();
            if (_requests.Count >= HourlyLimit) return false;

            _requests.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    private void TrimRequests()
    {
        var cutoff = DateTime.UtcNow.AddHours(-1);
        while (_requests.Count > 0 && _requests.Peek() < cutoff) _requests.Dequeue();
    }

    private async Task FetchAsync(GiphyResults results, string key, int offset)
    {
        var endpoint = results.Query.Length == 0
            ? "trending?"
            : $"search?q={Uri.EscapeDataString(results.Query)}&lang=en&";

        var url = $"https://api.giphy.com/v1/gifs/{endpoint}api_key={Uri.EscapeDataString(key)}&limit={PageSize}&offset={offset}&rating={Rating}";
        var succeeded = false;

        try
        {
            using var response = await Http.GetAsync(url, _cancel.Token).ConfigureAwait(false);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                results.Error = "Giphy rejected the API key. Check it under Settings -> Chatbox -> Content -> GIF Picker.";
                return;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                results.Error = $"Giphy rate limit reached ({HourlyLimit} requests per hour). Try again later.";
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                results.Error = $"Giphy returned an error ({(int)response.StatusCode}).";
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(_cancel.Token).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: _cancel.Token).ConfigureAwait(false);

            var page = Parse(document.RootElement, out var total);
            var merged = new List<GiphyGif>(results.Gifs);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var gif in merged) seen.Add(gif.Id);
            foreach (var gif in page)
            {
                if (seen.Add(gif.Id)) merged.Add(gif);
            }

            results.Gifs = merged;
            results.HasMore = page.Count > 0 && offset + page.Count < total;
            succeeded = true;
        }
        catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            results.Error = "Could not reach Giphy.";
            _log.Log("Giphy", CordiLogLevel.Warning, "Giphy request failed.", ex);
        }
        finally
        {
            results.FetchedAt = DateTime.UtcNow;
            results.Loading = false;
        }

        if (succeeded) SaveCache();
    }

    private static List<GiphyGif> Parse(JsonElement root, out int total)
    {
        var gifs = new List<GiphyGif>();
        total = 0;

        if (root.ValueKind != JsonValueKind.Object) return gifs;

        if (root.TryGetProperty("pagination", out var pagination)
            && pagination.ValueKind == JsonValueKind.Object
            && pagination.TryGetProperty("total_count", out var count)
            && count.ValueKind == JsonValueKind.Number)
        {
            count.TryGetInt32(out total);
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return gifs;

        foreach (var item in data.EnumerateArray())
        {
            var id = Text(item, "id");
            if (id == null || !GifId.IsMatch(id)) continue;
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("images", out var images)) continue;

            var preview = Rendition(images, "fixed_width_small")
                ?? Rendition(images, "fixed_width")
                ?? Rendition(images, "fixed_height_small");
            if (preview == null) continue;

            var title = Text(item, "title");
            gifs.Add(new GiphyGif
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(title) ? "GIF" : title.Trim(),
                PreviewUrl = preview.Value.Url,
                Aspect = preview.Value.Aspect,
            });
        }

        return gifs;
    }

    private static (string Url, float Aspect)? Rendition(JsonElement images, string name)
    {
        if (images.ValueKind != JsonValueKind.Object || !images.TryGetProperty(name, out var image)) return null;

        var url = Text(image, "url");
        if (url == null || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.Host.EndsWith("giphy.com", StringComparison.OrdinalIgnoreCase)) return null;

        var width = Number(image, "width");
        var height = Number(image, "height");
        var aspect = width > 0f && height > 0f ? Math.Clamp(height / width, 0.25f, 4f) : 1f;

        return (url, aspect);
    }

    private static float Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return 0f;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number)) return number;

        return value.ValueKind == JsonValueKind.String
            && float.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0f;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private void LoadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return;

            var cache = JsonSerializer.Deserialize<GiphyCacheFile>(File.ReadAllText(_cachePath));
            if (cache == null) return;

            lock (_requests)
            {
                foreach (var time in cache.Requests.Order()) _requests.Enqueue(time);
                TrimRequests();
            }

            foreach (var page in cache.Results)
            {
                var gifs = page.Gifs.Where(gif => GifId.IsMatch(gif.Id) && gif.PreviewUrl.StartsWith("https://", StringComparison.Ordinal)).ToList();
                if (page.Query == null || gifs.Count == 0) continue;

                var results = new GiphyResults(page.Query)
                {
                    Gifs = gifs,
                    HasMore = page.HasMore,
                    FetchedAt = page.FetchedAt,
                };

                if (!Expired(results)) _results[page.Query] = results;
            }
        }
        catch (Exception ex)
        {
            _log.Log("Giphy", CordiLogLevel.Warning, "Could not read the Giphy cache, starting empty.", ex);
        }
    }

    private void SaveCache()
    {
        try
        {
            var cache = new GiphyCacheFile
            {
                Results = _results.Values
                    .Where(results => results.Error == null && !results.Loading && results.Gifs.Count > 0)
                    .OrderByDescending(results => results.FetchedAt)
                    .Take(MaxCachedQueries)
                    .Select(results => new GiphyCachedPage
                    {
                        Query = results.Query,
                        FetchedAt = results.FetchedAt,
                        HasMore = results.HasMore,
                        Gifs = results.Gifs.ToList(),
                    })
                    .ToList(),
            };

            lock (_requests) cache.Requests = _requests.ToList();

            var json = JsonSerializer.Serialize(cache);

            lock (_saveGate)
            {
                var temp = _cachePath + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, _cachePath, true);
            }
        }
        catch (Exception ex)
        {
            _log.Log("Giphy", CordiLogLevel.Warning, "Could not write the Giphy cache.", ex);
        }
    }

    public void Dispose()
    {
        _cancel.Cancel();
        SaveCache();
        _cancel.Dispose();
    }

    private sealed class GiphyCacheFile
    {
        public List<DateTime> Requests { get; set; } = new();
        public List<GiphyCachedPage> Results { get; set; } = new();
    }

    private sealed class GiphyCachedPage
    {
        public string? Query { get; set; }
        public DateTime FetchedAt { get; set; }
        public bool HasMore { get; set; }
        public List<GiphyGif> Gifs { get; set; } = new();
    }
}
