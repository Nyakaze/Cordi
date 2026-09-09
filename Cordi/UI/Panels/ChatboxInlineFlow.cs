using System;
using System.Numerics;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.UI.Panels;

public sealed class ChatboxInlineFlow
{
    private readonly UiTheme _theme;

    private ImDrawListPtr _draw;
    private Vector2 _origin;
    private float _wrapWidth;
    private float _lineHeight;
    private float _lineSpacing;
    private float _indent;
    private float _x;
    private float _y;
    private float _clipTop;
    private float _clipBottom;
    private bool _active;
    private bool _placed;

    public bool AnyHovered { get; private set; }

    public ChatboxInlineFlow(UiTheme theme)
    {
        _theme = theme;
    }

    public void Begin(float wrapWidth, float lineHeight, float lineSpacing)
    {
        _draw = ImGui.GetWindowDrawList();
        _origin = ImGui.GetCursorScreenPos();
        _wrapWidth = MathF.Max(wrapWidth, 32f);
        _lineHeight = MathF.Max(lineHeight, ImGui.GetTextLineHeight());
        _lineSpacing = lineSpacing;
        _indent = 0f;
        _x = 0f;
        _y = 0f;
        _active = true;
        _placed = false;
        AnyHovered = false;

        var top = ImGui.GetWindowPos().Y;
        _clipTop = top;
        _clipBottom = top + ImGui.GetWindowSize().Y;
    }

    public void Indent(float offset)
    {
        if (!_active) return;

        _indent = Math.Clamp(offset, 0f, _wrapWidth * 0.5f);
        if (_x < _indent) _x = _indent;
    }

    public void NewLine()
    {
        if (!_active) return;
        _x = _indent;
        _y += _lineHeight + _lineSpacing;
    }

    public float End()
    {
        if (!_active) return 0f;
        _active = false;

        if (!_placed) return 0f;

        var height = _y + _lineHeight;

        ImGui.SetCursorScreenPos(_origin);
        ImGui.Dummy(new Vector2(_wrapWidth, height));
        return height;
    }

    private void EnsureRoom(float width)
    {
        if (_x > _indent && _x + width > _wrapWidth)
            NewLine();
    }

    private Vector2 Place(float width, float height)
    {
        var position = new Vector2(_origin.X + _x, _origin.Y + _y + (_lineHeight - height) * 0.5f);
        _x += width;
        _placed = true;
        return position;
    }

    private void Emit(ReadOnlySpan<char> text, Vector4 color, Vector2 size)
    {
        var position = Place(size.X, size.Y);
        if (position.Y + size.Y < _clipTop || position.Y > _clipBottom) return;

        _draw.AddText(position, ImGui.GetColorU32(color), text);
    }

    public void Text(string text, Vector4 color)
    {
        if (!_active || string.IsNullOrEmpty(text)) return;

        var span = text.AsSpan();
        if (_x <= _indent) span = span.TrimStart();
        if (span.IsEmpty) return;

        var size = ImGui.CalcTextSize(span);
        if (_x + size.X <= _wrapWidth)
        {
            Emit(span, color, size);
            return;
        }

        var start = 0;
        while (start < span.Length)
        {
            var end = TokenEnd(span, start);
            DrawToken(span[start..end], color);
            start = end;
        }
    }

    private static int TokenEnd(ReadOnlySpan<char> text, int start)
    {
        var isSpace = char.IsWhiteSpace(text[start]);
        var end = start + 1;
        while (end < text.Length && char.IsWhiteSpace(text[end]) == isSpace)
            end++;

        return end;
    }

    private void DrawToken(ReadOnlySpan<char> token, Vector4 color)
    {
        var size = ImGui.CalcTextSize(token);

        if (size.X > _wrapWidth)
        {
            DrawOversizedToken(token, color);
            return;
        }

        if (_x <= _indent && token.IsWhiteSpace()) return;

        EnsureRoom(size.X);
        Emit(token, color, size);
    }

    private void DrawOversizedToken(ReadOnlySpan<char> token, Vector4 color)
    {
        while (!token.IsEmpty)
        {
            var length = FitLength(token, _wrapWidth - _x);
            var slice = token[..length];

            Emit(slice, color, ImGui.CalcTextSize(slice));

            token = token[length..];
            if (!token.IsEmpty) NewLine();
        }
    }

    private static int FitLength(ReadOnlySpan<char> token, float available)
    {
        var low = 1;
        var high = token.Length;
        var best = 1;

        while (low <= high)
        {
            var mid = low + (high - low) / 2;
            if (ImGui.CalcTextSize(token[..mid]).X <= available)
            {
                best = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (best < token.Length && char.IsHighSurrogate(token[best - 1]))
            best = Math.Max(1, best - 1);

        return best;
    }

    public void Image(IDalamudTextureWrap texture, float size, string tooltip)
    {
        if (!_active) return;

        EnsureRoom(size);
        var position = Place(size, size);

        ImGui.SetCursorScreenPos(position);
        AnimatedTextureWrap.MarkVisible(texture, new Vector2(size, size));
        ImGui.Image(texture.Handle, new Vector2(size, size));

        if (!ImGui.IsItemHovered()) return;

        AnyHovered = true;
        if (!string.IsNullOrEmpty(tooltip)) ImGui.SetTooltip(tooltip);
    }

    public void Icon(IDalamudTextureWrap texture, Vector2 size, Vector2 uv0, Vector2 uv1)
    {
        if (!_active) return;

        EnsureRoom(size.X);
        var position = Place(size.X, size.Y);
        if (position.Y + size.Y < _clipTop || position.Y > _clipBottom) return;

        _draw.AddImage(texture.Handle, position, position + size, uv0, uv1);
    }

    public void Placeholder(string text, float size, Vector4 color)
    {
        if (!_active) return;

        var textSize = ImGui.CalcTextSize(text);
        var width = MathF.Min(textSize.X, size * 3f);
        EnsureRoom(width);

        var position = Place(width, textSize.Y);
        if (position.Y + textSize.Y < _clipTop || position.Y > _clipBottom) return;

        _draw.AddText(position, ImGui.GetColorU32(color), text);
    }

    private void DrawOutline(ImDrawListPtr draw, Vector2 position, ReadOnlySpan<char> token, float alpha) =>
        _theme.TextOutline(draw, position, token, alpha);

    public bool Pill(string text, Vector4 background, Vector4 foreground, float rounding) =>
        Pill(text, background, foreground, rounding, out _);

    public bool Pill(
        string text,
        Vector4 background,
        Vector4 foreground,
        float rounding,
        out bool hovered,
        bool backgroundOnHover = false)
    {
        hovered = false;
        if (!_active) return false;

        var width = _theme.ChipInlineWidth(text);

        EnsureRoom(width);
        var position = Place(width, _lineHeight);
        var bounds = _theme.ChipInlineBounds(text, position, _lineHeight);

        hovered = ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max);

        if (bounds.Min.Y > _clipBottom || bounds.Max.Y < _clipTop) return false;

        _theme.ChipInline(
            _draw,
            bounds,
            text,
            foreground,
            !backgroundOnHover || hovered ? background : null,
            rounding);

        if (hovered) AnyHovered = true;
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool Link(string text, Vector4 color, bool outlined = false)
    {
        if (!_active) return false;

        var span = text.AsSpan();
        var clicked = false;
        var start = 0;

        while (start < span.Length)
        {
            var end = TokenEnd(span, start);
            if (DrawLinkToken(span[start..end], color, outlined)) clicked = true;
            start = end;
        }

        return clicked;
    }

    private bool DrawLinkToken(ReadOnlySpan<char> token, Vector4 color, bool outlined)
    {
        var size = ImGui.CalcTextSize(token);
        EnsureRoom(size.X);
        var position = Place(size.X, size.Y);

        if (position.Y + size.Y < _clipTop || position.Y > _clipBottom) return false;

        if (outlined) DrawOutline(_draw, position, token, color.W);

        ImGui.SetCursorScreenPos(position);
        ImGui.TextColored(color, token);

        _theme.TextUnderline(_draw, ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), color, outlined);

        if (!ImGui.IsItemHovered()) return false;

        AnyHovered = true;
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
