using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Cordi.UI.Panels;

public sealed class ChatboxInlineFlow
{
    private Vector2 _origin;
    private float _wrapWidth;
    private float _lineHeight;
    private float _lineSpacing;
    private float _x;
    private float _y;
    private bool _active;
    private bool _placed;

    public bool AnyHovered { get; private set; }

    public void Begin(float wrapWidth, float lineHeight, float lineSpacing)
    {
        _origin = ImGui.GetCursorScreenPos();
        _wrapWidth = MathF.Max(wrapWidth, 32f);
        _lineHeight = MathF.Max(lineHeight, ImGui.GetTextLineHeight());
        _lineSpacing = lineSpacing;
        _x = 0f;
        _y = 0f;
        _active = true;
        _placed = false;
        AnyHovered = false;
    }

    public void NewLine()
    {
        if (!_active) return;
        _x = 0f;
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
        if (_x > 0f && _x + width > _wrapWidth)
            NewLine();
    }

    private Vector2 Place(float width, float height)
    {
        var position = new Vector2(_origin.X + _x, _origin.Y + _y + (_lineHeight - height) * 0.5f);
        _x += width;
        _placed = true;
        return position;
    }

    public void Text(string text, Vector4 color)
    {
        if (!_active || string.IsNullOrEmpty(text)) return;

        foreach (var token in Tokenize(text))
            DrawToken(token, color);
    }

    private void DrawToken(string token, Vector4 color)
    {
        var size = ImGui.CalcTextSize(token);

        if (size.X > _wrapWidth)
        {
            DrawOversizedToken(token, color);
            return;
        }

        if (token.Trim().Length == 0 && _x == 0f) return;

        EnsureRoom(size.X);
        var position = Place(size.X, size.Y);

        ImGui.SetCursorScreenPos(position);
        ImGui.TextColored(color, token);
    }

    private void DrawOversizedToken(string token, Vector4 color)
    {
        var start = 0;
        while (start < token.Length)
        {
            var length = 1;
            while (start + length <= token.Length)
            {
                var candidate = ImGui.CalcTextSize(token.Substring(start, length)).X;
                if (_x + candidate > _wrapWidth) break;
                length++;
            }

            length = Math.Max(1, length - 1);
            var slice = token.Substring(start, length);
            var size = ImGui.CalcTextSize(slice);
            var position = Place(size.X, size.Y);
            ImGui.SetCursorScreenPos(position);
            ImGui.TextColored(color, slice);

            start += length;
            if (start < token.Length) NewLine();
        }
    }

    public void Image(IDalamudTextureWrap texture, float size, string tooltip)
    {
        if (!_active) return;

        EnsureRoom(size);
        var position = Place(size, size);

        ImGui.SetCursorScreenPos(position);
        ImGui.Image(texture.Handle, new Vector2(size, size));

        if (!ImGui.IsItemHovered()) return;

        AnyHovered = true;
        if (!string.IsNullOrEmpty(tooltip)) ImGui.SetTooltip(tooltip);
    }

    public void Placeholder(string text, float size, Vector4 color)
    {
        if (!_active) return;

        var textSize = ImGui.CalcTextSize(text);
        var width = MathF.Min(textSize.X, size * 3f);
        EnsureRoom(width);
        var position = Place(width, textSize.Y);

        ImGui.SetCursorScreenPos(position);
        ImGui.TextColored(color, text);
    }

    private static uint OutlineColor(float alpha) =>
        ImGui.GetColorU32(new Vector4(0f, 0f, 0f, Math.Clamp(alpha, 0f, 1f) * 0.9f));

    private static void DrawOutline(ImDrawListPtr draw, Vector2 position, string token, float alpha)
    {
        var shade = OutlineColor(alpha);

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                draw.AddText(position + new Vector2(dx, dy), shade, token);
            }
        }
    }

    public bool Pill(string text, Vector4 background, Vector4 foreground, float rounding)
    {
        if (!_active) return false;

        var padding = ImGui.GetStyle().FramePadding.X * 0.6f;
        var textSize = ImGui.CalcTextSize(text);
        var width = textSize.X + padding * 2f;

        EnsureRoom(width);
        var position = Place(width, _lineHeight);

        var draw = ImGui.GetWindowDrawList();
        var min = new Vector2(position.X, position.Y + (_lineHeight - textSize.Y) * 0.5f - 1f);
        var max = new Vector2(position.X + width, min.Y + textSize.Y + 2f);
        draw.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);

        ImGui.SetCursorScreenPos(new Vector2(position.X + padding, min.Y + 1f));
        ImGui.TextColored(foreground, text);

        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered) AnyHovered = true;
        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool Link(string text, Vector4 color, bool outlined = false)
    {
        if (!_active) return false;

        var clicked = false;
        foreach (var token in Tokenize(text))
        {
            var size = ImGui.CalcTextSize(token);
            EnsureRoom(size.X);
            var position = Place(size.X, size.Y);

            var draw = ImGui.GetWindowDrawList();
            if (outlined) DrawOutline(draw, position, token, color.W);

            ImGui.SetCursorScreenPos(position);
            ImGui.TextColored(color, token);

            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            var underlineStart = new Vector2(min.X, max.Y - 1f);
            var underlineEnd = new Vector2(max.X, max.Y - 1f);

            if (outlined)
                draw.AddLine(underlineStart + Vector2.One, underlineEnd + Vector2.One, OutlineColor(color.W));

            draw.AddLine(underlineStart, underlineEnd, ImGui.GetColorU32(color));

            if (!ImGui.IsItemHovered()) continue;

            AnyHovered = true;
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) clicked = true;
        }

        return clicked;
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var start = 0;
        while (start < text.Length)
        {
            var isSpace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == isSpace)
                end++;

            yield return text[start..end];
            start = end;
        }
    }
}
