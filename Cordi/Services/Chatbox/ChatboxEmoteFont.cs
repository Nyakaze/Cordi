using System;
using System.Numerics;
using Dalamud.Interface.ManagedFontAtlas;

namespace Cordi.Services.Chatbox;

public sealed class ChatboxEmoteFont : IDisposable
{
    public const char PoolStart = (char)0xE900;
    public const int PoolSize = 64;

    private readonly IFontAtlas _atlas;
    private readonly IFontHandle _handle;
    private readonly string?[] _tokens = new string?[PoolSize];
    private readonly string?[] _urls = new string?[PoolSize];

    private int _next;

    public ChatboxEmoteFont()
    {
        _atlas = Service.PluginInterface.UiBuilder.CreateFontAtlas(
            FontAtlasAutoRebuildMode.Async,
            true,
            "Cordi.ChatboxEmotes");

        _handle = _atlas.NewDelegateFontHandle(e => e.OnPreBuild(tk =>
        {
            var font = tk.AddDalamudDefaultFont(-1);
            var box = (int)MathF.Ceiling(Service.PluginInterface.UiBuilder.FontDefaultSizePx * tk.Scale);
            if (box < 1) box = 1;

            for (var i = 0; i < PoolSize; i++)
            {
                tk.NewImAtlas.AddCustomRectFontGlyph(
                    font,
                    (ushort)(PoolStart + i),
                    box,
                    box,
                    box,
                    new Vector2(0, box * 0.1f));
            }
        }));
    }

    public bool Available => _handle.Available;

    public IDisposable Push() => _handle.Push();

    public static bool IsSlot(char c) => c >= PoolStart && c < PoolStart + PoolSize;

    public char Reserve(string token, string? url)
    {
        for (var i = 0; i < PoolSize; i++)
        {
            if (!string.Equals(_tokens[i], token, StringComparison.Ordinal)) continue;

            _urls[i] = url;
            return (char)(PoolStart + i);
        }

        var slot = _next;
        _next = (_next + 1) % PoolSize;

        _tokens[slot] = token;
        _urls[slot] = url;

        return (char)(PoolStart + slot);
    }

    public bool TryResolve(char c, out string token, out string? url)
    {
        token = string.Empty;
        url = null;

        if (!IsSlot(c)) return false;

        var stored = _tokens[c - PoolStart];
        if (stored == null) return false;

        token = stored;
        url = _urls[c - PoolStart];

        return true;
    }

    public string Expand(string text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

        var builder = new System.Text.StringBuilder(text.Length + 16);

        foreach (var c in text)
        {
            if (TryResolve(c, out var token, out _)) builder.Append(token);
            else builder.Append(c);
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        _handle.Dispose();
        _atlas.Dispose();
    }
}
