using System;
using System.Collections.Generic;
using Cordi.Core;
using Cordi.UI.Themes;

namespace Cordi.UI.Tabs;

public class PartyAndPlayersTab : ConfigTabBase
{
    private readonly PartyTab partyTab;
    private readonly RememberMeTab rememberMeTab;

    public override string Label => "Party & Players";

    public PartyAndPlayersTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        this.partyTab = new PartyTab(plugin, theme);
        this.rememberMeTab = new RememberMeTab(plugin, theme);
    }

    protected override IReadOnlyList<(string Label, Action Draw)>? GetSubTabs() => new (string, Action)[]
    {
        ("Party", partyTab.Draw),
        ("Remember Me", rememberMeTab.Draw),
    };
}
