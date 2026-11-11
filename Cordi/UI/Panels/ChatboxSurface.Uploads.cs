using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Cordi.Configuration;
using Cordi.Services.Chatbox;
using Cordi.UI.Themes;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility.Raii;

namespace Cordi.UI.Panels;

public sealed partial class ChatboxSurface
{
    private const string UploadDragLabel = "CordiChatboxUpload";
    private const string UploadUnmapped = "Images need a chat that is mapped to a Discord channel.";

    private sealed class PendingUpload
    {
        public required ChatboxUpload File { get; init; }
        public IDalamudTextureWrap? Preview { get; set; }
        public bool Removed { get; set; }
    }

    private readonly List<PendingUpload> _uploads = new();
    private readonly ConcurrentQueue<(ChatboxChannelState Channel, string Path)> _pickedUploads = new();

    private string _meterInput = string.Empty;
    private int _meterLength;
    private long _meterReplySeq;
    private string _meterChannel = string.Empty;

    private void AddUpload(ChatboxChannelState channel, string fileName, byte[] bytes)
    {
        if (!Chatbox.CanUpload(channel))
        {
            Chatbox.PostSystemMessage(channel.Id, UploadUnmapped);
            return;
        }

        if (!ChatboxService.IsUploadFile(fileName))
        {
            Chatbox.PostSystemMessage(channel.Id, $"{fileName} is not a png, jpg, gif or webp image.");
            return;
        }

        if (bytes.Length == 0 || bytes.Length > ChatboxService.MaxUploadBytes)
        {
            Chatbox.PostSystemMessage(channel.Id, $"{fileName} is larger than Discord's 10 MB upload limit.");
            return;
        }

        if (_uploads.Count >= ChatboxService.MaxUploads)
        {
            Chatbox.PostSystemMessage(channel.Id, $"A message can carry up to {ChatboxService.MaxUploads} images.");
            return;
        }

        var pending = new PendingUpload { File = new ChatboxUpload(fileName, bytes) };
        _uploads.Add(pending);
        _focusInput = true;
        _silentFocus = true;

        _ = Chatbox.ImageCache.CreatePreviewAsync(bytes).ContinueWith(task =>
        {
            if (!task.IsCompletedSuccessfully || task.Result == null) return;

            if (pending.Removed) task.Result.Dispose();
            else pending.Preview = task.Result;
        });
    }

    private void AddUploadFromPath(ChatboxChannelState channel, string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return;

            var bytes = info.Length > ChatboxService.MaxUploadBytes ? Array.Empty<byte>() : File.ReadAllBytes(path);
            AddUpload(channel, info.Name, bytes);
        }
        catch (Exception ex)
        {
            _plugin.LogService.Warning("Chatbox", $"Could not read {path}: {ex.Message}");
        }
    }

    private void PasteUploads(ChatboxChannelState channel)
    {
        foreach (var upload in ChatboxClipboardImages.Read())
            AddUpload(channel, upload.FileName, upload.Bytes);
    }

    private void RemoveUpload(PendingUpload upload)
    {
        upload.Removed = true;
        upload.Preview?.Dispose();
        upload.Preview = null;
        _uploads.Remove(upload);
    }

    private void ClearUploads()
    {
        foreach (var upload in _uploads.ToArray())
            RemoveUpload(upload);
    }

    private IReadOnlyList<ChatboxUpload>? TakeUploads()
    {
        if (_uploads.Count == 0) return null;

        var files = _uploads.Select(upload => upload.File).ToList();
        ClearUploads();

        return files;
    }

    private void DrawUploadButton(ChatboxChannelState channel, float spacing)
    {
        var allowed = Chatbox.CanUpload(channel);

        ImGui.SameLine(0, spacing);

        using (ImRaii.Disabled(!allowed))
        {
            if (_theme.IconButton("##chatbox-attach", FontAwesomeIcon.Paperclip))
            {
                ChatboxFilePicker.PickImages(paths =>
                {
                    foreach (var path in paths)
                        _pickedUploads.Enqueue((channel, path));
                });
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            _theme.Tooltip(allowed ? "Attach images. You can also paste or drop them here." : UploadUnmapped);

        while (_pickedUploads.TryDequeue(out var picked))
            AddUploadFromPath(picked.Channel, picked.Path);
    }

    private void OfferUploadDrop()
    {
        if (!Chatbox.UploadsConnected || !Service.DragDrop.ServiceAvailable) return;

        Service.DragDrop.CreateImGuiSource(
            UploadDragLabel,
            manager => manager.Files.Any(ChatboxService.IsUploadFile),
            _ =>
            {
                ImGui.TextUnformatted("Drop to attach to your message");
                return true;
            });
    }

    private void AcceptUploadDrop(ChatboxChannelState channel)
    {
        if (!Chatbox.UploadsConnected || !Service.DragDrop.ServiceAvailable) return;
        if (!Service.DragDrop.CreateImGuiTarget(UploadDragLabel, out var files, out _)) return;

        foreach (var path in files.Where(ChatboxService.IsUploadFile))
            AddUploadFromPath(channel, path);
    }

    private void DrawUploadStrip()
    {
        var size = _theme.Scaled(UiTheme.UploadThumbSize);
        var gap = _theme.Gap(0.4f);
        var closeSize = _theme.Scaled(16f);
        var origin = ImGui.GetCursorScreenPos();
        PendingUpload? removed = null;

        for (var i = 0; i < _uploads.Count; i++)
        {
            var upload = _uploads[i];
            var min = origin + new Vector2(i * (size + gap), 0f);
            var max = min + new Vector2(size, size);

            _theme.Thumbnail(min, max, upload.Preview);

            if (ImGui.IsMouseHoveringRect(min, max))
                _theme.Tooltip($"{upload.File.FileName}, {upload.File.Bytes.Length / 1024f:0} KB");

            if (_theme.IconAction(
                    $"chatbox-upload-remove-{i}",
                    new Vector2(max.X - closeSize, min.Y),
                    FontAwesomeIcon.Times,
                    _theme.Accent,
                    "Remove",
                    closeSize,
                    closeSize))
                removed = upload;
        }

        if (removed != null) RemoveUpload(removed);

        ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + size + _theme.Gap(0.3f)));
    }

    private void DrawInputMeter(ChatboxChannelState channel, Vector2 inputMin, Vector2 inputMax)
    {
        var replySeq = _replyTarget?.Seq ?? 0;

        if (!string.Equals(_meterInput, _input, StringComparison.Ordinal) || _meterReplySeq != replySeq || _meterChannel != channel.Id)
        {
            _meterInput = _input;
            _meterReplySeq = replySeq;
            _meterChannel = channel.Id;
            _meterLength = Chatbox.OutgoingLength(channel, _emoteFont.Expand(_input), _replyTarget);
        }

        const int limit = ChatboxConfig.MaxMessageLength;

        var length = _meterLength;
        if (_uploads.Count > 0)
            length += _uploads.Count * ChatboxService.UploadLinkLength + (length > 0 ? _uploads.Count : _uploads.Count - 1);

        var fraction = length / (float)limit;
        var color = fraction > 1f ? UiTheme.TileRed : fraction >= 0.85f ? UiTheme.TileAmber : _theme.Accent;

        var gap = _theme.Gap(0.2f);
        var height = _theme.Scaled(UiTheme.InputMeterHeight);
        var min = new Vector2(inputMin.X, inputMax.Y + gap);
        var max = new Vector2(inputMax.X, min.Y + height);

        _theme.Meter(min, max, fraction, color);

        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, inputMax.Y));
        ImGui.Dummy(new Vector2(0f, gap * 2f + height));

        if (!ImGui.IsMouseHoveringRect(new Vector2(min.X, inputMax.Y), new Vector2(max.X, max.Y + gap))) return;

        var summary = $"{length} of {limit} characters";

        if (_uploads.Count > 0)
            summary += $"\nIncludes about {ChatboxService.UploadLinkLength} characters for each image link.";

        if (length > limit)
        {
            summary += _uploads.Count > 0 || Config.SplitLongMessages
                ? "\nToo long for one message, so it goes out in several."
                : "\nToo long for one message, so the end is cut off.";
        }

        _theme.Tooltip(summary);
    }
}
