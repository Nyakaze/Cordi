using System;
using System.Collections.Generic;
using Dalamud.Interface;

namespace Cordi.UI.Components;

public sealed class NavItem
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required FontAwesomeIcon Icon { get; init; }
    public required Action Draw { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public bool OwnHeader { get; init; }

    public string HeaderTitle => string.IsNullOrEmpty(Title) ? Label : Title;
}

public sealed class NavSection
{
    public required string Label { get; init; }
    public required IReadOnlyList<NavItem> Items { get; init; }
}
