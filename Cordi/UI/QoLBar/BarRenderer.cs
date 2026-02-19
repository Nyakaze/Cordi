using System;
using System.Numerics;
using System.Collections.Generic;
using Cordi.Configuration.QoLBar;
using Cordi.Core;
using Cordi.Services.QoLBar;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Cordi.UI.QoLBar;

public class BarRenderer : IDisposable
{
    private int _id;
    public int ID
    {
        get => _id;
        set
        {
            _id = value;
            Config = CordiPlugin.Plugin.QoLBarConfig.Bars[value];
            SetupPivot();
        }
    }

    public BarCfg Config { get; private set; } = null!;

    private readonly ConditionService conditionService;
    private readonly CommandExecutor commandExecutor;

    public List<ShortcutRenderer> Children { get; } = new();

    public bool IsVisible => !IsHidden && CheckConditionSet() && CheckDynamicVisibility();
    public bool IsHidden
    {
        get => Config.Hidden;
        set
        {
            Config.Hidden = value;
            CordiPlugin.Plugin.QoLBarConfig.Save();
        }
    }

    public bool IsDocked { get; private set; } = true;
    public bool IsDragging { get; private set; } = false;
    public bool IsHovered { get; private set; } = false;
    public bool HasPopupOpen { get; set; } = false;

    private Vector2 window = ImGuiHelpers.MainViewport.Size;
    private Vector2 monitor => ImGui.GetPlatformIO().Monitors[0].MainSize;
    public Vector2 UsableArea => IsDocked ? window : monitor;

    private Vector2 barSize = new(200, 38);
    private Vector2 barPos;
    private Vector2 piv = Vector2.Zero;
    private Vector2 hidePos = Vector2.Zero;
    private Vector2 revealPos = Vector2.Zero;

    private bool _reveal = false;
    private bool _lastReveal = true;
    private bool _mouseRevealed = false;
    private bool _firstframe = true;
    public bool _setPos = true;
    private Vector2 _tweenStart;
    private float _tweenProgress = 1;

    private float _maxW = 0;
    public float MaxWidth
    {
        get => _maxW;
        set { if (_maxW < value || value == 0) _maxW = value; }
    }

    private float _maxH = 0;
    public float MaxHeight
    {
        get => _maxH;
        set { if (_maxH < value || value == 0) _maxH = value; }
    }

    private bool _activated = false;
    public bool WasActivated
    {
        get => _activated;
        set
        {
            if (!value && !_activated)
            {
                foreach (var ui in Children)
                    ui.ClearActivated();
            }
            _activated = value;
        }
    }

    private Vector2 _maincatpos = Vector2.Zero;

    public BarRenderer(int n, ConditionService conditionService, CommandExecutor commandExecutor)
    {
        this.conditionService = conditionService;
        this.commandExecutor = commandExecutor;
        ID = n;

        for (int i = 0; i < Config!.ShortcutList.Count; i++)
            Children.Add(new ShortcutRenderer(this, commandExecutor));
    }

    public bool CheckConditionSet()
    {
        if (Config.ConditionSet < 0 || Config.ConditionSet >= CordiPlugin.Plugin.QoLBarConfig.ConditionSets.Count)
            return true;
        return conditionService.CheckConditionSet(Config.ConditionSet, CordiPlugin.Plugin.QoLBarConfig.ConditionSets);
    }

    private bool CheckDynamicVisibility()
    {
        if (!Config.DynVisEnabled) return true;
        var val = CordiPlugin.Plugin.VariableService.GetVariable(Config.DynVisVar);
        return val == Config.DynVisVal;
    }

    public void SetupPivot()
    {
        var alignPiv = Config.Alignment switch
        {
            BarAlign.LeftOrTop => 0.0f,
            BarAlign.Center => 0.5f,
            BarAlign.RightOrBottom => 1.0f,
            _ => 0
        };

        switch (Config.DockSide)
        {
            case BarDock.Top:
                piv.X = alignPiv;
                piv.Y = 1.0f;
                break;
            case BarDock.Right:
                piv.X = 0.0f;
                piv.Y = alignPiv;
                break;
            case BarDock.Bottom:
                piv.X = alignPiv;
                piv.Y = 0.0f;
                break;
            case BarDock.Left:
                piv.X = 1.0f;
                piv.Y = alignPiv;
                break;
            case BarDock.Undocked:
                piv = Vector2.Zero;
                IsDocked = false;
                _setPos = true;
                return;
        }

        IsDocked = true;
        SetupPositions();
        barPos = hidePos;
        _tweenStart = hidePos;
    }

    private Vector2 VectorPosition => IsDocked
        ? new Vector2((float)Math.Floor(Config.Position[0] * window.X), (float)Math.Floor(Config.Position[1] * window.Y))
        : new Vector2((float)Math.Floor(Config.Position[0] * monitor.X), (float)Math.Floor(Config.Position[1] * monitor.Y));

    private void SetupPositions()
    {
        hidePos = ImGuiHelpers.MainViewport.Pos;
        var pos = VectorPosition;
        switch (Config.DockSide)
        {
            case BarDock.Top:
                hidePos.X += window.X * piv.X + pos.X;
                hidePos.Y += 0;
                revealPos.X = hidePos.X;
                revealPos.Y = Math.Max(hidePos.Y + barSize.Y + pos.Y, GetHidePosition().Y + 1);
                break;
            case BarDock.Right:
                hidePos.X += window.X;
                hidePos.Y += window.Y * piv.Y + pos.Y;
                revealPos.X = Math.Min(hidePos.X - barSize.X + pos.X, GetHidePosition().X - 1);
                revealPos.Y = hidePos.Y;
                break;
            case BarDock.Bottom:
                hidePos.X += window.X * piv.X + pos.X;
                hidePos.Y += window.Y;
                revealPos.X = hidePos.X;
                revealPos.Y = Math.Min(hidePos.Y - barSize.Y + pos.Y, GetHidePosition().Y - 1);
                break;
            case BarDock.Left:
                hidePos.X += 0;
                hidePos.Y += window.Y * piv.Y + pos.Y;
                revealPos.X = Math.Max(hidePos.X + barSize.X + pos.X, GetHidePosition().X + 1);
                revealPos.Y = hidePos.Y;
                break;
        }
    }

    private Vector2 GetHidePosition()
    {
        if (Config.Hint)
        {
            var realHidePos = hidePos;
            var winPad = ImGui.GetStyle().WindowPadding * 2;
            switch (Config.DockSide)
            {
                case BarDock.Top: realHidePos.Y += winPad.Y; break;
                case BarDock.Left: realHidePos.X += winPad.X; break;
                case BarDock.Bottom: realHidePos.Y -= winPad.Y; break;
                case BarDock.Right: realHidePos.X -= winPad.X; break;
            }
            return realHidePos;
        }
        return hidePos;
    }

    public void Reveal() => _reveal = true;
    public void Hide() => _reveal = false;

    public void Draw()
    {
        CheckGameResolution();

        if (!IsVisible) return;

        HasPopupOpen = false;

        if (IsDocked || Config.Visibility == BarVisibility.Immediate)
        {
            SetupPositions();
            if (Config.Editing || HasPopupOpen)
                Reveal();
            else
                CheckMousePosition();
        }
        else
            Reveal();

        if (!IsDocked && !_firstframe && !_reveal && !_lastReveal) { WasActivated = false; return; }

        if (_firstframe || _reveal || (barPos != hidePos) || (!IsDocked && _lastReveal))
        {
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoScrollWithMouse |
                        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing;
            if (IsDocked || Config.LockedPosition) flags |= ImGuiWindowFlags.NoMove;
            if (Config.NoBackground) flags |= ImGuiWindowFlags.NoBackground;
            if (Config.ClickThrough) flags |= ImGuiWindowFlags.NoInputs;

            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Config.CornerRadius * ImGuiHelpers.GlobalScale * Config.Scale);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, Config.CornerRadius * ImGuiHelpers.GlobalScale * Config.Scale);
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(Config.Spacing[0], Config.Spacing[1]));

            // Bar Button BG: #2e2e38 -> 46, 46, 56 -> 0.180f, 0.180f, 0.220f
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.180f, 0.180f, 0.220f, 1.0f));

            if (!Config.NoBackground)
            {
                // Bar BG: #17171c -> 23, 23, 28 -> 0.090f, 0.090f, 0.110f
                var winBg = new Vector4(0.090f, 0.090f, 0.110f, Config.Opacity);
                ImGui.PushStyleColor(ImGuiCol.WindowBg, winBg);
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
            }

            SetPosition();
            ImGui.SetNextWindowSize(barSize);
            ImGui.Begin($"CordiBar##{ID}", flags);

            if ((_mouseRevealed || Config.Editing || HasPopupOpen) && ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup | ImGuiHoveredFlags.AllowWhenBlockedByActiveItem))
                Reveal();

            IsHovered = ImGui.IsWindowHovered();

            DrawShortcuts();
            DrawAdd();
            CheckDrag();
            SetupSize();

            // Re-check popup state after drawing children (they set HasPopupOpen)
            if (HasPopupOpen)
                Reveal();

            ImGui.End();

            // Pop both colors: Button (always) + WindowBg (always, one of the two branches)
            ImGui.PopStyleColor(2);

            ImGui.PopStyleVar(3);
        }

        if (!_reveal)
            _mouseRevealed = false;

        if (IsDocked)
        {
            TweenBarPosition();
            Hide();
        }
        else
            _lastReveal = _reveal;

        WasActivated = false;
        _firstframe = false;
    }

    private void SetPosition()
    {
        if (IsDocked)
        {
            ImGuiHelpers.ForceNextWindowMainViewport();
            ImGui.SetNextWindowPos(barPos, ImGuiCond.Always, piv);
        }
        else if (_setPos || Config.LockedPosition)
        {
            if (!_firstframe)
            {
                ImGui.SetNextWindowPos(VectorPosition);
                _setPos = false;
            }
            else
                ImGui.SetNextWindowPos(monitor);
        }
    }

    private void CheckGameResolution()
    {
        var resolution = ImGuiHelpers.MainViewport.Size;
        if (resolution != window)
        {
            window = resolution;
            SetupPivot();
        }
    }

    private void CheckMousePosition()
    {
        if (IsDocked && _reveal) return;

        var (min, max) = CalculateRevealPosition();

        switch (Config.DockSide)
        {
            case BarDock.Top:
                max.Y = Math.Max(Math.Max(max.Y - barSize.Y * (1 - Config.RevealAreaScale), min.Y + 1), GetHidePosition().Y + 1);
                break;
            case BarDock.Left:
                max.X = Math.Max(Math.Max(max.X - barSize.X * (1 - Config.RevealAreaScale), min.X + 1), GetHidePosition().X + 1);
                break;
            case BarDock.Bottom:
                min.Y = Math.Min(Math.Min(min.Y + barSize.Y * (1 - Config.RevealAreaScale), max.Y - 1), GetHidePosition().Y - 1);
                break;
            case BarDock.Right:
                min.X = Math.Min(Math.Min(min.X + barSize.X * (1 - Config.RevealAreaScale), max.X - 1), GetHidePosition().X - 1);
                break;
        }

        var mPos = ImGui.GetMousePos();
        if (Config.Visibility == BarVisibility.Always || (min.X <= mPos.X && mPos.X < max.X && min.Y <= mPos.Y && mPos.Y < max.Y))
        {
            _mouseRevealed = true;
            Reveal();
        }
        else
            Hide();
    }

    private (Vector2, Vector2) CalculateRevealPosition()
    {
        var pos = IsDocked ? revealPos : VectorPosition;
        var min = new Vector2(pos.X - (barSize.X * piv.X), pos.Y - (barSize.Y * piv.Y));
        var max = new Vector2(pos.X + (barSize.X * (1 - piv.X)), pos.Y + (barSize.Y * (1 - piv.Y)));
        return (min, max);
    }

    private void CheckDrag()
    {
        if (_firstframe || Config.LockedPosition) return;

        if (IsDocked)
        {
            var dragging = !IsDragging
                ? ImGui.IsWindowFocused() && IsHovered && !ImGui.IsMouseClicked(ImGuiMouseButton.Left) && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 5)
                : IsDragging && !ImGui.IsMouseReleased(ImGuiMouseButton.Left);

            if (dragging && dragging != IsDragging)
                IsDragging = true;

            if (IsDragging)
            {
                Reveal();
                var delta = ImGui.GetMouseDragDelta(ImGuiMouseButton.Left, 0) / UsableArea;
                ImGui.ResetMouseDragDelta();
                Config.Position[0] = Math.Min(Config.Position[0] + delta.X, 1);
                Config.Position[1] = Math.Min(Config.Position[1] + delta.Y, 1);
                SetupPivot();
            }

            if (!dragging && dragging != IsDragging)
            {
                IsDragging = false;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
        }
        else
        {
            if (ImGui.GetWindowPos() != VectorPosition)
            {
                var newPos = ImGui.GetWindowPos() / UsableArea;
                Config.Position[0] = newPos.X;
                Config.Position[1] = newPos.Y;
                CordiPlugin.Plugin.QoLBarConfig.Save();
            }
        }
    }

    private void DrawShortcuts()
    {
        var cols = Config.Columns;
        var scale = Config.Scale;
        var width = (float)Math.Round(Config.ButtonWidth * ImGuiHelpers.GlobalScale * scale);
        var height = Config.ButtonHeight > 0
            ? (float)Math.Round(Config.ButtonHeight * ImGuiHelpers.GlobalScale * scale)
            : (float)Math.Round((ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2) * scale);
        for (int i = 0; i < Children.Count; i++)
        {
            var ui = Children[i];
            ImGui.PushID(i);
            ui.DrawShortcut(width, height);
            if (cols <= 0 || i % cols != cols - 1)
                ImGui.SameLine();
            ImGui.PopID();
        }
    }

    private void DrawAdd()
    {
        if (Config.Editing)
        {
            var scale = Config.Scale;
            var addHeight = Config.ButtonHeight > 0
                ? (float)Math.Round(Config.ButtonHeight * ImGuiHelpers.GlobalScale * scale)
                : (float)Math.Round((ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2) * scale);
            if (ImGui.Button("+", new Vector2(Config.ButtonWidth * ImGuiHelpers.GlobalScale * scale, addHeight)))
                ImGui.OpenPopup("addShortcut");

            var size = ImGui.GetItemRectMax() - ImGui.GetWindowPos();
            MaxWidth = size.X;
            MaxHeight = size.Y;
        }

        if (ImGui.BeginPopup("addShortcut"))
        {
            if (ImGui.MenuItem("Command"))
            {
                AddShortcut(new ShCfg { Name = "New", Type = ShortcutType.Command });
            }
            if (ImGui.MenuItem("Category"))
            {
                AddShortcut(new ShCfg { Name = "Menu", Type = ShortcutType.Category });
            }
            if (ImGui.MenuItem("Spacer"))
            {
                AddShortcut(new ShCfg { Name = string.Empty, Type = ShortcutType.Spacer });
            }
            ImGui.EndPopup();
        }
    }

    public (Vector2, Vector2) CalculateCategoryPosition(bool v, bool subItem, CategoryAnchor anchor = CategoryAnchor.Auto)
    {
        Vector2 pos, wMin, wMax;
        if (!subItem)
        {
            (wMin, wMax) = CalculateRevealPosition();
            pos = wMin + ((ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() / 2)) - ImGui.GetWindowPos());
            _maincatpos = pos;
        }
        else
        {
            wMin = ImGui.GetWindowPos();
            wMax = ImGui.GetWindowPos() + ImGui.GetWindowSize();
            pos = ImGui.GetItemRectMin() + (ImGui.GetItemRectSize() / 2);
        }

        var cpiv = Vector2.Zero;

        // Manual Anchor Logic
        if (anchor != CategoryAnchor.Auto)
        {
            switch (anchor)
            {
                case CategoryAnchor.TopRight: cpiv = new Vector2(0.0f, 1.0f); break;
                case CategoryAnchor.TopLeft: cpiv = new Vector2(1.0f, 1.0f); break;
                case CategoryAnchor.BottomRight: cpiv = new Vector2(0.0f, 0.0f); break;
                case CategoryAnchor.BottomLeft: cpiv = new Vector2(1.0f, 0.0f); break;
                case CategoryAnchor.RightTop: cpiv = new Vector2(0.0f, 0.0f); break;
                case CategoryAnchor.RightBottom: cpiv = new Vector2(0.0f, 1.0f); break;
                case CategoryAnchor.LeftTop: cpiv = new Vector2(1.0f, 0.0f); break;
                case CategoryAnchor.LeftBottom: cpiv = new Vector2(1.0f, 1.0f); break;
            }

            // For Top/Bottom anchors, X is centered on item, Y is top/bottom edge
            // For Left/Right anchors, Y is centered on item, X is left/right edge

            // Simplified placement based on pivot:
            // If pivot X is 0 (Left edge of popup), it attaches to Right edge of Item.
            // If pivot X is 1 (Right edge of popup), it attaches to Left edge of Item.

            var itemMin = ImGui.GetItemRectMin();
            var itemMax = ImGui.GetItemRectMax();
            var itemSize = ImGui.GetItemRectSize();

            // X Position
            if (cpiv.X == 0.0f) pos.X = itemMax.X; // Attach to Right
            else if (cpiv.X == 1.0f) pos.X = itemMin.X; // Attach to Left
            else pos.X = itemMin.X + itemSize.X * 0.5f; // Center

            // Y Position
            if (cpiv.Y == 0.0f) pos.Y = itemMax.Y; // Attach to Bottom
            else if (cpiv.Y == 1.0f) pos.Y = itemMin.Y; // Attach to Top
            else pos.Y = itemMin.Y + itemSize.Y * 0.5f; // Center

            // Adjust specific combos if needed (e.g. RightTop means Popup is Right of Item, Aligned Top)
            // RightTop -> Pivot (0,0) -> Pos should be (ItemRight, ItemTop)
            // My default logic above: 
            // Case RightTop (0,0): X=ItemRight, Y=ItemBottom. Wait. 
            // Pivot (0,0) means Top-Left corner of Popup.
            // If we want "RightTop" (Popup is to the Right, Top aligned):
            // Popup Top-Left (0,0) should be at Item Top-Right.
            // X matches: Pos.X = ItemMax.X (Right).
            // Y matches: Pos.Y = ItemMax.Y (Bottom). NO. Should be ItemMin.Y (Top).

            // Let's prevent over-engineering. User just wants Side/Corner.
            // TopRight: Popup Top-Left (0,1)? No, TopRight usually means "Top side, Right aligned" or "Top-Right corner"?
            // Let's assume standard definitions:
            // "TopRight" -> Opens Above, Right aligned. Pivot (1, 1). Pos (ItemRight, ItemTop).

            // Refined Switch:
            switch (anchor)
            {
                case CategoryAnchor.TopRight: // Opens Above, Right Aligned
                    cpiv = new Vector2(1, 1);
                    pos.X = itemMax.X; pos.Y = itemMin.Y;
                    break;
                case CategoryAnchor.TopLeft: // Opens Above, Left Aligned
                    cpiv = new Vector2(0, 1);
                    pos.X = itemMin.X; pos.Y = itemMin.Y;
                    break;
                case CategoryAnchor.TopCenter: // Opens Above, Centered
                    cpiv = new Vector2(0.5f, 1);
                    pos.X = itemMin.X + itemSize.X * 0.5f; pos.Y = itemMin.Y;
                    break;

                case CategoryAnchor.BottomRight: // Opens Below, Right Aligned
                    cpiv = new Vector2(1, 0);
                    pos.X = itemMax.X; pos.Y = itemMax.Y;
                    break;
                case CategoryAnchor.BottomLeft: // Opens Below, Left Aligned
                    cpiv = new Vector2(0, 0);
                    pos.X = itemMin.X; pos.Y = itemMax.Y;
                    break;
                case CategoryAnchor.BottomCenter: // Opens Below, Centered
                    cpiv = new Vector2(0.5f, 0);
                    pos.X = itemMin.X + itemSize.X * 0.5f; pos.Y = itemMax.Y;
                    break;

                case CategoryAnchor.RightTop: // Opens Right, Top Aligned
                    cpiv = new Vector2(0, 0);
                    pos.X = itemMax.X; pos.Y = itemMin.Y;
                    break;
                case CategoryAnchor.RightBottom: // Opens Right, Bottom Aligned
                    cpiv = new Vector2(0, 1);
                    pos.X = itemMax.X; pos.Y = itemMax.Y;
                    break;
                case CategoryAnchor.RightCenter: // Opens Right, Centered
                    cpiv = new Vector2(0, 0.5f);
                    pos.X = itemMax.X; pos.Y = itemMin.Y + itemSize.Y * 0.5f;
                    break;

                case CategoryAnchor.LeftTop: // Opens Left, Top Aligned
                    cpiv = new Vector2(1, 0);
                    pos.X = itemMin.X; pos.Y = itemMin.Y;
                    break;
                case CategoryAnchor.LeftBottom: // Opens Left, Bottom Aligned
                    cpiv = new Vector2(1, 1);
                    pos.X = itemMin.X; pos.Y = itemMax.Y;
                    break;
                case CategoryAnchor.LeftCenter: // Opens Left, Centered
                    cpiv = new Vector2(1, 0.5f);
                    pos.X = itemMin.X; pos.Y = itemMin.Y + itemSize.Y * 0.5f;
                    break;
            }
            return (pos, cpiv);
        }

        // Auto Logic
        // Fix: If subItem is true, we force "Vertical" logic (Left/Right open) unless Bar is explicitly Vertical?
        // Actually, standard menus:
        // Horizontal Bar -> Dropdown (Vertical) -> Submenu (Right/Left)
        // Vertical Bar -> Flyout (Right/Left) -> Submenu (Right/Left)
        // So for subItems, we almost ALWAYS want Side opening (Right/Left).
        // Only if config.Columns > 0 (Grid) might we want Top/Bottom?

        bool forceSide = subItem; // Always force side for sub-items?

        if (!v && !forceSide) // Horizontal Bar, Level 1
        {
            cpiv.X = 0.5f;
            cpiv.Y = _maincatpos.Y < UsableArea.Y / 2 ? 0.0f : 1.0f;
            pos.Y = cpiv.Y == 0.0f
                ? wMax.Y - ImGui.GetStyle().WindowPadding.Y / 2
                : wMin.Y + ImGui.GetStyle().WindowPadding.Y / 2;
        }
        else // Vertical Bar OR SubItem
        {
            cpiv.Y = 0.5f;
            // For sub-items, check bounds relative to screen to flip side
            if (subItem)
            {
                // Simple check: is there space on the right?
                bool fitRight = (pos.X + 200) < UsableArea.X; // Approx 200 width
                cpiv.X = fitRight ? 0.0f : 1.0f;
                // If fitRight (0.0), opens to Right. Pos should be ItemRight.
                // If !fitRight (1.0), opens to Left. Pos should be ItemLeft.
                pos.X = fitRight ? ImGui.GetItemRectMax().X : ImGui.GetItemRectMin().X;

                // Align Top by default for submenus
                cpiv.Y = 0.0f;
                pos.Y = ImGui.GetItemRectMin().Y;
            }
            else
            {
                cpiv.X = _maincatpos.X < UsableArea.X / 2 ? 0.0f : 1.0f;
                pos.X = cpiv.X == 0.0f
                    ? wMax.X - ImGui.GetStyle().WindowPadding.X / 2
                    : wMin.X + ImGui.GetStyle().WindowPadding.X / 2;
            }
        }

        return (pos, cpiv);
    }

    private void SetupSize()
    {
        var winPad = ImGui.GetStyle().WindowPadding;
        barSize.X = MaxWidth + winPad.X;
        barSize.Y = MaxHeight + winPad.Y;
        MaxWidth = 0;
        MaxHeight = 0;
    }

    private void TweenBarPosition()
    {
        if (Config.Visibility == BarVisibility.Slide)
        {
            var _hidePos = GetHidePosition();

            if (_reveal != _lastReveal)
            {
                _lastReveal = _reveal;
                _tweenStart = barPos;
                _tweenProgress = 0;
            }

            if (_tweenProgress >= 1)
                barPos = _reveal ? revealPos : _hidePos;
            else
            {
                var dt = ImGui.GetIO().DeltaTime * 2;
                _tweenProgress = Math.Min(_tweenProgress + dt, 1);
                var x = -1 * ((float)Math.Pow(_tweenProgress - 1, 4) - 1);
                var deltaX = ((_reveal ? revealPos.X : _hidePos.X) - _tweenStart.X) * x;
                var deltaY = ((_reveal ? revealPos.Y : _hidePos.Y) - _tweenStart.Y) * x;
                barPos.X = _tweenStart.X + deltaX;
                barPos.Y = _tweenStart.Y + deltaY;
            }
        }
        else
            barPos = _reveal ? revealPos : GetHidePosition();
    }

    public void AddShortcut(ShCfg sh)
    {
        Config.ShortcutList.Add(sh);
        Children.Add(new ShortcutRenderer(this, commandExecutor));
        CordiPlugin.Plugin.QoLBarConfig.Save();
    }

    public void RemoveShortcut(int i)
    {
        Children[i].Dispose();
        Children.RemoveAt(i);
        Config.ShortcutList.RemoveAt(i);
        CordiPlugin.Plugin.QoLBarConfig.Save();
        RefreshShortcutIDs();
    }

    public void ShiftShortcut(int i, bool increment)
    {
        if (!increment ? i > 0 : i < (Children.Count - 1))
        {
            var j = (increment ? i + 1 : i - 1);
            var ui = Children[i];
            Children.RemoveAt(i);
            Children.Insert(j, ui);

            var sh = Config.ShortcutList[i];
            Config.ShortcutList.RemoveAt(i);
            Config.ShortcutList.Insert(j, sh);
            CordiPlugin.Plugin.QoLBarConfig.Save();
            RefreshShortcutIDs();
        }
    }

    private void RefreshShortcutIDs()
    {
        for (int i = 0; i < Children.Count; i++)
            Children[i].ID = i;
    }

    public void Dispose()
    {
        foreach (var ui in Children)
            ui.Dispose();
    }
}
