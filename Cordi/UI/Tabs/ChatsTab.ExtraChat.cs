using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using DiscordChannel = Crovus.Models.DiscordChannel;

namespace Cordi.UI.Tabs;

public partial class ChatsTab
{
    private bool IsExtraChatAvailable
    {
        get
        {
#if DEBUG || CORDI_DEV
            return true;
#else
            return extraChatService.IsExtraChatInstalled();
#endif
        }
    }

    private void DrawExtraChatCard(IReadOnlyList<DiscordChannel>? textChannels)
    {
        Card.Draw(
            "extrachat-rows",
            innerWidth =>
            {
                theme.PushInputScope();

                var mappings = plugin.Config.Chat.ExtraChatMappings;

                if (mappings.Count == 0)
                {
                    using (ImRaii.PushColor(ImGuiCol.Text, theme.MutedText))
                        ImGui.TextUnformatted("No ExtraChat channels mapped yet. Sync from ExtraChat or add one manually.");

                    theme.SpacerY(0.8f);
                }

                foreach (var pair in mappings.ToList())
                    DrawExtraChatRow(pair.Key, pair.Value, textChannels, innerWidth);

                theme.SpacerY(0.6f);
                DrawExtraChatFooter(textChannels, innerWidth);

                theme.PopInputScope();
            },
            label: "ExtraChat Mappings",
            drawTrailing: anchor => DrawHelpPill(
                "extrachat-help",
                anchor,
                "Channels are added automatically once a message is sent in them.\n" +
                "The label is the name shown in game chat, for example ECLS1.\n" +
                "The number is the channel used to send, for example 1 for /ecl1."));
    }

    private void DrawExtraChatRow(string key, ExtraChatConnection connection, IReadOnlyList<DiscordChannel>? textChannels, float rowWidth)
    {
        string subtitle = connection.ExtraChatNumber > 0
            ? $"Relays to /ecl{connection.ExtraChatNumber}"
            : "No channel number set, messages are not sent back";

        Row.Draw(
            id: $"extrachat-{key}",
            icon: FontAwesomeIcon.ProjectDiagram,
            iconColor: UiTheme.TilePurple,
            title: key,
            subtitle: subtitle,
            controlWidth: 340f,
            drawControl: (pos, width) =>
            {
                float gap = theme.Gap(0.5f);
                float numWidth = theme.Scaled(58f);
                float deleteWidth = theme.Scaled(30f);
                float pickerWidth = width - numWidth - deleteWidth - gap * 2f;
                float frameHeight = ImGui.GetFrameHeight();
                float dropdownHeight = theme.Scaled(34f);
                float frameOffset = (dropdownHeight - frameHeight) * 0.5f;

                ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + frameOffset));
                ImGui.SetNextItemWidth(numWidth);
                int number = connection.ExtraChatNumber;
                if (ImGui.InputInt($"##ec-num-{key}", ref number, 0))
                {
                    connection.ExtraChatNumber = Math.Clamp(number, 0, 8);
                    plugin.Config.Save();
                }

                ImGui.SetCursorScreenPos(new Vector2(pos.X + numWidth + gap, pos.Y));
                theme.ChannelPicker(
                    $"ec-chan-{key}",
                    connection.DiscordChannelId ?? string.Empty,
                    textChannels,
                    newId =>
                    {
                        connection.DiscordChannelId = newId;
                        plugin.Config.Save();
                    },
                    defaultLabel: "Select a Channel...",
                    showLabel: false,
                    width: pickerWidth);

                ImGui.SetCursorScreenPos(new Vector2(pos.X + numWidth + pickerWidth + gap * 2f, pos.Y + frameOffset));
                if (theme.DangerIconButton($"##ec-del-{key}", FontAwesomeIcon.Trash, "Remove mapping"))
                {
                    plugin.Config.Chat.ExtraChatMappings.Remove(key);
                    plugin.Config.Save();
                }
            },
            rowWidth: rowWidth);
    }

    private void DrawExtraChatFooter(IReadOnlyList<DiscordChannel>? textChannels, float innerWidth)
    {
        if (extraChatAddState != null)
            DrawExtraChatAddRow(textChannels, innerWidth);

        float gap = theme.Gap();
        float buttonWidth = (innerWidth - gap) * 0.5f;
        float buttonHeight = theme.Scaled(32f);
        var basePos = ImGui.GetCursorScreenPos();

        ImGui.SetCursorScreenPos(basePos);
        if (theme.SecondaryButton("Sync from ExtraChat", new Vector2(buttonWidth, buttonHeight)))
        {
            int count = extraChatService.SyncFromExtraChat();
            plugin.NotificationManager.Add("ExtraChat Sync", $"Synced {count} channels from ExtraChat.", CordiNotificationType.Success);
        }

        ImGui.SetCursorScreenPos(new Vector2(basePos.X + buttonWidth + gap, basePos.Y));
        if (theme.SecondaryButton("Add Mapping", new Vector2(buttonWidth, buttonHeight)))
            extraChatAddState = (string.Empty, new ExtraChatConnection());

        ImGui.SetCursorScreenPos(new Vector2(basePos.X, basePos.Y + buttonHeight + theme.Gap(0.6f)));
    }

    private void DrawExtraChatAddRow(IReadOnlyList<DiscordChannel>? textChannels, float innerWidth)
    {
        var state = extraChatAddState!.Value;
        var basePos = ImGui.GetCursorScreenPos();

        float gap = theme.Gap(0.5f);
        float rowHeight = theme.Scaled(34f);
        float frameHeight = ImGui.GetFrameHeight();
        float frameOffset = (rowHeight - frameHeight) * 0.5f;
        float buttonWidth = theme.Scaled(30f);
        float numWidth = theme.Scaled(58f);
        float keyWidth = theme.Scaled(130f);
        float pickerWidth = innerWidth - keyWidth - numWidth - buttonWidth * 2f - gap * 4f;

        string key = state.Key;
        ImGui.SetCursorScreenPos(new Vector2(basePos.X, basePos.Y + frameOffset));
        ImGui.SetNextItemWidth(keyWidth);
        ImGui.InputTextWithHint("##ec-add-key", "Label (ECLS1)", ref key, 64);

        float cursorX = basePos.X + keyWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(cursorX, basePos.Y + frameOffset));
        ImGui.SetNextItemWidth(numWidth);
        int number = state.Value.ExtraChatNumber;
        if (ImGui.InputInt("##ec-add-num", ref number, 0))
            state.Value.ExtraChatNumber = Math.Clamp(number, 0, 8);

        cursorX += numWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(cursorX, basePos.Y));
        theme.ChannelPicker(
            "ec-add-chan",
            state.Value.DiscordChannelId ?? string.Empty,
            textChannels,
            newId => state.Value.DiscordChannelId = newId,
            defaultLabel: "Select a Channel...",
            showLabel: false,
            width: pickerWidth);

        extraChatAddState = (key, state.Value);

        cursorX += pickerWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(cursorX, basePos.Y + frameOffset));
        if (theme.SuccessIconButton("##ec-add-save", FontAwesomeIcon.Check, "Add mapping"))
        {
            var mappings = plugin.Config.Chat.ExtraChatMappings;
            if (!string.IsNullOrWhiteSpace(key) && !mappings.ContainsKey(key))
            {
                mappings[key] = state.Value;
                extraChatAddState = null;
                plugin.Config.Save();
            }
        }

        cursorX += buttonWidth + gap;
        ImGui.SetCursorScreenPos(new Vector2(cursorX, basePos.Y + frameOffset));
        if (theme.SecondaryIconButton("##ec-add-cancel", FontAwesomeIcon.Times, "Cancel"))
            extraChatAddState = null;

        ImGui.SetCursorScreenPos(new Vector2(basePos.X, basePos.Y + rowHeight + theme.Gap(0.6f)));
    }
}
