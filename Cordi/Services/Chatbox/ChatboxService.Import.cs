using System;
using System.Collections.Generic;
using Cordi.Configuration;
using Dalamud.Game.Text;

namespace Cordi.Services.Chatbox;

public sealed partial class ChatboxService
{
    public XivimLogImporter XivimLogs { get; private set; } = null!;

    public bool HasXivimHistory(string name, string world) => XivimLogs.HasLogFor(name, world);

    private void MaybeImportXivimHistory(ConversationConfig entry)
    {
        if (entry.XivimImported) return;
        if (!ConversationSettings.ImportXivimHistory) return;

        ImportXivimHistory(entry);
    }

    public int ImportXivimHistory(ConversationConfig entry)
    {
        entry.XivimImported = true;

        var path = XivimLogs.LocateLog(entry.Name, entry.World);
        if (path == null) return 0;

        var lines = XivimLogs.Read(path);
        if (lines.Count == 0) return 0;

        var config = BuildConversationChannel(entry);
        var known = Store.Fingerprints(entry.Id);
        var pending = new List<ChatboxMessage>(lines.Count);

        foreach (var line in lines)
        {
            var message = BuildImportedMessage(entry, config, line);
            if (message == null) continue;
            if (!known.Add(ChatboxMessageStore.Fingerprint(message))) continue;

            pending.Add(message);
        }

        if (pending.Count == 0) return 0;

        var start = Math.Min(0L, Store.LowestSeq()) - pending.Count;

        for (var i = 0; i < pending.Count; i++)
            pending[i].Seq = start + i;

        Store.InsertHistory(pending);

        var newest = pending[^1].Timestamp.ToUniversalTime().Ticks;
        if (newest > entry.LastActivityTicks) entry.LastActivityTicks = newest;

        RefreshImportedChannel(entry.Id);

        _plugin.LogService.Info(
            "Chatbox",
            $"Imported {pending.Count} message(s) for {entry.Label} from XIVInstantMessenger.");

        return pending.Count;
    }

    public (int People, int Messages) ImportAllXivimHistory()
    {
        var people = 0;
        var messages = 0;

        foreach (var path in XivimLogs.FindLogFiles())
        {
            if (!XivimLogImporter.TrySplitFileName(path, out var name, out var world)) continue;

            var id = ConversationId(name, world);
            var entry = FindConversation(id);
            var created = entry == null;

            if (entry == null)
            {
                entry = new ConversationConfig { Id = id, Name = name, World = world, Open = false };
                ConversationSettings.Items.Add(entry);
            }

            entry.XivimImported = false;
            var imported = ImportXivimHistory(entry);

            if (imported == 0)
            {
                if (created) ConversationSettings.Items.Remove(entry);
                continue;
            }

            people++;
            messages += imported;
        }

        _plugin.Config.Save();
        _plugin.ConversationWindows?.Sync();

        return (people, messages);
    }

    private ChatboxMessage? BuildImportedMessage(
        ConversationConfig entry,
        ChatboxChannelConfig config,
        XivimLogLine line)
    {
        var text = line.Text.ToString();
        if (text.Length == 0) return null;

        if (line.IsSystem)
        {
            return new ChatboxMessage
            {
                ChannelId = entry.Id,
                Origin = ChatboxOrigin.System,
                Timestamp = line.Timestamp,
                AuthorName = ChatboxMessage.SystemSender,
                RawContent = text,
            };
        }

        var incoming = string.Equals(line.Label, entry.Label, StringComparison.OrdinalIgnoreCase);
        var type = incoming ? XivChatType.TellIncoming : XivChatType.TellOutgoing;

        return new ChatboxMessage
        {
            ChannelId = entry.Id,
            Origin = ChatboxOrigin.Game,
            Timestamp = line.Timestamp,
            AuthorKey = line.Label,
            AuthorName = line.Author,
            AuthorWorld = line.World,
            GameChatType = type,
            RawContent = text,
            IsSelf = !incoming,
            TellTarget = incoming ? string.Empty : entry.Label,
            AuthorColor = ColorFor(config, type),
        };
    }

    private void RefreshImportedChannel(string id)
    {
        var state = GetChannel(id);
        if (state == null) return;

        if (state.Count == 0)
        {
            Hydrate(state);
            return;
        }

        state.HasMoreHistory = true;
    }
}
