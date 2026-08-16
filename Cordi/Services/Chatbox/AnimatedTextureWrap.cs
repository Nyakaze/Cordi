using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.Services.Chatbox;

public sealed class AnimatedTextureWrap : IDalamudTextureWrap
{
    private readonly (IDalamudTextureWrap Wrap, int DelayMs)[] _frames;
    private readonly int _totalDurationMs;
    private readonly long _startTime;
    private bool _disposed;

    public AnimatedTextureWrap((IDalamudTextureWrap Wrap, int DelayMs)[] frames)
    {
        _frames = frames;
        var total = 0;
        foreach (var frame in frames)
            total += frame.DelayMs;
        _totalDurationMs = Math.Max(1, total);
        _startTime = Environment.TickCount64;
    }

    public IDalamudTextureWrap CurrentWrap
    {
        get
        {
            if (_frames.Length == 0) throw new ObjectDisposedException(nameof(AnimatedTextureWrap));
            if (_frames.Length == 1) return _frames[0].Wrap;

            var elapsed = (int)((Environment.TickCount64 - _startTime) % _totalDurationMs);
            var acc = 0;
            for (var i = 0; i < _frames.Length; i++)
            {
                acc += _frames[i].DelayMs;
                if (elapsed < acc)
                    return _frames[i].Wrap;
            }
            return _frames[^1].Wrap;
        }
    }

    public ImTextureID Handle => CurrentWrap.Handle;
    public int Width => CurrentWrap.Width;
    public int Height => CurrentWrap.Height;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var frame in _frames)
        {
            try { frame.Wrap.Dispose(); }
            catch { }
        }
    }
}
