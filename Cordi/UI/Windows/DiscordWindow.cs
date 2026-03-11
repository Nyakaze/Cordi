

using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;


using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Windows;

public sealed class DiscordWindow : Window, IDisposable
{
    private readonly UiTheme theme = new UiTheme();
    private readonly CordiPlugin cfg;

    public DiscordWindow(CordiPlugin cfg)
        : base("DeinPlugin – Einstellungen",
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.None)
    {
        this.cfg = cfg;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        RespectCloseHotkey = true;
    }

    public override void PreDraw() => theme.PushWindow();
    public override void PostDraw() => theme.PopWindow();

    public override void Draw()
    {
        theme.ApplyFontScale();
        using (var card = theme.CardScope("wooowi"))
        {
            ImGui.Text("Allgemein");
            ImGui.Separator();
            theme.SpacerY(0.25f);

            if (theme.PrimaryButton("Speichern")) cfg.Config.Save();
            theme.SameLineGap();
            if (theme.SecondaryButton("Zurücksetzen")) { cfg.Config.Save(); }
        }

        theme.SpacerY();

        using (ImRaii.PushColor(ImGuiCol.Tab, theme.Tab)
               .Push(ImGuiCol.TabActive, theme.TabActive)
               .Push(ImGuiCol.TabHovered, theme.TabHovered))
        using (ImRaii.PushStyle(ImGuiStyleVar.TabRounding, theme.Radius()))
        using (var tabBar = ImRaii.TabBar("settings-tabs"))
        if (tabBar)
        {
            using (var tabItemErweitert = ImRaii.TabItem("Erweitert"))
            if (tabItemErweitert)
            {
                theme.MutedLabel("Erweiterte Optionen…");
                theme.SpacerY(0.5f);

                theme.SameLineGap();
                theme.Badge("Beta");
            }
            using (var tabItemTheming = ImRaii.TabItem("Theming"))
            if (tabItemTheming)
            {
                theme.MutedLabel("Akzentfarbe & Presets:");
                theme.SpacerY(0.5f);
            }
        }
    }

    public void Dispose() { }
}
