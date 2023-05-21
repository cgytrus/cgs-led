﻿using System.Runtime.InteropServices;

using JetBrains.Annotations;

using ScreenCapture.NET;

namespace CgsLedService.Helpers;

public sealed partial class ScreenCapture : IDisposable {
    private readonly IScreenCaptureService _screenCaptureService = new DX11ScreenCaptureService();
    private readonly IScreenCapture[] _screenCaptures;

    private readonly Dictionary<CaptureZone, IScreenCapture> _captureZones = new();
    private readonly HashSet<IScreenCapture> _toUpdate = new();

    public readonly record struct CaptureInfo(int screen, int x, int y, int width, int height);

    public ScreenCapture() {
        IEnumerable<GraphicsCard> graphicsCards = _screenCaptureService.GetGraphicsCards();
        List<Display> displays = _screenCaptureService.GetDisplays(graphicsCards.First()).ToList();
        _screenCaptures = new IScreenCapture[displays.Count];
        for(int i = 0; i < displays.Count; i++) {
            Display display = displays[i];
            IScreenCapture screenCapture = _screenCaptureService.GetScreenCapture(display);
            _screenCaptures[i] = screenCapture;

            // set capture timeout to 0 so that it doesn't lag other modes
            if(screenCapture is DX11ScreenCapture dx11ScreenCapture)
                dx11ScreenCapture.Timeout = 0;
        }
    }

#region WINAPI stuff

    [PublicAPI, StructLayout(LayoutKind.Sequential)]
    private struct Rect {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [PublicAPI, StructLayout(LayoutKind.Sequential)]
    private struct WindowInfo {
        public uint cbSize;
        public Rect rcWindow;
        public Rect rcClient;
        public uint dwStyle;
        public uint dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;
        public WindowInfo(bool? filler) : this() => cbSize = (uint)Marshal.SizeOf(typeof(WindowInfo));
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial nint FindWindowW(string? lpClassName, string? lpWindowName);

    [LibraryImport("user32.dll")]
    private static unsafe partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static unsafe partial void GetWindowInfo(nint hwnd, ref WindowInfo pwi);

    private delegate bool EnumMonitorsDelegate(nint hMonitor, nint hdcMonitor, ref Rect lprcMonitor, nint dwData);
    [LibraryImport("user32.dll")]
    private static unsafe partial void EnumDisplayMonitors(nint hdc, nint lprcClip, EnumMonitorsDelegate lpfnEnum,
        nint dwData);

    [LibraryImport("user32.dll")]
    private static unsafe partial nint MonitorFromWindow(nint hwnd, uint dwFlags);

#endregion

    public CaptureInfo GetCaptureInfo(int screen, string? window) {
        IScreenCapture screenCapture;

        nint windowHwnd = window is null ? nint.Zero : FindWindowW(null, window);
        nint foregroundWindowHwnd = screen < 0 ? GetForegroundWindow() : nint.Zero;

        if(windowHwnd == nint.Zero && foregroundWindowHwnd == nint.Zero) {
            screenCapture = _screenCaptures[screen];
            return new CaptureInfo(screen, 0, 0, screenCapture.Display.Width, screenCapture.Display.Height);
        }

        if(window is null)
            windowHwnd = foregroundWindowHwnd;
        WindowInfo info = new(null);
        GetWindowInfo(windowHwnd, ref info);
        Rect rect = info.rcClient;
        nint windowMonitor = MonitorFromWindow(windowHwnd, 2);
        int monitorIndex = -1;
        Rect monitorRect = new();
        EnumDisplayMonitors(nint.Zero, nint.Zero,
            (nint hMonitor, nint _, ref Rect lprcMonitor, nint _) => {
                monitorIndex++;
                monitorRect = lprcMonitor;
                return hMonitor != windowMonitor;
            }, nint.Zero);

        screenCapture = _screenCaptures[monitorIndex];
        int captureX = Math.Max(rect.left - monitorRect.left, 0);
        int captureY = Math.Max(rect.top - monitorRect.top, 0);
        return new CaptureInfo(monitorIndex, captureX, captureY,
            Math.Min(rect.right - rect.left, screenCapture.Display.Width - captureX),
            Math.Min(rect.bottom - rect.top, screenCapture.Display.Height - captureY));
    }

    public CaptureZone RegisterCaptureZone(CaptureInfo info, int width) {
        CaptureZone zone = _screenCaptures[info.screen]
            .RegisterCaptureZone(info.x, info.y, info.width, info.height, GetApproxDownscaleLevel(info.width, width));
        _captureZones[zone] = _screenCaptures[info.screen];
        UpdateToUpdate();
        return zone;
    }
    public void UnregisterCaptureZone(CaptureZone zone) {
        if(!_captureZones.TryGetValue(zone, out IScreenCapture? capture))
            return;
        capture.UnregisterCaptureZone(zone);
        _captureZones.Remove(zone);
        UpdateToUpdate();
    }

    private void UpdateToUpdate() {
        _toUpdate.Clear();
        foreach((CaptureZone? _, IScreenCapture? capture) in _captureZones)
            _toUpdate.Add(capture);
    }

    private static int GetApproxDownscaleLevel(int from, int to) => (int)MathF.Round(MathF.Sqrt((float)from / to));

    public void Update() {
        foreach(IScreenCapture capture in _toUpdate)
            capture.CaptureScreen();
    }

    public void Dispose() => _screenCaptureService.Dispose();
}