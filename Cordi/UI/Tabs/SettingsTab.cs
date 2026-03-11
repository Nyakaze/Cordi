using System;
using System.Collections.Generic;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Core;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;

namespace Cordi.UI.Tabs;

public class SettingsTab : ConfigTabBase
{
    private string _botToken = string.Empty;
    private bool _botTokenInputActive = false;
    private float? _tempFontScale = null;

    public override string Label => "Settings";

    public SettingsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        _botToken = plugin.Config.Discord.BotToken ?? string.Empty;
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        return new (string, Action)[]
        {
            ("Discord", DrawDiscord),
            ("Font", DrawFont),
        };
    }

    private void DrawDiscord()
    {
        bool dummyEnabled = true;
        theme.DrawPluginCardAuto(
            id: "bot-token-settings",
            enabled: ref dummyEnabled,
            showCheckbox: false,
            title: "Bot Token",
            drawContent: (avail) =>
            {
                ImGui.TextDisabled("Configure the Discord bot token used to connect to your server.");
                theme.SpacerY(0.5f);

                ImGui.TextColored(theme.Text, "Token");
                theme.SpacerY(0.25f);

                var flags = _botTokenInputActive ? ImGuiInputTextFlags.None : ImGuiInputTextFlags.Password;
                ImGui.PushItemWidth(avail);
                if (ImGui.InputText("##bot-token-input", ref _botToken, 256, flags))
                {
                }
                _botTokenInputActive = ImGui.IsItemActive();
                ImGui.PopItemWidth();

                theme.SpacerY(0.5f);

                if (theme.PrimaryButton("Save", new Vector2(80, 0)))
                {
                    plugin.Config.Discord.BotToken = _botToken;
                    plugin.Config.Save();
                }
                theme.HoverHandIfItem();

                ImGui.SameLine();

                if (theme.SecondaryButton("Reload", new Vector2(80, 0)))
                {
                    _botToken = plugin.Config.Discord.BotToken ?? string.Empty;
                }
                theme.HoverHandIfItem();
            }
        );
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
                ImGui.PushItemWidth(200);
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
                ImGui.PopItemWidth();

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
