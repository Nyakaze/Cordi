using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;

namespace Cordi.Services.Features;

public class ScreenshotService
{
    private static readonly IPluginLog Logger = Service.Log;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    /// <summary>
    /// Captures the FFXIV game window client area and returns the image as a PNG MemoryStream.
    /// Returns null if capture fails.
    /// </summary>
    public MemoryStream? CaptureGameWindow()
    {
        try
        {
            var hwnd = GetGameWindowHandle();
            if (hwnd == IntPtr.Zero)
            {
                Logger.Warning("[ScreenshotService] Could not find game window.");
                return null;
            }

            if (!GetClientRect(hwnd, out var clientRect))
            {
                Logger.Warning("[ScreenshotService] Failed to get client rect.");
                return null;
            }

            int width = clientRect.Right - clientRect.Left;
            int height = clientRect.Bottom - clientRect.Top;

            if (width <= 0 || height <= 0)
            {
                Logger.Warning($"[ScreenshotService] Invalid window dimensions: {width}x{height}");
                return null;
            }

            var topLeft = new POINT { X = 0, Y = 0 };
            ClientToScreen(hwnd, ref topLeft);

            using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(topLeft.X, topLeft.Y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);

            var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            Logger.Info($"[ScreenshotService] Captured screenshot: {width}x{height}");
            return stream;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[ScreenshotService] Failed to capture screenshot.");
            return null;
        }
    }

    /// <summary>
    /// Gets the FFXIV game window handle by finding the current process's main window.
    /// Falls back to the foreground window if it belongs to this process.
    /// </summary>
    private static IntPtr GetGameWindowHandle()
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var mainWindow = process.MainWindowHandle;

        if (mainWindow != IntPtr.Zero)
            return mainWindow;

        // Fallback: check if the foreground window belongs to this process
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero)
        {
            GetWindowThreadProcessId(foreground, out uint pid);
            if (pid == (uint)process.Id)
                return foreground;
        }

        return IntPtr.Zero;
    }
}
