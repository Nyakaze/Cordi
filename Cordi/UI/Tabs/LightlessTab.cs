using System;
using System.Numerics;
using Cordi.Core;
using Cordi.Services;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Tabs;

public class LightlessTab : ConfigTabBase
{
    private DateTime _lastPoll = DateTime.MinValue;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private bool _pluginInstalled;
    private bool? _isConnected;
    private string _stateRaw = "unknown";

    public override string Label => "Lightless";

    public LightlessTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme) { }

    public override void Draw()
    {
        PollIfDue();
        DrawCard();
        theme.SpacerY(1f);
        DrawDiagnosticsCard();
    }

    private void PollIfDue()
    {
        if ((DateTime.UtcNow - _lastPoll) < PollInterval) return;
        _lastPoll = DateTime.UtcNow;
        try
        {
            _pluginInstalled = plugin.Lightless.IsAvailable;
            _isConnected = plugin.Lightless.IsConnected();
            _stateRaw = plugin.Lightless.ConnectionStateRaw() ?? "unknown";
        }
        catch (Exception ex)
        {
            Service.Log.Debug($"[LightlessTab] Poll failed: {ex.Message}");
        }
    }

    private void DrawCard()
    {
        bool enabled = plugin.Config.Lightless.Enabled;
        theme.DrawPluginCardAuto(
            id: "lightless-disconnect-notify",
            title: "Disconnect Notification",
            enabled: ref enabled,
            showCheckbox: true,
            drawContent: (avail) =>
            {
                if (plugin.Config.Lightless.Enabled != enabled)
                {
                    plugin.Config.Lightless.Enabled = enabled;
                    plugin.Config.Save();
                }

                ImGui.TextWrapped(
                    $"Cordi keeps a live status embed in the selected channel and is updated in place. " +
                    $"When Lightless drops, a separate alert is posted (so Discord notifies you); react with " +
                    $"{LightlessConnectionMonitor.ReconnectEmoji} to trigger a reconnect. The alert is removed " +
                    $"automatically once the connection is back, keeping the channel clean.");
                theme.SpacerY(0.5f);

                DrawStatusLine();
                theme.SpacerY(0.5f);

                bool autoReconnect = plugin.Config.Lightless.AutoReconnect;
                if (theme.ConfigCheckbox("Auto Reconnect", ref autoReconnect, () =>
                {
                    plugin.Config.Lightless.AutoReconnect = autoReconnect;
                    plugin.Config.Save();
                }))
                {
                }
                theme.SpacerY(0.5f);


                using (ImRaii.Disabled(!enabled))
                {
                    theme.ChannelPicker(
                        "lightless-channel",
                        plugin.Config.Lightless.DiscordChannelId,
                        plugin.Channels.TextChannels,
                        (newId) =>
                        {
                            plugin.Config.Lightless.DiscordChannelId = newId;
                            plugin.Config.Save();
                        },
                        defaultLabel: "None");
                }
            });
    }

    private void DrawStatusLine()
    {
        ImGui.TextColored(theme.MutedText, "Plugin:");
        ImGui.SameLine();
        if (_pluginInstalled)
            ImGui.TextColored(UiTheme.ColorSuccessText, "Installed");
        else
            ImGui.TextColored(UiTheme.ColorDangerText, "Not installed");

        ImGui.SameLine();
        ImGui.TextColored(theme.MutedText, "   Connection:");
        ImGui.SameLine();
        switch (_isConnected)
        {
            case true: ImGui.TextColored(UiTheme.ColorSuccessText, _stateRaw); break;
            case false: ImGui.TextColored(UiTheme.ColorDangerText, _stateRaw); break;
            default: ImGui.TextColored(theme.MutedText, "Unknown — deep integration not resolved"); break;
        }
    }

    private void DrawDiagnosticsCard()
    {
        if (!ImGui.CollapsingHeader("Diagnostics##lightless-diag"))
            return;

        var r = plugin.Lightless.Reflection;

        DrawStage("Assembly", r.AssemblyResolved, r.AssemblyName);
        DrawStage("Plugin instance", r.PluginInstanceResolved, r.PluginTypeName);
        DrawStage("ApiController", r.ApiControllerResolved, r.ApiControllerTypeName);
        DrawStage("Connection-state member", r.ConnectionStateResolved, r.ConnectionStateMemberName);
        DrawStage("Reconnect method", r.ReconnectResolved, r.ReconnectMethodName);
        DrawStage("PairManager", r.PairManagerResolved, r.PairManagerResolved ? $"Resolved (Pairs count: {r.GetPairCount()?.ToString() ?? "unknown"})" : "Not resolved");

        theme.SpacerY(0.5f);

        if (theme.SecondaryButton("Re-probe", new Vector2(120, 26)))
        {
            r.Reset();
            _lastPoll = DateTime.MinValue;
        }
        theme.HoverHandIfItem();

        ImGui.SameLine();
        if (theme.SecondaryButton("Dump ApiController methods", new Vector2(220, 26)))
        {
            r.DumpApiControllerMethods();
        }
        theme.HoverHandIfItem();

        ImGui.SameLine();
        if (theme.SecondaryButton("Dump PairManager", new Vector2(160, 26)))
        {
            r.DumpPairManagerMembers();
        }
        theme.HoverHandIfItem();

        ImGui.SameLine();
        ImGui.TextColored(theme.MutedText, "/xllog");
    }

    private void DrawStage(string label, bool ok, string? detail)
    {
        ImGui.TextColored(ok ? UiTheme.ColorSuccessText : UiTheme.ColorDangerText, ok ? "[OK]" : "[--]");
        ImGui.SameLine();
        ImGui.TextColored(theme.MutedText, label);
        if (!string.IsNullOrEmpty(detail))
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("→");
            ImGui.SameLine();
            ImGui.TextUnformatted(detail);
        }
    }
}
