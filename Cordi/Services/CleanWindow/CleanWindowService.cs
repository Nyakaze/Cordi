using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Threading;

using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;

using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;

using Vortice.Direct3D11;
using Vortice.DXGI;

using Cordi.Configuration;

namespace Cordi.Services.CleanWindow;

/// <summary>
/// Manages a clean second game window without Dalamud plugin overlays.
/// Hooks DXGIPresent to copy the game's swap chain back buffer to a shared texture.
/// Also creates a native Win32 window with its own DXGI swap chain so Discord
/// can discover and stream it (ImGui viewports are not available in Dalamud).
/// </summary>
public unsafe class CleanWindowService : IDisposable
{
    private readonly CleanWindowConfig _cfg;
    private readonly IPluginLog _log;

    // Texture output for ImGui
    private ID3D11Texture2D? _cleanTexture;
    private ID3D11ShaderResourceView? _cleanSrv;
    private ID3D11DeviceContext? _context;
    private ID3D11Device? _device;
    private ID3D11BlendState? _opaqueBlendState;
    private uint _viewWidth;
    private uint _viewHeight;

    public static CleanWindowService? Instance { get; private set; }

    // State
    private bool _isEnabled;
    private bool _disposed;
    private readonly object _lockObj = new();

    // ── Native Win32 window for Discord streaming ──
    private IntPtr _nativeHwnd;
    private IDXGISwapChain1? _nativeSwapChain;
    private uint _nativeScWidth;
    private uint _nativeScHeight;
    private volatile bool _nativeCloseRequested;

    // Win32 P/Invoke
    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_EX_APPWINDOW_FLAG = 0x00040000;
    private const uint CS_OWNDC = 0x0020;
    private const uint WM_CLOSE = 0x0010;
    private const uint WM_SIZE = 0x0005;
    private const int SW_SHOW = 5;
    private const int SW_HIDE = 0;
    private const int CW_USEDEFAULT = unchecked((int)0x80000000);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate? s_wndProcDelegate;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public IntPtr lpszMenuName;
        public IntPtr lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern ushort RegisterClassExW(ref WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(
        uint dwExStyle, IntPtr lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AdjustWindowRectEx(ref RECT lpRect, uint dwStyle, bool bMenu, uint dwExStyle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UnregisterClassW(IntPtr lpClassName, IntPtr hInstance);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);

    private static ushort _windowClassAtom;
    private static IntPtr _classNamePtr;

    // DXGI Present hook
    private const string DXGIPresentSig = "E8 ?? ?? ?? ?? C6 43 79 00";
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void DXGIPresentDelegate(ulong a, ulong b);
    [Signature(DXGIPresentSig, DetourName = nameof(DXGIPresentDetour))]
    private Hook<DXGIPresentDelegate>? _presentHook = null;

    public CleanWindowService(CleanWindowConfig cfg)
    {
        _cfg = cfg;
        _log = Service.Log;
        Instance = this;

        Service.GameInteropProvider.InitializeFromAttributes(this);
        _presentHook?.Enable();

        _log.Info("[CleanWindow] Service initialized, hook enabled.");
    }

    public void OnFrameworkUpdate()
    {
        if (_disposed) return;

        // User closed the native window via the X button
        if (_nativeCloseRequested)
        {
            _nativeCloseRequested = false;
            _cfg.Enabled = false;
        }

        if (_cfg.Enabled && !_isEnabled)
        {
            Enable();
        }
        else if (!_cfg.Enabled && _isEnabled)
        {
            Disable();
        }
    }

    // ── Native window helpers ──

    private static IntPtr NativeWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_CLOSE)
        {
            if (Instance != null) Instance._nativeCloseRequested = true;
            ShowWindow(hWnd, SW_HIDE);
            return IntPtr.Zero;
        }
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private void CreateNativeWindow(int clientW, int clientH)
    {
        if (_nativeHwnd != IntPtr.Zero) return;

        var hInstance = GetModuleHandleW(null);

        // Keep the delegate alive for the lifetime of the class registration.
        s_wndProcDelegate = NativeWndProc;

        if (_classNamePtr == IntPtr.Zero)
            _classNamePtr = Marshal.StringToHGlobalUni("CordiCleanWindow");

        if (_windowClassAtom == 0)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                style = CS_OWNDC,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(s_wndProcDelegate),
                hInstance = hInstance,
                lpszClassName = _classNamePtr,
            };
            _windowClassAtom = RegisterClassExW(ref wc);
            if (_windowClassAtom == 0)
            {
                var err = Marshal.GetLastWin32Error();
                if (err == 1410) // ERROR_CLASS_ALREADY_EXISTS — class survived a plugin reload
                {
                    _log.Info("[CleanWindow] Window class already registered (plugin reload), reusing by name.");
                }
                else
                {
                    _log.Error($"[CleanWindow] RegisterClassExW failed: {err}");
                    return;
                }
            }
        }

        // Calculate window size including frame/title bar
        const uint style = WS_OVERLAPPEDWINDOW | WS_VISIBLE;
        const uint exStyle = WS_EX_APPWINDOW_FLAG;
        var rect = new RECT { Left = 0, Top = 0, Right = clientW, Bottom = clientH };
        AdjustWindowRectEx(ref rect, style, false, exStyle);

        // Use atom if available, otherwise fall back to class name pointer
        var lpClassName = _windowClassAtom != 0 ? (IntPtr)_windowClassAtom : _classNamePtr;

        _nativeHwnd = CreateWindowExW(
            exStyle,
            lpClassName,
            "Cordi \u2014 Clean View",
            style,
            CW_USEDEFAULT, CW_USEDEFAULT,
            rect.Right - rect.Left, rect.Bottom - rect.Top,
            IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

        if (_nativeHwnd == IntPtr.Zero)
        {
            _log.Error($"[CleanWindow] CreateWindowExW failed: {Marshal.GetLastWin32Error()}");
            return;
        }

        _log.Info($"[CleanWindow] Native window created ({clientW}x{clientH}).");
    }

    private void DestroyNativeWindow()
    {
        if (_nativeHwnd != IntPtr.Zero)
        {
            DestroyWindow(_nativeHwnd);
            _nativeHwnd = IntPtr.Zero;
            _log.Info("[CleanWindow] Native window destroyed.");
        }
    }

    private void CreateNativeSwapChain(uint width, uint height, Format format)
    {
        if (_device == null || _nativeHwnd == IntPtr.Zero) return;

        try
        {
            using var dxgiDevice = _device.QueryInterface<IDXGIDevice>();
            using var adapter = dxgiDevice.GetAdapter();
            using var factory = adapter.GetParent<IDXGIFactory2>();

            var desc = new SwapChainDescription1
            {
                Width = width,
                Height = height,
                Format = format,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = 2,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore,
            };

            _nativeSwapChain = factory.CreateSwapChainForHwnd(_device, _nativeHwnd, desc);
            _nativeScWidth = width;
            _nativeScHeight = height;
            _log.Info($"[CleanWindow] Native swap chain created ({width}x{height}, {format}).");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[CleanWindow] Failed to create native swap chain.");
        }
    }

    private void DestroyNativeSwapChain()
    {
        _nativeSwapChain?.Dispose();
        _nativeSwapChain = null;
        _nativeScWidth = 0;
        _nativeScHeight = 0;
    }

    private void Enable()
    {
        lock (_lockObj)
        {
            if (_isEnabled) return;
            _isEnabled = true;

            try
            {
                var ffxivDevice = Device.Instance();
                if (ffxivDevice == null)
                {
                    _log.Error("[CleanWindow] Failed to get FFXIV device instance.");
                    _isEnabled = false;
                    return;
                }

                var devicePtr = (nint)ffxivDevice->D3D11Forwarder;
                var contextPtr = (nint)ffxivDevice->D3D11DeviceContext;

                if (devicePtr == 0 || contextPtr == 0)
                {
                    _log.Error("[CleanWindow] D3D11 device or context pointer is null.");
                    _isEnabled = false;
                    return;
                }

                _device = new ID3D11Device(devicePtr);
                _device.AddRef(); // Increment refcount to prevent finalizing/disposing from dropping FFXIV's real refcount

                _context = new ID3D11DeviceContext(contextPtr);
                _context.AddRef(); // Same for context

                var blendDesc = new BlendDescription
                {
                    AlphaToCoverageEnable = false,
                    IndependentBlendEnable = false
                };
                blendDesc.RenderTarget[0] = new RenderTargetBlendDescription
                {
                    BlendEnable = true,
                    SourceBlend = Blend.One,
                    DestinationBlend = Blend.Zero,
                    BlendOperation = BlendOperation.Add, // RGB = Texture
                    SourceBlendAlpha = Blend.Zero,
                    DestinationBlendAlpha = Blend.One,
                    BlendOperationAlpha = BlendOperation.Add, // Alpha = Dest (Preserve ImGui Window Alpha!)
                    RenderTargetWriteMask = ColorWriteEnable.All
                };
                _opaqueBlendState = _device.CreateBlendState(blendDesc);

                // Create a native Win32 window that Discord/OBS can discover for streaming.
                int initialW = _cfg.OutputHeight > 0 ? (int)(_cfg.OutputHeight * 16.0 / 9.0) : 1280;
                int initialH = _cfg.OutputHeight > 0 ? _cfg.OutputHeight : 720;
                CreateNativeWindow(initialW, initialH);

                _isEnabled = true;
                _log.Info("[CleanWindow] Enabled.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[CleanWindow] Failed to enable.");
                _isEnabled = false;
            }
        }
    }

    private void Disable()
    {
        lock (_lockObj)
        {
            if (!_isEnabled) return;
            _isEnabled = false;

            try
            {
                DestroyNativeSwapChain();
                DestroyNativeWindow();
                DestroyTexture();

                _context?.Dispose();
                _context = null;

                _device?.Dispose();
                _device = null;

                _opaqueBlendState?.Dispose();
                _opaqueBlendState = null;

                _viewWidth = 0;
                _viewHeight = 0;

                _log.Info("[CleanWindow] Disabled.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[CleanWindow] Error during disable.");
            }
        }
    }

    public nint GetShaderResourceView()
    {
        lock (_lockObj)
        {
            return _cleanSrv != null ? _cleanSrv.NativePointer : nint.Zero;
        }
    }

    public System.Numerics.Vector2 GetViewSize()
    {
        lock (_lockObj)
        {
            if (_viewWidth == 0 || _viewHeight == 0) return System.Numerics.Vector2.Zero;

            float outWidth = _viewWidth;
            float outHeight = _viewHeight;

            if (_cfg != null && _cfg.OutputHeight > 0 && _cfg.OutputHeight != _viewHeight)
            {
                outHeight = _cfg.OutputHeight;
                outWidth = (_viewWidth * outHeight) / _viewHeight;
            }

            return new System.Numerics.Vector2(outWidth, outHeight);
        }
    }

    private void CreateTexture(int width, int height, Format format)
    {
        if (_device == null) return;

        try
        {
            var desc = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                Format = format,
                MipLevels = 1,
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                Usage = ResourceUsage.Default,
                SampleDescription = new SampleDescription(1, 0)
            };

            _cleanTexture = _device.CreateTexture2D(desc);

            var srvDesc = new ShaderResourceViewDescription
            {
                Format = format,
                ViewDimension = Vortice.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new Texture2DShaderResourceView { MipLevels = 1, MostDetailedMip = 0 }
            };

            _cleanSrv = _device.CreateShaderResourceView(_cleanTexture, srvDesc);

            _viewWidth = (uint)width;
            _viewHeight = (uint)height;

            _log.Info($"[CleanWindow] Created ShaderResourceView {width}x{height}");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[CleanWindow] Failed to create Clean Texture.");
        }
    }

    private void DestroyTexture()
    {
        _cleanSrv?.Dispose();
        _cleanSrv = null;

        _cleanTexture?.Dispose();
        _cleanTexture = null;
    }

    /// <summary>
    /// Resolves the source texture for the clean window based on ShowGameUI config.
    /// When ShowGameUI is true, uses the backbuffer (game + native UI, no Dalamud).
    /// When ShowGameUI is false, tries RenderIndexWithoutUI (game world only, no UI),
    /// falling back to the backbuffer.
    /// </summary>
    private nint ResolveSourceTexture(Texture* backBuffer)
    {
        if (!_cfg.ShowGameUI)
        {
            var rtm = RenderTargetManager.Instance();
            if (rtm != null)
            {
                int rtIndex = _cfg.RenderIndexWithoutUI;
                int maxIndex = RenderTargetManager.StructSize / sizeof(nint);

                if (rtIndex > 0 && rtIndex < maxIndex)
                {
                    var ptrBase = (Texture**)rtm;
                    var rtTexture = ptrBase[rtIndex];

                    if (rtTexture != null && rtTexture->D3D11Texture2D != null)
                    {
                        return (nint)rtTexture->D3D11Texture2D;
                    }
                }
            }
        }

        // ShowGameUI=true or fallback: backbuffer (game + native UI, no Dalamud overlays)
        return (nint)backBuffer->D3D11Texture2D;
    }

    private void DXGIPresentDetour(ulong a, ulong b)
    {
        try
        {
            lock (_lockObj)
            {
                if (_isEnabled && _context != null && _device != null)
                {
                    var ffxivDevice = Device.Instance();
                    if (ffxivDevice != null && ffxivDevice->SwapChain != null && ffxivDevice->SwapChain->BackBuffer != null)
                    {
                        var backBuffer = ffxivDevice->SwapChain->BackBuffer;
                        var srcTexturePtr = ResolveSourceTexture(backBuffer);

                        if (srcTexturePtr != IntPtr.Zero)
                        {
                            using var srcTexture = new ID3D11Texture2D(srcTexturePtr);
                            srcTexture.AddRef(); // Don't let Dispose release the game's texture

                            var srcDesc = srcTexture.Description;

                            if (_cleanTexture == null || srcDesc.Width != _viewWidth || srcDesc.Height != _viewHeight)
                            {
                                DestroyTexture();
                                CreateTexture((int)srcDesc.Width, (int)srcDesc.Height, srcDesc.Format);

                                // Recreate swap chain to match new resolution
                                DestroyNativeSwapChain();
                            }

                            if (_cleanTexture != null)
                            {
                                // Asynchronous GPU command. No CPU blocking occurs here.
                                _context.CopyResource(_cleanTexture, srcTexture);
                            }

                            // Present to the native window so Discord/OBS can capture it
                            if (_nativeHwnd != IntPtr.Zero)
                            {
                                if (_nativeSwapChain == null || _nativeScWidth != srcDesc.Width || _nativeScHeight != srcDesc.Height)
                                {
                                    DestroyNativeSwapChain();
                                    CreateNativeSwapChain(srcDesc.Width, srcDesc.Height, srcDesc.Format);
                                }

                                if (_nativeSwapChain != null)
                                {
                                    using var scBackBuffer = _nativeSwapChain.GetBuffer<ID3D11Texture2D>(0);
                                    _context.CopyResource(scBackBuffer, srcTexture);
                                    _nativeSwapChain.Present(0, PresentFlags.None);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Verbose(ex, "[CleanWindow] Error in present hook.");
        }

        _presentHook!.Original(a, b);
    }

    private ID3D11BlendState? _previousBlendState;
    private float[] _previousBlendFactor = new float[4];
    private uint _previousSampleMask;

    public void SetOpaqueBlendState()
    {
        if (_isEnabled && _context != null && _opaqueBlendState != null)
        {
            unsafe
            {
                fixed (float* pBlendFactor = _previousBlendFactor)
                {
                    _context.OMGetBlendState(out _previousBlendState, pBlendFactor, out _previousSampleMask);
                }
            }

            var mathCol = new Vortice.Mathematics.Color4(1, 1, 1, 1);
            _context.OMSetBlendState(_opaqueBlendState, mathCol, 0xFFFFFFFF);
        }
    }

    public void RestoreBlendState()
    {
        if (_isEnabled && _context != null)
        {
            unsafe
            {
                fixed (float* pBlendFactor = _previousBlendFactor)
                {
                    _context.OMSetBlendState(_previousBlendState, pBlendFactor, _previousSampleMask);
                }
            }

            // OMGetBlendState increments the COM ref count, so we must dispose our wrapper to release our hold on it
            _previousBlendState?.Dispose();
            _previousBlendState = null;
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void OpaqueBlendCallback(IntPtr parent_list, IntPtr cmd)
    {
        try
        {
            Instance?.SetOpaqueBlendState();
        }
        catch (Exception ex)
        {
            Instance?._log.Error(ex, "Exception in OpaqueBlendCallback");
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static void RestoreBlendCallback(IntPtr parent_list, IntPtr cmd)
    {
        try
        {
            Instance?.RestoreBlendState();
        }
        catch (Exception ex)
        {
            Instance?._log.Error(ex, "Exception in RestoreBlendCallback");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Instance = null;

        Disable();

        _presentHook?.Disable();
        _presentHook?.Dispose();
        _presentHook = null;
    }
}
