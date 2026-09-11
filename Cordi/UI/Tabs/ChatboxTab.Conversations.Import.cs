using System;
using Cordi.UI.Themes;
using Dalamud.Interface;

namespace Cordi.UI.Tabs;

public partial class ChatboxTab
{
    private static readonly TimeSpan XivimScanLifetime = TimeSpan.FromSeconds(5);

    private DateTime xivimScanStamp = DateTime.MinValue;
    private int xivimLogCount;
    private string xivimImportStatus = string.Empty;

    private void DrawConversationImportCard()
    {
        RefreshXivimScan();

        Card.Draw(
            "conversation-xivim",
            innerWidth =>
            {
                DrawInfoRow(
                    "conversation-xivim-status",
                    xivimLogCount > 0 ? FontAwesomeIcon.FolderOpen : FontAwesomeIcon.FolderMinus,
                    xivimLogCount > 0
                        ? $"{xivimLogCount} log file(s) found"
                        : "No XIVInstantMessenger logs found",
                    XivimStatusSubtitle(),
                    innerWidth);

                DrawToggleRow(
                    "conversation-xivim-auto",
                    FontAwesomeIcon.FileImport,
                    "Import when a conversation opens",
                    "The first time a conversation opens, Cordi looks for that person's XIVInstantMessenger log and pulls it in.",
                    innerWidth,
                    () => Ccfg.ImportXivimHistory,
                    SetXivimImportEnabled,
                    UiTheme.TileGreen);

                DrawTextRow(
                    "conversation-xivim-folder",
                    FontAwesomeIcon.Folder,
                    "Log folder",
                    "Leave this empty unless XIVInstantMessenger was pointed at a custom log folder.",
                    innerWidth,
                    () => Ccfg.XivimLogFolder,
                    SetXivimLogFolder,
                    260,
                    plugin.Chatbox.XivimLogs.DefaultFolder());

                if (xivimLogCount == 0)
                    return;

                DrawActionRow(
                    "conversation-xivim-run",
                    FontAwesomeIcon.Download,
                    UiTheme.TileBlue,
                    "Import everything now",
                    "Reads every log in the folder and adds the messages to the matching conversation. Running it twice changes nothing.",
                    "Import",
                    innerWidth,
                    RunXivimImport);
            },
            "XIVInstantMessenger");
    }

    private string XivimStatusSubtitle()
    {
        if (xivimImportStatus.Length > 0) return xivimImportStatus;

        var folder = plugin.Chatbox.XivimLogs.ResolveFolder();

        return xivimLogCount > 0
            ? $"Imported messages sit in front of Cordi's own history, behind \"Load older\". Reading from {folder}."
            : $"Nothing to import. Looked in {folder}.";
    }

    private void SetXivimImportEnabled(bool value)
    {
        Ccfg.ImportXivimHistory = value;
        Save();
    }

    private void SetXivimLogFolder(string value)
    {
        Ccfg.XivimLogFolder = value;
        xivimScanStamp = DateTime.MinValue;
        xivimImportStatus = string.Empty;
        Save();
    }

    private void RunXivimImport()
    {
        try
        {
            var (people, messages) = plugin.Chatbox.ImportAllXivimHistory();

            xivimImportStatus = messages == 0
                ? "Nothing new to import — every log had already been ported."
                : $"Imported {messages} message(s) across {people} conversation(s).";
        }
        catch (Exception ex)
        {
            xivimImportStatus = "The import failed. See the log for details.";
            plugin.LogService.Error("UI", "Failed to import XIVInstantMessenger logs.", ex);
        }

        threadCacheStamp = DateTime.MinValue;
    }

    private void RefreshXivimScan()
    {
        var now = DateTime.UtcNow;
        if (now - xivimScanStamp < XivimScanLifetime) return;

        xivimScanStamp = now;

        try
        {
            xivimLogCount = plugin.Chatbox.XivimLogs.FindLogFiles().Count;
        }
        catch (Exception ex)
        {
            xivimLogCount = 0;
            plugin.LogService.Error("UI", "Failed to scan for XIVInstantMessenger logs.", ex);
        }
    }
}
