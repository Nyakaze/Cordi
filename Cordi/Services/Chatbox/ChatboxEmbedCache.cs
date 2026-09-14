using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxEmbedCache : IDisposable
{
    private const int MaxHtmlBytes = 512 * 1024;
    private const int MaxDescriptionLength = 350;
    private const int MaxTitleLength = 160;

    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan EmptyLifetime = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private const string SelectEmbedSql = """
        SELECT payload FROM embeds
        WHERE url = $url
          AND fetched_at >= (CASE WHEN length(payload) = 0 THEN $oldestEmpty ELSE $oldest END);
        """;

    private const string PruneEmbedSql = """
        DELETE FROM embeds
        WHERE (length(payload) > 0 AND fetched_at < $oldest)
           OR (length(payload) = 0 AND fetched_at < $oldestEmpty);
        """;

    private static readonly Regex KlipyGif = new(
        @"^https?://(?:www\.)?klipy\.com/gifs/(?<slug>[A-Za-z0-9._~-]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ChatboxDatabase _database;
    private readonly Func<int> _lifetimeDays;
    private readonly ConcurrentDictionary<string, ChatboxLinkEmbed> _embeds = new(StringComparer.Ordinal);
    private readonly ChatboxFetchQueue _fetch = new(2, FailureBackoff, "Embed lookup");
    private bool _disposed;

    public ChatboxEmbedCache(ChatboxDatabase database, Func<int> lifetimeDays)
    {
        _database = database;
        _lifetimeDays = lifetimeDays;

        PruneUnresolved();
    }

    public int PendingFetches => _fetch.Pending;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (compatible; Cordi-Chatbox/1.0; +https://github.com/)");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml");
        client.DefaultRequestHeaders.Add("Accept-Language", "en");
        return client;
    }

    public ChatboxLinkEmbed? Get(string? url)
    {
        if (_disposed || string.IsNullOrWhiteSpace(url)) return null;
        if (!IsFetchable(url, out var uri)) return null;

        var key = uri!.AbsoluteUri;

        if (_embeds.TryGetValue(key, out var cached))
            return cached;

        _fetch.Enqueue(key, ResolveAsync);
        return null;
    }

    public static bool IsFetchable(string url, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (IsPrivateHost(parsed.Host)) return false;

        uri = parsed;
        return true;
    }

    private static bool IsPrivateHost(string host)
    {
        if (string.IsNullOrEmpty(host)) return true;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host, out var address)) return false;

        if (IPAddress.IsLoopback(address)) return true;
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal) return true;

        var bytes = address.GetAddressBytes();
        if (bytes.Length != 4) return false;

        return bytes[0] switch
        {
            10 => true,
            127 => true,
            169 when bytes[1] == 254 => true,
            172 when bytes[1] >= 16 && bytes[1] <= 31 => true,
            192 when bytes[1] == 168 => true,
            _ => false,
        };
    }

    private async Task ResolveAsync(string url)
    {
        var stored = ReadStored(url);
        if (stored != null)
        {
            _embeds[url] = stored;
            return;
        }

        var embed = await FetchAsync(url).ConfigureAwait(false);
        if (embed == null)
        {
            StoreEmpty(url);
            if (!_disposed) _embeds[url] = new ChatboxLinkEmbed { Url = url };
            return;
        }

        Store(url, embed);
        if (!_disposed) _embeds[url] = embed;
    }

    private async Task<ChatboxLinkEmbed?> FetchAsync(string url)
    {
        if (!await _fetch.EnterGateAsync().ConfigureAwait(false))
            return null;

        try
        {
            var provider = await FetchProviderAsync(url).ConfigureAwait(false);
            if (provider != null) return provider;

            using var response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _fetch.Token)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode) return null;

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

            if (mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return ChatboxLinkEmbed.ForImage(url);

            if (!mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;

            var html = await ReadCappedAsync(response).ConfigureAwait(false);
            if (string.IsNullOrEmpty(html)) return null;

            return OpenGraphParser.Parse(html, url, MaxTitleLength, MaxDescriptionLength);
        }
        finally
        {
            _fetch.ExitGate();
        }
    }

    private async Task<ChatboxLinkEmbed?> FetchProviderAsync(string url)
    {
        var match = KlipyGif.Match(url);
        if (!match.Success) return null;

        using var response = await Http
            .GetAsync($"https://api.klipy.com/api/v1/gifs/{match.Groups["slug"].Value}", _fetch.Token)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode) return null;

        var payload = await response.Content.ReadAsStringAsync(_fetch.Token).ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("file", out var file)) return null;

            var media = SmallestGifUrl(file);
            if (media == null) return null;

            var embed = ChatboxLinkEmbed.ForImage(media);
            embed.Url = url;
            embed.SiteName = "Klipy";
            return embed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? SmallestGifUrl(JsonElement file)
    {
        string? best = null;
        var bestSize = long.MaxValue;

        foreach (var quality in new[] { "sd", "md", "hd" })
        {
            if (!file.TryGetProperty(quality, out var bucket)) continue;
            if (!bucket.TryGetProperty("gif", out var gif)) continue;
            if (!gif.TryGetProperty("url", out var value)) continue;

            var url = value.GetString();
            if (string.IsNullOrEmpty(url)) continue;

            var size = gif.TryGetProperty("size", out var bytes) && bytes.TryGetInt64(out var parsed) && parsed > 0
                ? parsed
                : long.MaxValue;

            if (best != null && size >= bestSize) continue;

            best = url;
            bestSize = size;
        }

        return best;
    }

    private async Task<string> ReadCappedAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(_fetch.Token).ConfigureAwait(false);

        var buffer = new byte[8192];
        using var memory = new MemoryStream();

        while (memory.Length < MaxHtmlBytes)
        {
            var read = await stream.ReadAsync(buffer, _fetch.Token).ConfigureAwait(false);
            if (read <= 0) break;

            memory.Write(buffer, 0, read);
        }

        var encoding = ResolveEncoding(response);
        return encoding.GetString(memory.GetBuffer(), 0, (int)memory.Length);
    }

    private static System.Text.Encoding ResolveEncoding(HttpResponseMessage response)
    {
        var name = response.Content.Headers.ContentType?.CharSet?.Trim('"');
        if (string.IsNullOrEmpty(name)) return System.Text.Encoding.UTF8;

        try { return System.Text.Encoding.GetEncoding(name); }
        catch (ArgumentException) { return System.Text.Encoding.UTF8; }
    }

    private ChatboxLinkEmbed? ReadStored(string url)
    {
        var oldest = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _lifetimeDays())).ToUnixTimeSeconds();
        var oldestEmpty = DateTimeOffset.UtcNow.Subtract(EmptyLifetime).ToUnixTimeSeconds();

        return _database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = SelectEmbedSql;
            command.Parameters.AddWithValue("$url", url);
            command.Parameters.AddWithValue("$oldest", oldest);
            command.Parameters.AddWithValue("$oldestEmpty", oldestEmpty);

            if (command.ExecuteScalar() is not string payload) return null;
            if (payload.Length == 0) return new ChatboxLinkEmbed { Url = url };

            try { return JsonSerializer.Deserialize<ChatboxLinkEmbed>(payload, Json); }
            catch (JsonException) { return null; }
        }, null, "read embed");
    }

    private void Store(string url, ChatboxLinkEmbed embed) =>
        Persist(url, JsonSerializer.Serialize(embed, Json));

    private void StoreEmpty(string url) => Persist(url, string.Empty);

    private void Persist(string url, string payload) => _database.Write(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO embeds(url, payload, fetched_at)
            VALUES ($url, $payload, $now)
            ON CONFLICT(url) DO UPDATE SET payload = $payload, fetched_at = $now;
            """;
        command.Parameters.AddWithValue("$url", url);
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }, "store embed");

    public int Count() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM embeds;";
        return Convert.ToInt32(command.ExecuteScalar());
    }, 0, "count embeds");

    public void Clear()
    {
        _embeds.Clear();
        _fetch.ClearFailures();

        _database.Write(connection => ChatboxDatabase.Execute(connection, "DELETE FROM embeds;"), "clear embeds");
    }

    public void PruneExpired()
    {
        var oldest = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _lifetimeDays())).ToUnixTimeSeconds();
        var oldestEmpty = DateTimeOffset.UtcNow.Subtract(EmptyLifetime).ToUnixTimeSeconds();

        _database.Write(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = PruneEmbedSql;
            command.Parameters.AddWithValue("$oldest", oldest);
            command.Parameters.AddWithValue("$oldestEmpty", oldestEmpty);
            command.ExecuteNonQuery();
        }, "prune embeds");
    }

    private void PruneUnresolved() => _database.Write(
        connection => ChatboxDatabase.Execute(connection, "DELETE FROM embeds WHERE length(payload) = 0;"),
        "prune unresolved embeds");

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fetch.Dispose();
        _embeds.Clear();
    }
}
