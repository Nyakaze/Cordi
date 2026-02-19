using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;


namespace Cordi.Services.QoLBar;

[Flags]
public enum KeyModifier
{
    None = 0,
    Shift = 1 << 16,
    Ctrl = 1 << 17,
    Alt = 1 << 18
}

public class KeybindService : IDisposable
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly Dictionary<int, bool> keyStates = new();
    private readonly Dictionary<int, bool> prevKeyStates = new();
    private readonly List<(int hotkey, Action onActivate, bool blockGame)> registeredHotkeys = new();
    private readonly IKeyState keyState;
    public bool Disabled { get; set; } = false;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    public KeybindService(IKeyState keyState)
    {
        this.keyState = keyState;
    }

    private static bool IsGameFocused()
    {
        var hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        return GetForegroundWindow() == hwnd;
    }

    public void Update()
    {
        if (Disabled) return;

        // Only process hotkeys when the game window is in focus
        if (!IsGameFocused())
        {
            // Still update prev states so IsKeyDown returns false next focused frame
            foreach (var key in keyStates.Keys)
                prevKeyStates[key] = keyStates[key];
            for (int i = 0; i < 256; i++)
                keyStates[i] = false;
            return;
        }

        foreach (var key in prevKeyStates.Keys)
            prevKeyStates[key] = keyStates.GetValueOrDefault(key, false);

        for (int i = 0; i < 256; i++)
        {
            prevKeyStates[i] = keyStates.GetValueOrDefault(i, false);
            keyStates[i] = (GetAsyncKeyState(i) & 0x8000) != 0;
        }

        ProcessHotkeys();
    }

    public bool IsKeyDown(int vKey)
    {
        return keyStates.GetValueOrDefault(vKey, false) && !prevKeyStates.GetValueOrDefault(vKey, false);
    }

    public bool IsKeyHeld(int vKey)
    {
        return keyStates.GetValueOrDefault(vKey, false);
    }

    public bool IsKeyUp(int vKey)
    {
        return !keyStates.GetValueOrDefault(vKey, false) && prevKeyStates.GetValueOrDefault(vKey, false);
    }

    public int GetModifiers()
    {
        int mods = 0;
        if (IsKeyHeld(VK_SHIFT)) mods |= (int)KeyModifier.Shift;
        if (IsKeyHeld(VK_CONTROL)) mods |= (int)KeyModifier.Ctrl;
        if (IsKeyHeld(VK_MENU)) mods |= (int)KeyModifier.Alt;
        return mods;
    }

    public static int GetBaseKey(int hotkey)
    {
        return hotkey & 0xFFFF;
    }

    /// <summary>
    /// Called from the framework update (BEFORE the game reads input) for each bar's shortcuts.
    /// Sets HotkeyActivatedThisFrame on any shortcut whose hotkey is pressed,
    /// and blocks the game from receiving the key if HotkeyPassToGame is false.
    /// </summary>
    public void ProcessShortcutHotkeys(IEnumerable<Cordi.UI.QoLBar.ShortcutRenderer> shortcuts)
    {
        foreach (var sh in shortcuts)
        {
            var cfg = sh.Config;  // Config is always public; hotkeys are defined on the base config
            var baseKey = GetBaseKey(cfg.Hotkey);
            if (baseKey <= 0) continue;

            var modifiers = cfg.Hotkey & ~0xFFFF;
            if (!IsKeyDown(baseKey) || GetModifiers() != modifiers) continue;

            sh.HotkeyActivatedThisFrame = true;
            if (!cfg.HotkeyPassToGame)
                BlockGameKey(baseKey);

            // Recurse into category children
            if (cfg.Type == Cordi.Configuration.QoLBar.ShortcutType.Category)
                ProcessShortcutHotkeys(sh.Children);
        }
    }

    public void RegisterHotkey(int hotkey, Action onActivate, bool blockGame = false)
    {
        registeredHotkeys.Add((hotkey, onActivate, blockGame));
    }

    public void ClearHotkeys()
    {
        registeredHotkeys.Clear();
    }

    private void ProcessHotkeys()
    {
        foreach (var (hotkey, onActivate, blockGame) in registeredHotkeys)
        {
            var baseKey = GetBaseKey(hotkey);
            if (baseKey <= 0) continue;

            var modifiers = hotkey & ~0xFFFF;
            if (IsKeyDown(baseKey) && GetModifiers() == modifiers)
            {
                onActivate();
                if (blockGame)
                    BlockGameKey(baseKey);
            }
        }
    }

    private void BlockGameKey(int vKey)
    {
        try
        {
            keyState[(Dalamud.Game.ClientState.Keys.VirtualKey)vKey] = false;
        }
        catch { }
    }

    /// <summary>Publicly accessible block: suppresses the given key from reaching the game for one frame.</summary>
    public void BlockKey(int hotkey) => BlockGameKey(GetBaseKey(hotkey));

    public bool InputHotkey(string label, ref int hotkey)
    {
        var changed = false;
        var baseKey = GetBaseKey(hotkey);
        var mods = hotkey & ~0xFFFF;

        var displayParts = new List<string>();
        if ((mods & (int)KeyModifier.Ctrl) != 0) displayParts.Add("Ctrl");
        if ((mods & (int)KeyModifier.Shift) != 0) displayParts.Add("Shift");
        if ((mods & (int)KeyModifier.Alt) != 0) displayParts.Add("Alt");
        if (baseKey > 0) displayParts.Add(((System.Windows.Forms.Keys)baseKey).ToString());

        var display = displayParts.Count > 0 ? string.Join(" + ", displayParts) : "Click to bind";
        var hasKey = hotkey != 0;
        var popupId = $"HotkeyInput##{label}";

        // --- Pill button: rounded, accent-tinted when a key is set ---
        var btnBg = hasKey
            ? new Vector4(0.24f, 0.14f, 0.45f, 1f)   // accent purple
            : new Vector4(0.18f, 0.18f, 0.22f, 1f);   // neutral
        var btnText = hasKey
            ? new Vector4(0.85f, 0.70f, 1.00f, 1f)    // light lavender
            : new Vector4(0.65f, 0.65f, 0.75f, 1f);   // muted

        ImGui.PushStyleColor(ImGuiCol.Button, btnBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, btnBg with { W = 0.8f });
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, btnBg with { W = 0.6f });
        ImGui.PushStyleColor(ImGuiCol.Text, btnText);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);

        if (ImGui.Button($" {display} ##{label}", new Vector2(0, 0)))
            ImGui.OpenPopup(popupId);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);

        // Small × clear button, only when a key is set
        if (hasKey)
        {
            ImGui.SameLine(0, 4);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.40f, 0.10f, 0.10f, 0.6f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.65f, 0.15f, 0.15f, 0.9f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.80f, 0.20f, 0.20f, 1.0f));
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button($"{FontAwesomeIcon.Times.ToIconString()}##{label}_clear", new Vector2(ImGui.GetFrameHeight(), ImGui.GetFrameHeight())))
            {
                hotkey = 0;
                changed = true;
            }
            ImGui.PopFont();
            ImGui.PopStyleVar();
            ImGui.PopStyleColor(3);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Clear hotkey");
        }

        // --- Capture popup ---
        if (ImGui.BeginPopup(popupId))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.85f, 0.90f, 1f));
            ImGui.TextUnformatted("Press any key combination…");
            ImGui.PopStyleColor();

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.65f, 1f));
            ImGui.TextUnformatted("Press Esc to clear the binding.");
            ImGui.PopStyleColor();

            for (int i = 1; i < 256; i++)
            {
                if (i == VK_SHIFT || i == VK_CONTROL || i == VK_MENU) continue;

                if (i == 0x1B) // Escape → clear
                {
                    if (IsKeyDown(i))
                    {
                        hotkey = 0;
                        changed = true;
                        ImGui.CloseCurrentPopup();
                        break;
                    }
                    continue;
                }

                if (IsKeyDown(i))
                {
                    hotkey = i | GetModifiers();
                    changed = true;
                    ImGui.CloseCurrentPopup();
                    break;
                }
            }

            ImGui.EndPopup();
        }

        return changed;
    }

    public void Dispose()
    {
        registeredHotkeys.Clear();
        keyStates.Clear();
        prevKeyStates.Clear();
    }
}
