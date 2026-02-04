

using System;
using Dalamud.Interface.Windowing;
using System.Numerics;
using Dalamud.Bindings.ImGui;


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
        theme.BeginCard("wooowi");
        ImGui.Text("Allgemein");
        ImGui.Separator();
        theme.SpacerY(0.25f);



        if (theme.PrimaryButton("Speichern")) cfg.Config.Save();
        theme.SameLineGap();
        if (theme.SecondaryButton("Zurücksetzen")) { cfg.Config.Save(); }

        theme.EndCard();

        theme.SpacerY();

        if (theme.BeginTabBar("settings-tabs"))
        {
            if (theme.BeginTabItem("Erweitert"))
            {
                theme.MutedLabel("Erweiterte Optionen…");
                theme.SpacerY(0.5f);

                theme.SameLineGap();
                theme.Badge("Beta");

                theme.EndTabItem();
            }
            if (theme.BeginTabItem("Theming"))
            {
                theme.MutedLabel("Akzentfarbe & Presets:");
                theme.SpacerY(0.5f);


                theme.EndTabItem();
            }
            theme.EndTabBar();
        }
    }

    public void Dispose() { }
}
