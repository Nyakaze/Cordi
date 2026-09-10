using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public Vector2 NavBadgeSize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        var textSize = ImGui.CalcTextSize(text);
        var height = textSize.Y + ImGuiHelpers.GlobalScale;

        return new Vector2(MathF.Max(height, textSize.X + 5f * ImGuiHelpers.GlobalScale), height);
    }

    public float NavBadgeSpace(string? text)
    {
        var size = NavBadgeSize(text);
        return size.X > 0f ? size.X + Gap(0.4f) : 0f;
    }

    public void NavBadge(
        ImDrawListPtr draw,
        Vector2 itemMin,
        Vector2 itemMax,
        string? text,
        Vector4 color,
        UiNavBadgePlacement placement,
        float inset = 0f)
    {
        var size = NavBadgeSize(text);
        if (size.X <= 0f) return;

        var right = itemMax.X - inset;
        var bottom = placement == UiNavBadgePlacement.TopRight
            ? itemMin.Y + inset + size.Y
            : (itemMin.Y + itemMax.Y + size.Y) * 0.5f;

        var max = new Vector2(right, bottom);
        var min = max - size;
        var textSize = ImGui.CalcTextSize(text);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(color), size.Y * 0.5f);
        draw.AddText(min + (size - textSize) * 0.5f, 0xFFFFFFFF, text);
    }

    public UiNavHit NavRailTile(
        string id,
        float size,
        UiNavItem item,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        var origin = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(size, size));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(size, size);
        var rounding = item.Active || hovered ? size * 0.30f : size * 0.5f;
        var accent = item.Accent;

        var background = item.Active
            ? accent
            : hovered
                ? new Vector4(accent.X * 0.55f, accent.Y * 0.55f, accent.Z * 0.55f, 1f)
                : FrameBg;

        draw.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);

        if (item.Image != null)
        {
            onImage?.Invoke(item.Image, min, max);
            draw.AddImageRounded(item.Image.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFF, rounding);
        }
        else
        {
            var textSize = ImGui.CalcTextSize(item.Label);
            var foreground = item.Active ? new Vector4(1f, 1f, 1f, 1f) : Text;

            draw.AddText(min + (new Vector2(size, size) - textSize) * 0.5f, ImGui.GetColorU32(foreground), item.Label);
        }

        NavRailIndicator(draw, min, size, item, hovered);
        NavBadge(draw, min, max, item.BadgeText, item.BadgeColor, UiNavBadgePlacement.TopRight);

        return new UiNavHit { Clicked = clicked, Hovered = hovered };
    }

    public float NavIconSize() => ImGui.GetTextLineHeight();

    public float NavIconSpace(IDalamudTextureWrap? image) => image != null ? NavIconSize() + Gap(0.35f) : 0f;

    public float NavIconSpace(UiNavItem item) => NavIconSpace(item.Image);

    private void NavIcon(
        ImDrawListPtr draw,
        Vector2 min,
        float size,
        UiNavItem item,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage)
    {
        if (item.Image == null) return;

        var max = min + new Vector2(size, size);

        onImage?.Invoke(item.Image, min, max);
        draw.AddImageRounded(item.Image.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFF, Radius(0.35f));
    }

    public UiNavHit NavListRow(
        string id,
        float width,
        UiNavItem item,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        var height = ImGui.GetFrameHeight();
        var rowWidth = MathF.Max(width, 40f);
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton(id, new Vector2(rowWidth, height));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(rowWidth, height);

        if (item.Active || hovered)
            draw.AddRectFilled(min, max, ImGui.GetColorU32(item.Active ? Active : Hover), Radius(0.5f));

        var padding = PadX(0.6f);
        var textY = min.Y + (height - ImGui.GetTextLineHeight()) * 0.5f;

        if (item.Unread && item.ShowUnreadDot)
        {
            var radius = 3f * ImGuiHelpers.GlobalScale;

            draw.AddCircleFilled(
                new Vector2(min.X + radius + 1f, min.Y + height * 0.5f),
                radius,
                ImGui.GetColorU32(Text));
        }

        float leadWidth;

        if (item.Image != null)
        {
            var iconSize = NavIconSize();

            NavIcon(draw, new Vector2(min.X + padding, textY), iconSize, item, onImage);
            leadWidth = iconSize + Gap(0.35f);
        }
        else
        {
            const string prefix = "# ";

            draw.AddText(new Vector2(min.X + padding, textY), ImGui.GetColorU32(item.Accent), prefix);
            leadWidth = ImGui.CalcTextSize(prefix).X;
        }

        var nameColor = item.Active || item.Unread ? Text : MutedText;
        var nameX = min.X + padding + leadWidth;
        var name = Fit(item.Label, max.X - padding - NavBadgeSpace(item.BadgeText) - nameX);

        draw.AddText(new Vector2(nameX, textY), ImGui.GetColorU32(nameColor), name);

        NavBadge(draw, min, max, item.BadgeText, item.BadgeColor, UiNavBadgePlacement.MiddleRight, padding);

        return new UiNavHit { Clicked = clicked, Hovered = hovered };
    }

    public UiNavHit NavTab(
        string id,
        float width,
        UiNavItem item,
        float height = 0f,
        Action<IDalamudTextureWrap?, Vector2, Vector2>? onImage = null)
    {
        if (height <= 0f) height = ImGui.GetFrameHeight();
        var origin = ImGui.GetCursorScreenPos();

        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();

        var draw = ImGui.GetWindowDrawList();
        var min = origin;
        var max = origin + new Vector2(width, height);
        var background = item.Active ? TabActive : hovered ? TabHovered : Tab;

        draw.AddRectFilled(min, max, ImGui.GetColorU32(background), Radius(0.6f));

        if (item.Active)
        {
            var barHeight = 2f * ImGuiHelpers.GlobalScale;

            draw.AddRectFilled(
                new Vector2(min.X + PadX(0.4f), max.Y - barHeight),
                new Vector2(max.X - PadX(0.4f), max.Y),
                ImGui.GetColorU32(item.Accent),
                barHeight);
        }

        var badgeSpace = NavBadgeSpace(item.BadgeText);
        var iconSpace = NavIconSpace(item);
        var shown = Fit(item.Label, width - badgeSpace - iconSpace - PadX(0.6f));
        var textSize = ImGui.CalcTextSize(shown);
        var color = item.Active || item.Unread ? Text : MutedText;
        var groupX = min.X + MathF.Max(PadX(0.3f), (width - badgeSpace - iconSpace - textSize.X) * 0.5f);

        if (item.Image != null)
        {
            var iconSize = NavIconSize();

            NavIcon(draw, new Vector2(groupX, min.Y + (height - iconSize) * 0.5f), iconSize, item, onImage);
        }

        draw.AddText(
            new Vector2(groupX + iconSpace, min.Y + (height - textSize.Y) * 0.5f),
            ImGui.GetColorU32(color),
            shown);

        if (item.Unread && item.ShowUnreadDot && badgeSpace <= 0f)
        {
            var radius = 3f * ImGuiHelpers.GlobalScale;

            draw.AddCircleFilled(
                new Vector2(max.X - radius - 2f, min.Y + radius + 2f),
                radius,
                ImGui.GetColorU32(Text));
        }

        NavBadge(draw, min, max, item.BadgeText, item.BadgeColor, UiNavBadgePlacement.MiddleRight, PadX(0.3f));

        return new UiNavHit { Clicked = clicked, Hovered = hovered };
    }

    private void NavRailIndicator(ImDrawListPtr draw, Vector2 min, float size, UiNavItem item, bool hovered)
    {
        if (!item.Active && (!item.Unread || !item.ShowUnreadDot)) return;

        var width = 4f * ImGuiHelpers.GlobalScale;
        var height = item.Active ? size * 0.65f : hovered ? size * 0.4f : width * 2f;
        var top = min.Y + (size - height) * 0.5f;
        var left = min.X - width - 4f * ImGuiHelpers.GlobalScale;

        draw.AddRectFilled(
            new Vector2(left, top),
            new Vector2(left + width, top + height),
            ImGui.GetColorU32(Text),
            width * 0.5f);
    }
}
