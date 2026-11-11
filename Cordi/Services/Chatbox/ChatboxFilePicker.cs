using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace Cordi.Services.Chatbox;

public static class ChatboxFilePicker
{
    private static int _open;

    private sealed class Owner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle => handle;
    }

    public static void PickImages(Action<string[]> picked)
    {
        if (Interlocked.Exchange(ref _open, 1) == 1) return;

        var owner = Process.GetCurrentProcess().MainWindowHandle;

        var thread = new Thread(() =>
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Attach images",
                    Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.webp",
                    Multiselect = true,
                    CheckFileExists = true,
                };

                var result = owner != IntPtr.Zero ? dialog.ShowDialog(new Owner(owner)) : dialog.ShowDialog();
                if (result == DialogResult.OK) picked(dialog.FileNames);
            }
            catch (Exception ex)
            {
                Core.CordiPlugin.Plugin.LogService.Warning("Chatbox", $"Could not open the file picker: {ex.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _open, 0);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }
}
