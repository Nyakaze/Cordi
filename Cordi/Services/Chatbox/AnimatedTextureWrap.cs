using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.Services.Chatbox;

public sealed class AnimatedTextureWrap : IDalamudTextureWrap
{
    private const int MaxStepsPerAdvance = 64;
    private const double MaxDeltaMs = 250d;

    private readonly (IDalamudTextureWrap Wrap, int DelayMs)[] _frames;
    private double _elapsedMs;
    private long _lastSeenMs;
    private int _index;
    private bool _requested;
    private bool _disposed;

    public AnimatedTextureWrap((IDalamudTextureWrap Wrap, int DelayMs)[] frames)
    {
        if (frames.Length == 0) throw new ArgumentException("At least one frame is required.", nameof(frames));
        _frames = frames;
        _lastSeenMs = Environment.TickCount64;
    }

    public long LastSeenMs => _lastSeenMs;

    public int FrameCount => _frames.Length;

    public IDalamudTextureWrap CurrentWrap => _frames[Math.Min(_index, _frames.Length - 1)].Wrap;

    public ImTextureID Handle => CurrentWrap.Handle;
    public int Width => CurrentWrap.Width;
    public int Height => CurrentWrap.Height;

    public static void MarkVisible(IDalamudTextureWrap? texture, Vector2 size)
    {
        if (texture is not AnimatedTextureWrap animated) return;
        if (!ImGui.IsRectVisible(size)) return;

        animated.Touch();
    }

    public static void MarkVisible(IDalamudTextureWrap? texture, Vector2 min, Vector2 max)
    {
        if (texture is not AnimatedTextureWrap animated) return;
        if (!ImGui.IsRectVisible(min, max)) return;

        animated.Touch();
    }

    private void Touch()
    {
        _requested = true;
        _lastSeenMs = Environment.TickCount64;
    }

    public void Advance(double milliseconds)
    {
        if (_disposed || _frames.Length < 2) return;

        if (!_requested)
        {
            _elapsedMs = 0d;
            return;
        }

        _requested = false;

        _elapsedMs += Math.Clamp(milliseconds, 0d, MaxDeltaMs);

        var steps = 0;
        while (_elapsedMs >= _frames[_index].DelayMs && steps++ < MaxStepsPerAdvance)
        {
            _elapsedMs -= _frames[_index].DelayMs;
            _index = (_index + 1) % _frames.Length;
        }

        if (steps >= MaxStepsPerAdvance) _elapsedMs = 0d;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var frame in _frames)
        {
            try { frame.Wrap.Dispose(); }
            catch (Exception ex) { Service.Log.Debug($"[Chatbox] Frame dispose failed: {ex.Message}"); }
        }
    }
}
