using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.Services;
using Cordi.UI.Components;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class SettingsTab : ConfigTabBase
{
    private string _botToken = string.Empty;
    private bool _botTokenInputActive = false;
    private float? _tempFontScale = null;
    private Vector3 _accentEdit;
    private bool _accentDirty;

    public override string Label => "Settings";

    public SettingsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        _botToken = plugin.Config.Discord.BotToken ?? string.Empty;

        var accent = UiTheme.ParseAccent(plugin.Config.Appearance.AccentColor);
        _accentEdit = new Vector3(accent.X, accent.Y, accent.Z);
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new (string, Action)[]
        {
            ("Discord", DrawDiscord),
            ("Audio", DrawAudio),
            ("Appearance", DrawAppearance),
            ("Font", DrawFont),
        };
    }

    private void DrawAudio()
    {
        var devices = AudioService.GetOutputDevices();
        var configured = plugin.Config.Audio.OutputDevice;
        bool deviceMissing = !plugin.Audio.IsConfiguredDeviceAvailable();

        var items = new List<DropdownItem>
        {
            new() { Key = string.Empty, Label = AudioService.DefaultDeviceLabel },
        };

        foreach (var device in devices)
            items.Add(new DropdownItem { Key = device.Guid.ToString(), Label = device.Description });

        Card.Draw(
            "audio-output",
            innerWidth =>
            {
                Row.Draw(
                    id: "audio-device",
                    icon: deviceMissing ? FontAwesomeIcon.ExclamationTriangle : FontAwesomeIcon.VolumeUp,
                    iconColor: deviceMissing ? UiTheme.TileAmber : theme.Accent,
                    title: "Output device",
                    subtitle: deviceMissing
                        ? "Saved device not found, using the primary sound driver"
                        : "Used for every sound Cordi plays",
                    controlWidth: 260f,
                    drawControl: (pos, width) =>
                    {
                        ImGui.SetCursorScreenPos(pos);
                        theme.OptionPicker(
                            "audio-output-device",
                            configured == Guid.Empty ? string.Empty : configured.ToString(),
                            items,
                            key =>
                            {
                                plugin.Config.Audio.OutputDevice =
                                    Guid.TryParse(key, out var parsed) ? parsed : Guid.Empty;
                                plugin.Config.Save();
                            },
                            width);
                    },
                    rowWidth: innerWidth);

                Row.Draw(
                    id: "audio-test",
                    icon: FontAwesomeIcon.Play,
                    iconColor: UiTheme.TileGreen,
                    title: "Test sound",
                    subtitle: "Plays the built-in alert through the selected device",
                    controlWidth: 140f,
                    drawControl: (pos, width) =>
                    {
                        float buttonHeight = theme.Scaled(32f);
                        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + (theme.Scaled(UiTheme.ControlHeight) - buttonHeight) * 0.5f));

                        if (theme.SecondaryButton("Play", new Vector2(width, buttonHeight)))
                            plugin.Audio.Play(string.Empty, 1f);
                    },
                    rowWidth: innerWidth);
            },
            label: "Audio Output",
            drawTrailing: anchor => theme.HelpPill(
                "audio-help",
                anchor,
                "Cordi plays alert sounds through this device.\n" +
                "Individual volumes stay with each feature.\n" +
                "If the device is unplugged, playback falls back to the primary sound driver."));
    }

    private void DrawAppearance()
    {
        bool dummyEnabled = true;
        theme.DrawPluginCardAuto(
            id: "accent-settings",
            enabled: ref dummyEnabled,
            showCheckbox: false,
            title: "Accent Color",
            drawContent: (avail) =>
            {
                ImGui.TextDisabled("Choose the accent color used across all Cordi windows.");
                theme.SpacerY(0.75f);

                var draw = ImGui.GetWindowDrawList();
                var origin = ImGui.GetCursorScreenPos();
                float swatch = theme.Scaled(30f);
                float step = swatch + theme.Gap(1.2f);

                for (int i = 0; i < UiTheme.AccentPresets.Length; i++)
                {
                    var (name, color) = UiTheme.AccentPresets[i];
                    var pos = new Vector2(origin.X + step * i, origin.Y);

                    ImGui.SetCursorScreenPos(pos);
                    if (ImGui.InvisibleButton($"##accent-{name}", new Vector2(swatch, swatch)))
                        ApplyAccent(color, true);

                    bool hovered = ImGui.IsItemHovered();
                    if (hovered)
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                        ImGui.SetTooltip(name);
                    }

                    bool selected = IsSameAccent(color, UiTheme.GlobalAccent);
                    draw.AddRectFilled(pos, pos + new Vector2(swatch, swatch), ImGui.GetColorU32(color), theme.Radius(0.9f));

                    if (selected || hovered)
                    {
                        float ring = theme.Scaled(3f);
                        draw.AddRect(
                            pos - new Vector2(ring, ring),
                            pos + new Vector2(swatch + ring, swatch + ring),
                            ImGui.GetColorU32(selected ? theme.Text : theme.MutedText),
                            theme.Radius(1.1f),
                            ImDrawFlags.None,
                            theme.Scaled(2f));
                    }
                }

                ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + swatch + theme.Gap(1.5f)));

                ImGui.TextColored(theme.Text, "Custom");
                theme.SpacerY(0.25f);

                if (ImGui.ColorEdit3("##accent-custom", ref _accentEdit, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.DisplayHex))
                {
                    ApplyAccent(new Vector4(_accentEdit.X, _accentEdit.Y, _accentEdit.Z, 1f), false);
                    _accentDirty = true;
                }

                if (_accentDirty && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    plugin.Config.Appearance.AccentColor = UiTheme.ToAccentHex(UiTheme.GlobalAccent);
                    plugin.Config.Save();
                    _accentDirty = false;
                }

                ImGui.SameLine();

                if (theme.SecondaryButton("Reset", new Vector2(80, 0)))
                    ApplyAccent(UiTheme.DefaultAccent, true);
                theme.HoverHandIfItem();
            }
        );
    }

    private void ApplyAccent(Vector4 color, bool save)
    {
        UiTheme.GlobalAccent = new Vector4(color.X, color.Y, color.Z, 1f);
        _accentEdit = new Vector3(color.X, color.Y, color.Z);

        if (!save)
            return;

        plugin.Config.Appearance.AccentColor = UiTheme.ToAccentHex(UiTheme.GlobalAccent);
        plugin.Config.Save();
        _accentDirty = false;
    }

    private static bool IsSameAccent(Vector4 a, Vector4 b)
        => MathF.Abs(a.X - b.X) < 0.004f && MathF.Abs(a.Y - b.Y) < 0.004f && MathF.Abs(a.Z - b.Z) < 0.004f;

    private void DrawDiscord()
    {
        Card.Draw(
            "bot-token-settings",
            innerWidth =>
            {
                ImGui.TextColored(theme.MutedText, "The bot token Cordi uses to connect to Discord.");
                theme.SpacerY(0.5f);

                var origin = ImGui.GetCursorScreenPos();
                float gap = theme.Gap(0.7f);
                float buttonWidth = theme.Scaled(90f);
                float rowHeight = theme.Scaled(UiTheme.ControlHeight);
                float tokenWidth = MathF.Max(theme.Scaled(160f), innerWidth - buttonWidth * 2f - gap * 2f);

                var flags = _botTokenInputActive ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
                theme.TextInput("##bot-token-input", origin, tokenWidth, ref _botToken, 256, "Paste your bot token", flags);
                _botTokenInputActive = ImGui.IsItemActive();

                ImGui.SetCursorScreenPos(new Vector2(origin.X + tokenWidth + gap, origin.Y));
                if (theme.PrimaryButton("Save##bot-token", new Vector2(buttonWidth, rowHeight)))
                {
                    plugin.Config.Discord.BotToken = _botToken;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SetCursorScreenPos(new Vector2(origin.X + tokenWidth + buttonWidth + gap * 2f, origin.Y));
                if (theme.SecondaryButton("Reload##bot-token", new Vector2(buttonWidth, rowHeight)))
                    _botToken = plugin.Config.Discord.BotToken ?? string.Empty;
                theme.HoverHandIfItem();

                ImGui.SetCursorScreenPos(origin);
                ImGui.Dummy(new Vector2(innerWidth, rowHeight));
            },
            label: "Bot Token",
            drawTrailing: anchor => theme.HelpPill(
                "bot-token-help",
                anchor,
                "Create an application at discord.com/developers, add a bot to it\n" +
                "and copy the token from the Bot page.\n" +
                "The token is stored in your local Cordi config and never leaves your machine."));

        DrawTargetUserCard();
    }

    private void DrawTargetUserCard()
    {
        var config = plugin.Config.ActivityConfig;
        bool hasTarget = config.TargetUserId != 0;

        Card.Draw(
            "discord-presence",
            innerWidth => Row.Draw(
                id: "target-user-id",
                icon: hasTarget ? FontAwesomeIcon.UserCheck : FontAwesomeIcon.ExclamationTriangle,
                iconColor: hasTarget ? UiTheme.TileGreen : UiTheme.TileAmber,
                title: "Target User ID",
                subtitle: hasTarget
                    ? "Used for activity titles and for chatbox mentions of you"
                    : "Not set, Discord driven activity types stay idle and mentions of you are missed",
                controlWidth: 220f,
                drawControl: (pos, width) =>
                {
                    string value = hasTarget ? config.TargetUserId.ToString() : string.Empty;

                    if (theme.TextInput("##target-user-id", pos, width, ref value, 20, "000000000000000000"))
                    {
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            config.TargetUserId = 0;
                            plugin.Config.Save();
                        }
                        else if (ulong.TryParse(value, out var parsed))
                        {
                            config.TargetUserId = parsed;
                            plugin.Config.Save();
                        }
                    }
                },
                rowWidth: innerWidth),
            label: "Presence",
            drawTrailing: anchor => theme.HelpPill(
                "target-user-help",
                anchor,
                "Enable Developer Mode in Discord, right click your account\n" +
                "and choose Copy User ID.\n" +
                "Cordi watches this account to turn its Discord activity into an in game title,\n" +
                "and uses the same ID to spot chatbox mentions of you."));
    }

    private void DrawFont()
    {
        var fontConfig = plugin.Config.Font;

        bool dummyEnabled = true;
        theme.DrawPluginCardAuto(
            id: "font-settings",
            enabled: ref dummyEnabled,
            showCheckbox: false,
            title: "Font Settings",
            drawContent: (avail) =>
            {
                ImGui.TextDisabled("Adjust the font size and style used across all Cordi windows.");
                theme.SpacerY(0.5f);

                ImGui.TextColored(theme.Text, "Font Size");
                theme.SpacerY(0.25f);

                float scale = _tempFontScale ?? fontConfig.GlobalScale;
                using (var width2 = ImRaii.ItemWidth(200))
                {
                    if (ImGui.SliderFloat("##fontScale", ref scale, 0.7f, 2.0f, $"{scale:F2}x"))
                    {
                        _tempFontScale = scale;
                    }

                    if (ImGui.IsItemDeactivatedAfterEdit())
                    {
                        fontConfig.GlobalScale = _tempFontScale ?? scale;
                        UiTheme.GlobalFontScale = fontConfig.GlobalScale;
                        plugin.Config.Save();
                    }

                    if (ImGui.IsItemDeactivated())
                    {
                        _tempFontScale = null;
                    }
                }

                ImGui.SameLine();
                ImGui.TextColored(theme.MutedText, "Scale multiplier");

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Controls the font size across all Cordi windows. Default: 1.00x");
                }

                theme.SpacerY(1f);

                // Reset button
                if (theme.PrimaryButton("Reset to Defaults", new Vector2(150, 0)))
                {
                    _tempFontScale = null;
                    fontConfig.GlobalScale = 1.0f;
                    fontConfig.Bold = false;
                    UiTheme.GlobalFontScale = 1.0f;
                    UiTheme.GlobalFontBold = false;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();
            }
        );
    }
}
