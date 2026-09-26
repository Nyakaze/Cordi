using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Panels;

public sealed class RowSegment
{
    public string Text = string.Empty;
    public uint Color;
    public float Width;
    public Action<Vector2>? Draw;
}

public static class RowRenderer
{
    public static void DrawLeft(ImDrawListPtr list, Vector2 origin, IReadOnlyList<RowSegment> segments, float gap, bool shadow, Themes.UiTheme theme)
    {
        float x = origin.X;

        foreach (var segment in segments)
        {
            Paint(list, new Vector2(x, origin.Y), segment, shadow, theme);
            x += segment.Width + gap;
        }
    }

    public static void DrawRight(ImDrawListPtr list, Vector2 rightAnchor, IReadOnlyList<RowSegment> segments, float gap, bool shadow, Themes.UiTheme theme)
    {
        float x = rightAnchor.X;

        for (int i = segments.Count - 1; i >= 0; i--)
        {
            x -= segments[i].Width;
            Paint(list, new Vector2(x, rightAnchor.Y), segments[i], shadow, theme);
            x -= gap;
        }
    }

    private static void Paint(ImDrawListPtr list, Vector2 pos, RowSegment segment, bool shadow, Themes.UiTheme theme)
    {
        if (segment.Draw != null)
        {
            segment.Draw(pos);
            return;
        }

        theme.ShadowedTextAt(list, pos, segment.Text, segment.Color, shadow);
    }
}
