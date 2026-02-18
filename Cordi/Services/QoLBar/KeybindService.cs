using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
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

    public void Update()
    {
        if (Disabled) return;

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

        var display = displayParts.Count > 0 ? string.Join(" + ", displayParts) : "None";

        ImGui.TextUnformatted(label);
        ImGui.SameLine();

        if (ImGui.Button($"{display}##{label}"))
            ImGui.OpenPopup($"HotkeyInput##{label}");

        if (ImGui.BeginPopup($"HotkeyInput##{label}"))
        {
            ImGui.TextUnformatted("Press a key combination...");
            ImGui.TextUnformatted("Press Escape to clear.");

            for (int i = 1; i < 256; i++)
            {
                if (i == VK_SHIFT || i == VK_CONTROL || i == VK_MENU) continue;
                if (i == 0x1B)
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

        ImGui.SameLine();
        if (ImGui.SmallButton($"×##{label}"))
        {
            hotkey = 0;
            changed = true;
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
