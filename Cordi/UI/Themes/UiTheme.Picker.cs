using System;
using System.Numerics;
using Cordi.Configuration;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;

public sealed partial class UiTheme
{
    public float PickerGripSize() => Scaled(EmojiPickerGripSize);

    public Vector2 PickerMinSize() => new(Scaled(EmojiPickerMinWidth), Scaled(EmojiPickerMinHeight));

    public Vector2 ClampPickerSize(Vector2 size)
    {
        var min = PickerMinSize();

        return Vector2.Clamp(size, min, Vector2.Max(min, ImGui.GetMainViewport().WorkSize));
    }

    public Vector2 PickerInitialSize(Vector2 chatboxSize, ChatboxEmojiPickerPosition side) => ClampPickerSize(side switch
    {
        ChatboxEmojiPickerPosition.Top or ChatboxEmojiPickerPosition.Bottom
            => new Vector2(chatboxSize.X, Scaled(EmojiPickerHeight)),
        _ => new Vector2(Scaled(EmojiPickerWidth), chatboxSize.Y),
    });

    public void PreparePickerPopup(Vector2 chatboxPosition, Vector2 chatboxSize, ChatboxEmojiPickerPosition side, Vector2 size)
    {
        var viewport = ImGui.GetMainViewport();
        var gap = Gap(0.5f);
        var position = side switch
        {
            ChatboxEmojiPickerPosition.Left => new Vector2(chatboxPosition.X - size.X - gap, chatboxPosition.Y),
            ChatboxEmojiPickerPosition.Top => new Vector2(chatboxPosition.X, chatboxPosition.Y - size.Y - gap),
            ChatboxEmojiPickerPosition.Bottom => new Vector2(chatboxPosition.X, chatboxPosition.Y + chatboxSize.Y + gap),
            _ => new Vector2(chatboxPosition.X + chatboxSize.X + gap, chatboxPosition.Y),
        };

        position = Vector2.Clamp(position,
            viewport.WorkPos,
            Vector2.Max(viewport.WorkPos, viewport.WorkPos + viewport.WorkSize - size));

        ImGui.SetNextWindowViewport(viewport.ID);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.SetNextWindowPos(position, ImGuiCond.Always);
    }

    public bool PickerResizeGrip(string id, ChatboxEmojiPickerPosition side, ref Vector2 size)
    {
        var grip = PickerGripSize();
        var padding = ImGui.GetStyle().WindowPadding;
        var min = ImGui.GetWindowPos() + padding;
        var max = min + ImGui.GetWindowSize() - padding * 2f;
        var flipX = side == ChatboxEmojiPickerPosition.Left;
        var flipY = side == ChatboxEmojiPickerPosition.Top;
        var corner = new Vector2(flipX ? min.X : max.X, flipY ? min.Y : max.Y);
        var step = new Vector2(flipX ? grip : -grip, flipY ? grip : -grip);
        var cursor = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(Vector2.Min(corner, corner + step));
        ImGui.InvisibleButton(id, new Vector2(grip, grip));

        var hovered = ImGui.IsItemHovered();
        var active = ImGui.IsItemActive();
        var released = ImGui.IsItemDeactivated();

        ImGui.SetCursorScreenPos(cursor);

        if (hovered || active)
            ImGui.SetMouseCursor(flipX != flipY ? ImGuiMouseCursor.ResizeNesw : ImGuiMouseCursor.ResizeNwse);

        if (active)
        {
            var delta = ImGui.GetIO().MouseDelta;

            size = ClampPickerSize(size + new Vector2(flipX ? -delta.X : delta.X, flipY ? -delta.Y : delta.Y));
        }

        var draw = ImGui.GetWindowDrawList();
        var color = ImGui.GetColorU32(active ? Accent : hovered ? AccentHover : Border);

        for (var i = 1; i <= 3; i++)
        {
            var fraction = i / 3f;

            draw.AddLine(
                new Vector2(corner.X + step.X * fraction, corner.Y),
                new Vector2(corner.X, corner.Y + step.Y * fraction),
                color,
                1.5f * ImGuiHelpers.GlobalScale);
        }

        return released;
    }

    public IDisposable PickerPopupScope(float opacity)
    {
        var style = ImRaii.PushStyle(ImGuiStyleVar.PopupRounding, Radius(1.2f))
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(PadX(0.8f), PadY(0.8f)))
            .Push(ImGuiStyleVar.WindowBorderSize, 1f * ImGuiHelpers.GlobalScale)
            .Push(ImGuiStyleVar.ItemSpacing, new Vector2(Gap(0.35f), Gap(0.35f)));

        var background = WindowBg;
        background.W *= Math.Clamp(opacity, 0f, 1f);
        var color = ImRaii.PushColor(ImGuiCol.PopupBg, background)
            .Push(ImGuiCol.Border, WindowBorder);

        return new ActionDisposable(() =>
        {
            color.Dispose();
            style.Dispose();
        });
    }

    public float PickerSearchHeight() => Scaled(30f);

    public float PickerChipHeight() => Scaled(24f);

    public float PickerChipWidth(ReadOnlySpan<char> label) => ChipPillWidth(label, 0.82f) + PadX(0.4f);

    public void PickerSearch(string id, float width, ref string value, string hint, ref bool inputActive)
    {
        var draw = ImGui.GetWindowDrawList();
        var height = PickerSearchHeight();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);

        draw.AddRectFilled(min, max, ImGui.GetColorU32(FrameBg), Radius());
        draw.AddRect(min, max, ImGui.GetColorU32(inputActive ? AccentBorder : Border), Radius());

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, FaintText))
        {
            var glyph = FontAwesomeIcon.Search.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(min.X + PadX(0.8f), min.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        var inputX = min.X + PadX(0.8f) + Scaled(20f);
        var clearWidth = value.Length > 0 ? Scaled(24f) : 0f;
        var inputWidth = MathF.Max(Scaled(40f), max.X - PadX(0.5f) - clearWidth - inputX);

        ImGui.SetCursorScreenPos(new Vector2(inputX, min.Y + (height - ImGui.GetFrameHeight()) * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);

        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.FrameBgHovered, new Vector4(0f, 0f, 0f, 0f))
                   .Push(ImGuiCol.FrameBgActive, new Vector4(0f, 0f, 0f, 0f)))
        using (ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 0f))
        {
            ImGui.InputTextWithHint(id, hint, ref value, 64);
        }

        inputActive = ImGui.IsItemActive();

        if (clearWidth > 0f)
        {
            var clearMin = new Vector2(max.X - PadX(0.35f) - clearWidth, min.Y);

            ImGui.SetCursorScreenPos(clearMin);
            if (ImGui.InvisibleButton($"{id}-clear", new Vector2(clearWidth, height))) value = string.Empty;

            var clearHovered = ImGui.IsItemHovered();
            if (clearHovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            using (ImRaii.PushFont(UiBuilder.IconFont))
            using (ImRaii.PushColor(ImGuiCol.Text, clearHovered ? Text : FaintText))
            {
                var glyph = FontAwesomeIcon.Times.ToIconString();
                var size = ImGui.CalcTextSize(glyph);
                ImGui.SetCursorScreenPos(new Vector2(clearMin.X + (clearWidth - size.X) * 0.5f, min.Y + (height - size.Y) * 0.5f));
                ImGui.TextUnformatted(glyph);
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(min.X, max.Y));
    }

    public bool PickerChip(string id, Vector2 pos, string label, bool active)
    {
        var height = PickerChipHeight();
        var width = PickerChipWidth(label);

        ImGui.SetCursorScreenPos(pos);
        ApplyFontScale(0.82f);
        try
        {
            var hit = NavTab(id, width, new UiNavItem
            {
                Label = label,
                Active = active,
                Accent = Accent,
            }, height);
            if (hit.Hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            return hit.Clicked;
        }
        finally
        {
            ApplyFontScale();
        }
    }

    public void PickerSectionHeader(string label, string trailing, float width)
    {
        var draw = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var lineHeight = ImGui.GetTextLineHeight();
        var barWidth = Scaled(3f);

        draw.AddRectFilled(
            new Vector2(origin.X, origin.Y + lineHeight * 0.14f),
            new Vector2(origin.X + barWidth, origin.Y + lineHeight * 0.86f),
            ImGui.GetColorU32(Accent),
            barWidth * 0.5f);

        var textX = origin.X + barWidth + PadX(0.5f);
        var text = label.ToUpperInvariant();

        ApplyFontScale(0.86f);
        var textSize = ImGui.CalcTextSize(text);

        using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(textX, origin.Y + (lineHeight - textSize.Y) * 0.5f));
            ImGui.TextUnformatted(text);
        }

        var trailingWidth = 0f;

        if (trailing.Length > 0)
        {
            var size = ImGui.CalcTextSize(trailing);
            trailingWidth = size.X + PadX(0.6f);

            using (ImRaii.PushColor(ImGuiCol.Text, FaintText))
            {
                ImGui.SetCursorScreenPos(new Vector2(origin.X + width - size.X, origin.Y + (lineHeight - size.Y) * 0.5f));
                ImGui.TextUnformatted(trailing);
            }
        }

        ApplyFontScale();

        var ruleX = textX + textSize.X + PadX(0.6f);
        var ruleWidth = origin.X + width - trailingWidth - ruleX;

        if (ruleWidth > Scaled(8f))
            RuleH(draw, new Vector2(ruleX, origin.Y + (lineHeight - RuleThickness()) * 0.5f), ruleWidth, Border);

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + lineHeight + Gap(0.3f)));
    }

    public bool PickerCollapsible(string id, string label, bool open, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Scaled(26f);
        var max = origin + new Vector2(width, height);

        ImGui.SetCursorScreenPos(origin);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

        ImGui.GetWindowDrawList().AddRectFilled(
            origin,
            max,
            ImGui.GetColorU32(hovered ? RowHover : RowBg),
            Radius(0.5f));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, FaintText))
        {
            var glyph = (open ? FontAwesomeIcon.ChevronDown : FontAwesomeIcon.ChevronRight).ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(origin.X + PadX(0.6f), origin.Y + (height - size.Y) * 0.5f));
            ImGui.TextUnformatted(glyph);
        }

        var textSize = ImGui.CalcTextSize(label);

        using (ImRaii.PushColor(ImGuiCol.Text, hovered ? Text : MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X + PadX(0.6f) + Scaled(18f), origin.Y + (height - textSize.Y) * 0.5f));
            ImGui.TextUnformatted(label);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, max.Y + Gap(0.3f)));

        return clicked;
    }

    public void PickerEmptyState(FontAwesomeIcon icon, string text, float width)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Scaled(92f);

        ImGui.Dummy(new Vector2(width, height));

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Text, FaintText))
        {
            var glyph = icon.ToIconString();
            var size = ImGui.CalcTextSize(glyph);
            ImGui.SetCursorScreenPos(new Vector2(origin.X + (width - size.X) * 0.5f, origin.Y + height * 0.26f));
            ImGui.TextUnformatted(glyph);
        }

        var textSize = ImGui.CalcTextSize(text);

        using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X + (width - textSize.X) * 0.5f, origin.Y + height * 0.58f));
            ImGui.TextUnformatted(text);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + height));
    }
}
