using System;
using System.Collections.Generic;
using Dalamud.Interface.Utility;
using Dalamud.Bindings.ImGui;
using Cordi.Core;
using Cordi.UI.Themes;
using Cordi.Configuration;

namespace Cordi.UI.Tabs;

public partial class ChatsTab : ConfigTabBase
{
    private bool extraChatExpanded = false;
    private bool existingAvatarsExpanded = false;
    private (string Key, ExtraChatConnection Value)? extraChatAddState = null;
    private readonly Services.Features.ExtraChatService extraChatService;

    public override string Label => "Chats";

    public ChatsTab(CordiPlugin plugin, UiTheme theme) : base(plugin, theme)
    {
        extraChatService = new Cordi.Services.Features.ExtraChatService(plugin);
    }

    protected override IReadOnlyList<(string Label, Action Draw)> GetSubTabs()
    {
        var tabs = new List<(string Label, Action Draw)>
        {
            ("Channel Mappings", () => DrawChatMappingsPage(plugin.Channels.TextChannels, plugin.Channels.ForumChannels)),
            ("Custom Avatars", () => DrawExistingAvatarsCard(ref existingAvatarsExpanded))
        };

        tabs.Add(("Advertisement Filter", () => DrawAdvertisementFilterPage(plugin.Config.AdvertisementFilter)));

        return tabs;
    }

    private void DrawExistingAvatarsCard(ref bool enabled)
    {
        var avatars = plugin.Config.Chat.CustomAvatars;
        var headers = new[] { "Character", "URL", "Action" };

        Action setupCols = () =>
        {
            ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthFixed, 150f * ImGuiHelpers.GlobalScale);
            ImGui.TableSetupColumn("URL", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
        };

        theme.DrawDictionaryTable(
            "customAvatars",
            $"Custom Avatars: {avatars.Count}",
            ref existingAvatarsExpanded,
            avatars,
            () =>
            {
                plugin.Config.Save();
                // Invalidate all for safety since we don't know exactly which one changed in this generic callback, 
                // but for avatars it's cheap enough.
                foreach (var key in avatars.Keys) plugin.Lodestone.InvalidateAvatarCache(key);
            },
            headers,
            setupColumns: setupCols,
            allowAdd: true,
            collapsible: false
        );
    }

}
