using System;
using System.Numerics;
using Cordi.Extensions;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Themes;
using Cordi.Configuration;
using Dalamud.Interface.Components;

public sealed partial class UiTheme
{
    public UiCardResult DrawPluginCard(
        string id,
        ref bool enabled,
        string title,
        string description,
        string authorRightAligned,
        string tagLabel = "Combat",
        Vector4? tagBg = null,
        float height = 88f
    )
    {
        float scale = ImGuiHelpers.GlobalScale;
        float pad = PadX(0.9f);
        float gap = Gap(1f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(availW, height * scale);

        ImGui.InvisibleButton(id, size);
        bool clickedCard = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        bool rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        var cardBg = CardBgOr();
        var border = WindowBorderOr();
        draw.AddRectFilled(start, start + size, ImGui.GetColorU32(cardBg), radius);
        draw.AddRect(start, start + size, ImGui.GetColorU32(border), radius);

        float fh = ImGui.GetFrameHeight();
        float cbSize = MathF.Max(fh * 0.95f, 18f * scale);
        var cbPos = new Vector2(start.X + pad, start.Y + (size.Y - cbSize) * 0.5f);
        var cbRect = new Vector2(cbPos.X + cbSize, cbPos.Y + cbSize);

        var colChkOff = Lerp(FrameBg, Vector4.Zero, 0f) is { W: > 0 } ? FrameBg : new Vector4(0.20f, 0.22f, 0.26f, 1f);
        var colChkOn = ColorCheckboxOn;

        draw.AddRectFilled(cbPos, cbRect, ImGui.GetColorU32(enabled ? colChkOn : colChkOff), 4f * scale);
        draw.AddRect(cbPos, cbRect, ImGui.GetColorU32(border), 4f * scale);

        if (enabled)
        {
            var a = new Vector2(cbPos.X + cbSize * 0.25f, cbPos.Y + cbSize * 0.55f);
            var b = new Vector2(cbPos.X + cbSize * 0.45f, cbPos.Y + cbSize * 0.75f);
            var c = new Vector2(cbPos.X + cbSize * 0.78f, cbPos.Y + cbSize * 0.30f);
            draw.AddLine(a, b, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2f * scale);
            draw.AddLine(b, c, ImGui.GetColorU32(new Vector4(1, 1, 1, 1)), 2f * scale);
        }

        ImGui.SetCursorScreenPos(cbPos);
        ImGui.InvisibleButton(id + "##chk", new Vector2(cbSize, cbSize));
        bool toggleChanged = false;
        if (ImGui.IsItemClicked()) { enabled = !enabled; toggleChanged = true; }

        float textLeft = cbRect.X + pad;
        float textRight = start.X + size.X - pad;
        float textTop = start.Y + pad;
        float textBottom = start.Y + size.Y - pad;

        using (ImRaii.PushColor(ImGuiCol.Text, TextOr()))
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
            ImGui.TextUnformatted(title);
        }

        var muted = MutedOr();
        var authSize = ImGui.CalcTextSize(authorRightAligned);
        using (ImRaii.PushColor(ImGuiCol.Text, muted))
        {
            ImGui.SetCursorScreenPos(new Vector2(textRight - authSize.X, textTop));
            ImGui.TextUnformatted(authorRightAligned);
        }

        using (ImRaii.PushColor(ImGuiCol.Text, muted))
        {
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop + ImGui.GetTextLineHeightWithSpacing()));
            ImGui.PushTextWrapPos(textRight);
            ImGui.TextUnformatted(description);
            ImGui.PopTextWrapPos();
        }

        string tag = tagLabel;
        var tagPad = new Vector2(PadX(0.6f), PadY(0.4f));
        var tagSize = ImGui.CalcTextSize(tag);
        var tagPos = new Vector2(textLeft, textBottom - tagSize.Y - tagPad.Y);
        var tagRect = tagPos + tagSize + tagPad * 2f;

        var tagBgCol = tagBg ?? Accent;
        var tagTextCol = AccentText.W > 0 ? AccentText : new Vector4(1, 1, 1, 1);

        draw.AddRectFilled(tagPos, tagRect, ImGui.GetColorU32(tagBgCol), Radius(0.75f));
        draw.AddRect(tagPos, tagRect, ImGui.GetColorU32(border), Radius(0.75f));

        using (ImRaii.PushColor(ImGuiCol.Text, tagTextCol))
        {
            ImGui.SetCursorScreenPos(tagPos + tagPad);
            ImGui.TextUnformatted(tag);
        }

        ImGui.SetCursorScreenPos(tagPos);
        ImGui.InvisibleButton(id + "##tag", tagRect - tagPos);
        bool tagClicked = ImGui.IsItemClicked();

        float kebabW = 18f * scale;
        float kebabH = 12f * scale;
        var kebabPos = new Vector2(textRight - kebabW, textBottom - kebabH);
        for (int i = 0; i < 3; i++)
        {
            float y = kebabPos.Y + i * (kebabH / 2.5f);
            draw.AddLine(new Vector2(kebabPos.X, y), new Vector2(kebabPos.X + kebabW, y), ImGui.GetColorU32(muted), 2f * scale);
        }
        ImGui.SetCursorScreenPos(new Vector2(kebabPos.X, kebabPos.Y - 2f * scale));
        ImGui.InvisibleButton(id + "##menu", new Vector2(kebabW, kebabH + 4f * scale));
        bool menuClicked = ImGui.IsItemClicked();

        return new UiCardResult
        {
            Clicked = clickedCard,
            RightClicked = rightClicked,
            ToggleChanged = toggleChanged,
            TagClicked = tagClicked,
            MenuClicked = menuClicked
        };
    }

    public UiCardDynResult DrawPluginCardFlex(
        string id,
        ref bool enabled,
        bool showCheckbox,
        string title,
        Action<UiCardSlots, UiTheme>? drawTopRight = null,
        Action<UiCardSlots, UiTheme>? drawBody = null,
        Action<UiCardSlots, UiTheme>? drawBottomLeft = null,
        Action<UiCardSlots, UiTheme>? drawBottomRight = null,
        string defaultDescription = "",
        string defaultTopRightText = "",
        string defaultBottomLeftTag = "",
        float height = 88f
    )
    {
        float scale = ImGuiHelpers.GlobalScale;
        float pad = PadX(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(availW, height * scale);
        var cardRect = new UiRect(start, start + size);

        ImGui.InvisibleButton(id, size);
        ImGui.SetItemAllowOverlap();
        bool clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        bool rightClicked = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        float fh = ImGui.GetFrameHeight();
        float cbSize = MathF.Max(fh * 0.95f, 18f * scale);
        var cbMin = new Vector2(cardRect.Min.X + pad, cardRect.Min.Y + (cardRect.Size.Y - cbSize) * 0.5f);
        var cbMax = cbMin + new Vector2(cbSize, cbSize);
        var cbRect = new UiRect(cbMin, cbMax);
        bool toggleChanged = false;

        if (showCheckbox)
        {
            var colChkOff = FrameBg;
            var colChkOn = ColorCheckboxOn;
            draw.AddRectFilled(cbRect.Min, cbRect.Max, ImGui.GetColorU32(enabled ? colChkOn : colChkOff), 4f * scale);
            draw.AddRect(cbRect.Min, cbRect.Max, ImGui.GetColorU32(WindowBorder), 4f * scale);
            if (enabled)
            {
                var a = new Vector2(cbMin.X + cbSize * 0.25f, cbMin.Y + cbSize * 0.55f);
                var b = new Vector2(cbMin.X + cbSize * 0.45f, cbMin.Y + cbSize * 0.75f);
                var c = new Vector2(cbMin.X + cbSize * 0.78f, cbMin.Y + cbSize * 0.30f);
                draw.AddLine(a, b, ImGui.GetColorU32(Vector4.One), 2f * scale);
                draw.AddLine(b, c, ImGui.GetColorU32(Vector4.One), 2f * scale);
            }
            if (InvisibleBtn(id + "##chk", cbRect)) { enabled = !enabled; toggleChanged = true; }
        }

        float textLeft = cbMax.X + pad;
        float textRight = cardRect.Max.X - pad;
        float textTop = cardRect.Min.Y + pad;
        float textBottom = cardRect.Max.Y - pad;

        var titlePos = new Vector2(textLeft, textTop);
        var titleSize = ImGui.CalcTextSize(title);
        var titleRect = new UiRect(titlePos, titlePos + titleSize);

        var topRightRect = new UiRect(
            new Vector2(textRight - 220f * scale, textTop),
            new Vector2(textRight, textTop + ImGui.GetTextLineHeight())
        );

        var bodyRect = new UiRect(
            new Vector2(textLeft, titleRect.Max.Y + 4f * scale),
            new Vector2(textRight, textBottom - 22f * scale)
        );

        var bottomLeftRect = new UiRect(
            new Vector2(textLeft, textBottom - 20f * scale),
            new Vector2(textLeft + 240f * scale, textBottom)
        );

        var bottomRightRect = new UiRect(
            new Vector2(textRight - 80f * scale, textBottom - 18f * scale),
            new Vector2(textRight, textBottom)
        );

        var slots = new UiCardSlots
        {
            Card = cardRect,
            Checkbox = cbRect,
            TitleLeft = titleRect,
            TopRight = topRightRect,
            Body = bodyRect,
            BottomLeft = bottomLeftRect,
            BottomRight = bottomRightRect
        };

        WithCursor(titleRect, () =>
        {
            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }
        });

        if (drawTopRight is not null)
        {
            drawTopRight(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultTopRightText))
        {
            WithCursor(topRightRect, () =>
            {
                using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
                {
                    var txt = defaultTopRightText;
                    var sz = ImGui.CalcTextSize(txt);
                    ImGui.SetCursorScreenPos(new Vector2(topRightRect.Max.X - sz.X, topRightRect.Min.Y));
                    ImGui.TextUnformatted(txt);
                }
            });
        }

        if (drawBody is not null)
        {
            drawBody(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultDescription))
        {
            WithCursor(bodyRect, () =>
            {
                using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
                {
                    ImGui.PushTextWrapPos(bodyRect.Max.X);
                    ImGui.TextUnformatted(defaultDescription);
                    ImGui.PopTextWrapPos();
                }
            });
        }

        bool tagClicked = false;
        if (drawBottomLeft is not null)
        {
            drawBottomLeft(slots, this);
        }
        else if (!string.IsNullOrEmpty(defaultBottomLeftTag))
        {
            var tagPad = new Vector2(PadX(0.6f), PadY(0.4f));
            var sz = ImGui.CalcTextSize(defaultBottomLeftTag);
            var min = slots.BottomLeft.Min;
            var rect = new UiRect(min, min + sz + tagPad * 2f);

            draw.AddRectFilled(rect.Min, rect.Max, ImGui.GetColorU32(Accent), Radius(0.75f));
            draw.AddRect(rect.Min, rect.Max, ImGui.GetColorU32(WindowBorder), Radius(0.75f));
            using (ImRaii.PushColor(ImGuiCol.Text, AccentText))
            {
                WithCursor(new UiRect(rect.Min + tagPad, rect.Min + tagPad + sz), () =>
                {
                    ImGui.TextUnformatted(defaultBottomLeftTag);
                });
            }

            if (InvisibleBtn(id + "##tag", rect)) tagClicked = true;
        }

        bool menuClicked = false;
        if (drawBottomRight is not null)
        {
            drawBottomRight(slots, this);
        }
        else
        {
            float kebabW = 18f * scale;
            float kebabH = 12f * scale;
            var basePos = new Vector2(slots.BottomRight.Max.X - kebabW, slots.BottomRight.Min.Y);
            for (int i = 0; i < 3; i++)
            {
                float y = basePos.Y + i * (kebabH / 2.5f);
                draw.AddLine(new Vector2(basePos.X, y),
                             new Vector2(basePos.X + kebabW, y),
                             ImGui.GetColorU32(MutedText), 2f * scale);
            }
            var kebabRect = new UiRect(
                new Vector2(basePos.X, basePos.Y - 2f * scale),
                new Vector2(basePos.X + kebabW, basePos.Y + kebabH + 2f * scale)
            );
            if (InvisibleBtn(id + "##menu", kebabRect)) menuClicked = true;
        }

        return new UiCardDynResult
        {
            Clicked = clicked,
            RightClicked = rightClicked,
            ToggleChanged = toggleChanged,
            TagClicked = tagClicked,
            MenuClicked = menuClicked
        };
    }


    public void DrawPluginCardAuto(
        string id,
        string title,
        Action<float> drawContent,
        ref bool enabled,
        bool showCheckbox = false,
        string? mutedText = null,
        Action? drawHeaderRight = null)
    {
        float scale = ImGuiHelpers.GlobalScale;
        float padX = PadX(0.9f);
        float padY = PadY(0.9f);
        float radius = Radius(1.0f);

        var draw = ImGui.GetWindowDrawList();
        var startPos = ImGui.GetCursorScreenPos();
        var availW = ImGui.GetContentRegionAvail().X;

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        using (var group = ImRaii.Group())
        {
            ImGui.Dummy(new Vector2(0, padY));

            float contentStartX = padX;

            if (showCheckbox)
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
                bool chk = enabled;
                if (ImGui.Checkbox($"##chk_{id}", ref chk))
                {
                    enabled = chk;
                }

                ImGui.SameLine();
                contentStartX = 0;
                HoverHandIfItem();
            }
            else
            {
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + padX);
            }

            using (ImRaii.PushColor(ImGuiCol.Text, Text))
            {
                ImGui.TextUnformatted(title);
            }

            if (!string.IsNullOrEmpty(mutedText))
            {
                ImGui.SameLine();
                ImGui.TextColored(MutedText, mutedText);
            }

            if (drawHeaderRight != null)
            {
                ImGui.SameLine();
                float reservedWidth = 10f * scale; // Reduced to fit "((?))" icon snugly
                float headerRightCursorX = (startPos.X + availW - padX) - reservedWidth;
                if (headerRightCursorX < ImGui.GetCursorPosX()) headerRightCursorX = ImGui.GetCursorPosX() + 10f; // Prevent overlap
                ImGui.SetCursorScreenPos(new Vector2(headerRightCursorX, ImGui.GetCursorScreenPos().Y));

                drawHeaderRight();
                ImGui.NewLine(); // Ensure subsequent content starts on a new line
            }

            SpacerY(0.5f);

            using (ImRaii.PushIndent(padX))
            {
                float innerWidth = availW - (padX * 2);
                using (ImRaii.ItemWidth(innerWidth))
                {
                    drawContent(innerWidth);
                }
            }

            ImGui.Dummy(new Vector2(0, padY));
        }

        var itemMin = ImGui.GetItemRectMin();
        var itemMax = ImGui.GetItemRectMax();


        float totalH = itemMax.Y - startPos.Y;

        var endPos = new Vector2(startPos.X + availW, itemMax.Y);

        draw.ChannelsSetCurrent(0);


        draw.AddRectFilled(startPos, endPos, ImGui.GetColorU32(CardBg), radius);
        draw.AddRect(startPos, endPos, ImGui.GetColorU32(WindowBorder), radius);


        draw.ChannelsMerge();


        ImGui.SetCursorScreenPos(new Vector2(startPos.X, endPos.Y));
    }


    public void MutedLabel(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, MutedText))
        {
            ImGui.TextUnformatted(text);
        }
    }

    public string Fit(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        if (maxWidth <= 0f) return string.Empty;
        if (ImGui.CalcTextSize(text).X <= maxWidth) return text;

        const string ellipsis = "...";
        float ellipsisWidth = ImGui.CalcTextSize(ellipsis).X;
        if (ellipsisWidth >= maxWidth) return string.Empty;

        float budget = maxWidth - ellipsisWidth;
        int low = 0;
        int high = text.Length;

        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(text[..mid]).X <= budget) low = mid;
            else high = mid - 1;
        }

        return low == 0 ? ellipsis : text[..low].TrimEnd() + ellipsis;
    }

    public void FittedText(string text, float maxWidth) => ImGui.TextUnformatted(Fit(text, maxWidth));

    public Vector2 MeasureWrapped(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0f)
            return Vector2.Zero;

        return ImGui.CalcTextSize(text, false, maxWidth);
    }

    public void WrappedText(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return;

        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + MathF.Max(maxWidth, 1f));
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
    }

    public void WrappedText(string text, float maxWidth, Vector4 color)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, color))
            WrappedText(text, maxWidth);
    }
    public void SpacerY(float mul = 1f) => ImGui.Dummy(new Vector2(0, Gap(mul)));
    public void SpacerX(float mul = 1f) => ImGui.Dummy(new Vector2(Gap(mul), 0));
    public void SameLineGap(float mul = 1f) => ImGui.SameLine(0f, Gap(mul));

    static Vector4 Lerp(in Vector4 a, in Vector4 b, float t)
        => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, a.W + (b.W - a.W) * t);

    private static void WithCursor(UiRect r, Action draw)
    {
        ImGui.SetCursorScreenPos(r.Min);
        draw();
    }
    private static bool InvisibleBtn(string id, UiRect r)
    {
        ImGui.SetCursorScreenPos(r.Min);
        bool clicked = ImGui.InvisibleButton(id, r.Size);
        if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return clicked;
    }
    public void HoverHandIfItem() { if (ImGui.IsItemHovered()) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand); }

    public void Tooltip(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var padding = ImGui.GetStyle().WindowPadding;
        var size = ImGui.CalcTextSize(text) + padding * 2f;
        var mouse = ImGui.GetMousePos();
        var viewport = ImGui.GetMainViewport();
        var workMin = viewport.WorkPos;
        var workMax = viewport.WorkPos + viewport.WorkSize;

        float x = Math.Clamp(mouse.X, workMin.X, MathF.Max(workMin.X, workMax.X - size.X));
        float y = mouse.Y + Scaled(TooltipCursorOffset);

        if (y + size.Y > workMax.Y)
            y = MathF.Max(workMin.Y, mouse.Y - size.Y - Scaled(TooltipCursorOffset * 0.5f));

        ImGui.SetNextWindowPos(new Vector2(x, y), ImGuiCond.Always);

        using (ImRaii.Tooltip())
        {
            ImGui.TextUnformatted(text);
        }
    }

    public void TooltipIfItemHovered(string text)
    {
        if (ImGui.IsItemHovered())
            Tooltip(text);
    }
}
