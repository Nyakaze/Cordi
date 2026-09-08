using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;
using Microsoft.Data.Sqlite;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxImageCache : IDisposable
{
    private const int DisposeDelayFrames = 4;
    private const int MaxImageBytes = 8 * 1024 * 1024;

    private static readonly Regex EmoteFallbackRegex = new(
        @"^(?<base>https?://(?:cdn|media)\.discord(?:app)?\.(?:com|net)/emojis/\d{5,25})\.(?:gif|webp)(?:\?\S*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

    private readonly ChatboxDatabase _database;
    private readonly Func<int> _maxEntries;
    private readonly Func<bool> _animationEnabled;
    private readonly Func<int> _animationIdleSeconds;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> _textures = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _lastUsed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AnimatedTextureWrap> _animated = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _unloaded = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<(IDalamudTextureWrap Wrap, long Frame)> _graveyard = new();
    private readonly ChatboxFetchQueue _fetch = new(4, FailureBackoff, "Image load");
    private long _frame;
    private bool _disposed;

    public ChatboxImageCache(
        ChatboxDatabase database,
        Func<int> maxEntries,
        Func<bool> animationEnabled,
        Func<int> animationIdleSeconds)
    {
        _database = database;
        _maxEntries = maxEntries;
        _animationEnabled = animationEnabled;
        _animationIdleSeconds = animationIdleSeconds;
    }

    public int PendingDownloads => _fetch.Pending;
    public int FailedDownloads => _fetch.Failed;
    public int LoadedTextures => _textures.Count;
    public int AnimatedTextures => _animated.Count;
    public bool HasUnloaded => !_unloaded.IsEmpty;

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
        return client;
    }

    public IDalamudTextureWrap? Get(string? url)
    {
        if (_disposed || string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        if (_textures.TryGetValue(url, out var wrap))
        {
            _lastUsed[url] = Interlocked.Read(ref _frame);
            return wrap;
        }

        if (_unloaded.ContainsKey(url)) return null;

        _fetch.Enqueue(url, ResolveAsync);
        return null;
    }

    public void Request(string? url)
    {
        if (_unloaded.IsEmpty || string.IsNullOrEmpty(url)) return;

        _unloaded.TryRemove(url, out _);
    }

    public void Tick(float deltaSeconds, bool animate)
    {
        if (_disposed) return;

        var frame = Interlocked.Increment(ref _frame);

        if (animate && !_animated.IsEmpty)
        {
            var milliseconds = deltaSeconds * 1000d;
            foreach (var wrap in _animated.Values)
                wrap.Advance(milliseconds);
        }

        while (_graveyard.TryPeek(out var pending) && frame - pending.Frame >= DisposeDelayFrames)
        {
            if (!_graveyard.TryDequeue(out pending)) break;

            try { pending.Wrap.Dispose(); }
            catch (Exception ex) { Service.Log.Debug($"[Chatbox] Texture dispose failed: {ex.Message}"); }
        }

        if (frame % 60 == 0) EvictIdleAnimations(frame);
        if (frame % 120 == 0) EvictOverflow(frame);
    }

    private void EvictIdleAnimations(long frame)
    {
        if (_animated.IsEmpty) return;

        var seconds = _animationIdleSeconds();
        if (seconds <= 0) return;

        var cutoff = Environment.TickCount64 - seconds * 1000L;

        foreach (var pair in _animated)
        {
            if (pair.Value.LastSeenMs > cutoff) continue;
            if (!_animated.TryRemove(pair.Key, out _)) continue;

            _lastUsed.TryRemove(pair.Key, out _);
            _unloaded[pair.Key] = 0;

            if (_textures.TryRemove(pair.Key, out var texture))
                _graveyard.Enqueue((texture, frame));
        }
    }

    private void EvictOverflow(long frame)
    {
        var max = Math.Max(32, _maxEntries());
        if (_textures.Count <= max) return;

        var stale = _lastUsed
            .OrderBy(pair => pair.Value)
            .Take(_textures.Count - max)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var url in stale)
        {
            _lastUsed.TryRemove(url, out _);
            _animated.TryRemove(url, out _);
            if (_textures.TryRemove(url, out var wrap))
                _graveyard.Enqueue((wrap, frame));
        }
    }

    private async Task ResolveAsync(string url)
    {
        var bytes = ReadBlob(url);

        if (bytes is null)
        {
            bytes = await TryDownloadAsync(url).ConfigureAwait(false);

            if (bytes is null || bytes.Length == 0)
            {
                var fallback = EmoteFallbackUrl(url);
                if (fallback != null)
                    bytes = await TryDownloadAsync(fallback).ConfigureAwait(false);
            }

            if (bytes is null || bytes.Length == 0)
            {
                _fetch.MarkFailed(url);
                return;
            }

            StoreBlob(url, bytes);
        }

        var wrap = await CreateTextureWrapAsync(bytes, _fetch.Token).ConfigureAwait(false);
        if (wrap == null)
        {
            _fetch.MarkFailed(url);
            return;
        }

        if (_disposed)
        {
            wrap.Dispose();
            return;
        }

        _lastUsed[url] = Interlocked.Read(ref _frame);
        if (!_textures.TryAdd(url, wrap))
        {
            wrap.Dispose();
            return;
        }

        if (wrap is AnimatedTextureWrap animated)
            _animated[url] = animated;
    }

    private static string? EmoteFallbackUrl(string url)
    {
        var match = EmoteFallbackRegex.Match(url);

        return match.Success ? $"{match.Groups["base"].Value}.png?size=96" : null;
    }

    private static bool IsGifBytes(byte[] bytes)
    {
        return bytes.Length >= 6 &&
               bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' &&
               bytes[3] == (byte)'8' && (bytes[4] == (byte)'7' || bytes[4] == (byte)'9') && bytes[5] == (byte)'a';
    }

    private async Task<IDalamudTextureWrap?> CreateTextureWrapAsync(byte[] bytes, CancellationToken token)
    {
        if (_animationEnabled() && IsGifBytes(bytes))
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var image = System.Drawing.Image.FromStream(ms);
                var dimension = System.Drawing.Imaging.FrameDimension.Time;
                var frameCount = image.GetFrameCount(dimension);

                if (frameCount > 1)
                {
                    var delayProp = image.PropertyItems.FirstOrDefault(p => p.Id == 0x5100);
                    var delayBytes = delayProp?.Value;

                    var frames = new List<(IDalamudTextureWrap Wrap, int DelayMs)>(frameCount);
                    var maxFrames = Math.Min(frameCount, 120);

                    for (var i = 0; i < maxFrames; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        image.SelectActiveFrame(dimension, i);

                        using var frameBitmap = new System.Drawing.Bitmap(image.Width, image.Height);
                        using (var g = System.Drawing.Graphics.FromImage(frameBitmap))
                        {
                            g.DrawImage(image, 0, 0, image.Width, image.Height);
                        }

                        using var frameMs = new MemoryStream();
                        frameBitmap.Save(frameMs, System.Drawing.Imaging.ImageFormat.Png);
                        var frameBytes = frameMs.ToArray();

                        var frameWrap = await Service.TextureProvider
                            .CreateFromImageAsync(frameBytes, "Cordi.Chatbox", token)
                            .ConfigureAwait(false);

                        var delayMs = 100;
                        if (delayBytes != null && delayBytes.Length >= (i + 1) * 4)
                        {
                            var d = BitConverter.ToInt32(delayBytes, i * 4) * 10;
                            if (d > 0) delayMs = d;
                        }

                        frames.Add((frameWrap, Math.Max(20, delayMs)));
                    }

                    return new AnimatedTextureWrap(frames.ToArray());
                }
            }
            catch (Exception ex)
            {
                Service.Log.Debug($"[Chatbox] Animated GIF decode failed, falling back to static: {ex.Message}");
            }
        }

        return await Service.TextureProvider
            .CreateFromImageAsync(bytes, "Cordi.Chatbox", token)
            .ConfigureAwait(false);
    }

    private async Task<byte[]?> TryDownloadAsync(string url)
    {
        try
        {
            return await DownloadAsync(url).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Chatbox] Image download failed for {url}: {ex.Message}");
            return null;
        }
    }

    private async Task<byte[]?> DownloadAsync(string url)
    {
        if (!await _fetch.EnterGateAsync().ConfigureAwait(false))
            return null;

        try
        {
            using var response = await Http.GetAsync(url, _fetch.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > MaxImageBytes) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(_fetch.Token).ConfigureAwait(false);
            return bytes.Length is > 0 and <= MaxImageBytes ? bytes : null;
        }
        finally
        {
            _fetch.ExitGate();
        }
    }

    private byte[]? ReadBlob(string url) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.bytes FROM images AS i
            JOIN blobs AS b ON b.hash = i.hash
            WHERE i.url = $url;
            """;
        command.Parameters.AddWithValue("$url", url);

        var value = command.ExecuteScalar();
        if (value is not byte[] bytes || bytes.Length == 0) return null;

        Touch(connection, url);
        return bytes;
    }, null, "read image");

    private static void Touch(SqliteConnection connection, string url)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE images SET last_used = $now WHERE url = $url;";
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$url", url);
        command.ExecuteNonQuery();
    }

    private void StoreBlob(string url, byte[] bytes)
    {
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        _database.Transact(connection =>
        {
            using var blob = connection.CreateCommand();
            blob.CommandText = "INSERT OR IGNORE INTO blobs(hash, bytes, size) VALUES ($hash, $bytes, $size);";
            blob.Parameters.AddWithValue("$hash", hash);
            blob.Parameters.AddWithValue("$bytes", bytes);
            blob.Parameters.AddWithValue("$size", bytes.Length);
            blob.ExecuteNonQuery();

            using var image = connection.CreateCommand();
            image.CommandText = """
                INSERT INTO images(url, hash, fetched_at, last_used)
                VALUES ($url, $hash, $now, $now)
                ON CONFLICT(url) DO UPDATE SET hash = $hash, fetched_at = $now, last_used = $now;
                """;
            image.Parameters.AddWithValue("$url", url);
            image.Parameters.AddWithValue("$hash", hash);
            image.Parameters.AddWithValue("$now", now);
            image.ExecuteNonQuery();
        }, "store image");
    }

    public void PruneStored(int maxEntries)
    {
        if (maxEntries <= 0) return;

        _database.Transact(connection =>
        {
            using var images = connection.CreateCommand();
            images.CommandText = """
                DELETE FROM images WHERE url IN (
                    SELECT url FROM images ORDER BY last_used DESC LIMIT -1 OFFSET $limit
                );
                """;
            images.Parameters.AddWithValue("$limit", maxEntries);
            images.ExecuteNonQuery();

            ChatboxDatabase.Execute(connection,
                "DELETE FROM blobs WHERE hash NOT IN (SELECT hash FROM images);");
        }, "prune images");
    }

    public (int Entries, int UniqueBlobs, long Bytes) Inspect() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT (SELECT COUNT(*) FROM images),
                   (SELECT COUNT(*) FROM blobs),
                   (SELECT IFNULL(SUM(size), 0) FROM blobs);
            """;

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? (reader.GetInt32(0), reader.GetInt32(1), reader.GetInt64(2))
            : (0, 0, 0L);
    }, (0, 0, 0L), "inspect images");

    public string InspectSummary()
    {
        var (entries, blobs, bytes) = Inspect();
        var megabytes = (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture);
        var deduped = entries - blobs;

        return deduped > 0
            ? $"{entries} image(s), {megabytes} MB ({deduped} deduplicated)"
            : $"{entries} image(s), {megabytes} MB";
    }

    public void ResetTextures()
    {
        if (_disposed) return;

        var frame = Interlocked.Read(ref _frame);
        _animated.Clear();
        _unloaded.Clear();

        foreach (var url in _textures.Keys.ToArray())
        {
            if (_textures.TryRemove(url, out var wrap))
                _graveyard.Enqueue((wrap, frame));
        }

        _lastUsed.Clear();
        _fetch.ClearFailures();
    }

    public void Clear()
    {
        _fetch.ClearFailures();
        _animated.Clear();
        _unloaded.Clear();

        var frame = Interlocked.Read(ref _frame);
        foreach (var url in _textures.Keys.ToArray())
        {
            if (_textures.TryRemove(url, out var wrap))
                _graveyard.Enqueue((wrap, frame));
        }

        _lastUsed.Clear();

        _database.Transact(connection =>
        {
            ChatboxDatabase.Execute(connection, "DELETE FROM images;");
            ChatboxDatabase.Execute(connection, "DELETE FROM blobs;");
        }, "clear images");
    }

    public static long RemoveLegacyDirectory(string configDirectory)
    {
        var legacy = Path.Combine(configDirectory, "chatbox-images");

        try
        {
            if (!Directory.Exists(legacy)) return 0;

            var files = Directory.GetFiles(legacy);
            var bytes = files.Sum(file => new FileInfo(file).Length);
            Directory.Delete(legacy, true);
            return bytes;
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[Chatbox] Legacy image cache cleanup failed: {ex.Message}");
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _fetch.Cancel();

        foreach (var wrap in _textures.Values)
        {
            try { wrap.Dispose(); }
            catch (Exception ex) { Service.Log.Debug($"[Chatbox] Texture dispose failed: {ex.Message}"); }
        }

        _textures.Clear();
        _animated.Clear();
        _unloaded.Clear();

        while (_graveyard.TryDequeue(out var pending))
        {
            try { pending.Wrap.Dispose(); }
            catch (Exception ex) { Service.Log.Debug($"[Chatbox] Texture dispose failed: {ex.Message}"); }
        }

        _fetch.Dispose();
    }
}
