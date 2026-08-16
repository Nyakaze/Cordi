using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly ChatboxDatabase _database;
    private readonly Func<int> _lifetimeDays;
    private readonly ConcurrentDictionary<string, ChatboxLinkEmbed> _embeds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _inFlight = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTime> _failures = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _fetchGate = new(2, 2);
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public ChatboxEmbedCache(ChatboxDatabase database, Func<int> lifetimeDays)
    {
        _database = database;
        _lifetimeDays = lifetimeDays;
    }

    public int PendingFetches => _inFlight.Count;

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

        Enqueue(key);
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

    private void Enqueue(string url)
    {
        if (_failures.TryGetValue(url, out var failedAt))
        {
            if (DateTime.UtcNow - failedAt < FailureBackoff) return;
            _failures.TryRemove(url, out _);
        }

        if (!_inFlight.TryAdd(url, 0)) return;

        _ = Task.Run(() => ResolveAsync(url), _cts.Token);
    }

    private async Task ResolveAsync(string url)
    {
        try
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
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _failures[url] = DateTime.UtcNow;
            Service.Log.Debug($"[Chatbox] Embed lookup failed for {url}: {ex.Message}");
        }
        finally
        {
            _inFlight.TryRemove(url, out _);
        }
    }

    private async Task<ChatboxLinkEmbed?> FetchAsync(string url)
    {
        try
        {
            await _fetchGate.WaitAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            using var response = await Http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cts.Token)
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
            try { _fetchGate.Release(); }
            catch (ObjectDisposedException) { }
        }
    }

    private async Task<string> ReadCappedAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(_cts.Token).ConfigureAwait(false);

        var buffer = new byte[8192];
        using var memory = new MemoryStream();

        while (memory.Length < MaxHtmlBytes)
        {
            var read = await stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
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

        return _database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT payload FROM embeds WHERE url = $url AND fetched_at >= $oldest;";
            command.Parameters.AddWithValue("$url", url);
            command.Parameters.AddWithValue("$oldest", oldest);

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
        _failures.Clear();

        _database.Write(connection => ChatboxDatabase.Execute(connection, "DELETE FROM embeds;"), "clear embeds");
    }

    public void PruneExpired()
    {
        var oldest = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _lifetimeDays())).ToUnixTimeSeconds();

        _database.Write(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM embeds WHERE fetched_at < $oldest;";
            command.Parameters.AddWithValue("$oldest", oldest);
            command.ExecuteNonQuery();
        }, "prune embeds");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
        _fetchGate.Dispose();
        _embeds.Clear();
    }
}
