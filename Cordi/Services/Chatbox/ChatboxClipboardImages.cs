using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Cordi.Services.Chatbox;

public static class ChatboxClipboardImages
{
    public static IReadOnlyList<ChatboxUpload> Read()
    {
        var uploads = new List<ChatboxUpload>();
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Clipboard.ContainsFileDropList())
                {
                    foreach (var path in Clipboard.GetFileDropList())
                    {
                        if (path == null || !ChatboxService.IsUploadFile(path) || !File.Exists(path)) continue;
                        if (new FileInfo(path).Length > ChatboxService.MaxUploadBytes) continue;

                        uploads.Add(new ChatboxUpload(Path.GetFileName(path), File.ReadAllBytes(path)));
                    }

                    return;
                }

                if (!Clipboard.ContainsImage()) return;

                using var image = Clipboard.GetImage();
                if (image == null) return;

                using var stream = new MemoryStream();
                image.Save(stream, ImageFormat.Png);
                uploads.Add(new ChatboxUpload("image.png", stream.ToArray()));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(2))) return Array.Empty<ChatboxUpload>();

        if (failure != null)
            Core.CordiPlugin.Plugin.LogService.Warning("Chatbox", $"Could not read an image from the clipboard: {failure.Message}");

        return uploads;
    }
}
