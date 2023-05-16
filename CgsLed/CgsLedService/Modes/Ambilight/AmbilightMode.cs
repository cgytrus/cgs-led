using System.Runtime.InteropServices;

using CgsLedController;

using ScreenCapture.NET;

namespace CgsLedService.Modes.Ambilight;

public partial class AmbilightMode : LedMode {
    public int screen { get; set; }
    public string? window { get; set; }

    public override bool running => _running;

    private bool _running;

    private IScreenCapture? _screenCapture;
    private readonly CaptureZone[] _captures = new CaptureZone[3];

    public override void StopMode() {
        _running = false;
        _screenCapture?.Dispose();
        _screenCapture = null;
    }

    // ReSharper disable MemberCanBePrivate.Local FieldCanBeMadeReadOnly.Local
#pragma warning disable CS0649
    private struct Rect {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
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

        // ReSharper disable once UnusedParameter.Local
        public WindowInfo(bool? filler) : this() => cbSize = (uint)Marshal.SizeOf(typeof(WindowInfo));
    }
#pragma warning restore CS0649
    // ReSharper restore MemberCanBePrivate.Local FieldCanBeMadeReadOnly.Local

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

    // shut
#pragma warning disable CA1416
    protected override void Main() {
        IScreenCaptureService screenCaptureService = new DX11ScreenCaptureService();
        IEnumerable<GraphicsCard> graphicsCards = screenCaptureService.GetGraphicsCards();
        IEnumerable<Display> displays = screenCaptureService.GetDisplays(graphicsCards.First());

        int captureX;
        int captureY;
        int captureWidth;
        int captureHeight;

        nint windowHwnd = window is null ? nint.Zero : FindWindowW(null, window);
        nint foregroundWindowHwnd = screen < 0 ? GetForegroundWindow() : nint.Zero;
        if(windowHwnd != nint.Zero || foregroundWindowHwnd != nint.Zero) {
            if(window is null) windowHwnd = foregroundWindowHwnd;
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
            _screenCapture = screenCaptureService.GetScreenCapture(displays.ElementAt(monitorIndex));
            captureX = Math.Max(rect.left - monitorRect.left, 0);
            captureY = Math.Max(rect.top - monitorRect.top, 0);
            captureWidth = Math.Min(rect.right - rect.left, _screenCapture.Display.Width - captureX);
            captureHeight = Math.Min(rect.bottom - rect.top, _screenCapture.Display.Height - captureY);
        }
        else {
            _screenCapture = screenCaptureService.GetScreenCapture(displays.ElementAt(screen));
            captureX = 0;
            captureY = 0;
            captureWidth = _screenCapture.Display.Width;
            captureHeight = _screenCapture.Display.Height;
        }

        _captures[0] = _screenCapture.RegisterCaptureZone(captureX, captureY, captureWidth, captureHeight,
            GetApproxDownscaleLevel(captureWidth, writer.ledCounts[0]));

        //_captures[1] = _screenCapture.RegisterCaptureZone(0, 0, captureWidth, captureHeight,
        //    GetApproxDownscaleLevel(captureWidth, ledCounts[1]));
        _captures[1] = _captures[0];

        int bottomHeight = captureHeight / 5;
        _captures[2] = _screenCapture.RegisterCaptureZone(captureX, captureY + captureHeight - bottomHeight,
            captureWidth, bottomHeight,
            GetApproxDownscaleLevel(captureWidth, writer.ledCounts[2]));

        _running = true;
    }

    private static int GetApproxDownscaleLevel(int from, int to) => (int)MathF.Round(MathF.Sqrt((float)from / to));

    // ReSharper disable once CognitiveComplexity
    protected override void Frame() {
        if(_screenCapture is null) {
            Console.WriteLine("wtf");
            return;
        }

        _screenCapture.CaptureScreen();

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int pixelCount = writer.ledCounts[strip];

            CaptureZone capture = _captures[strip];
            lock(capture.Buffer) {
                Span<byte> data = new(capture.Buffer);
                float width = (float)capture.Width / pixelCount;

                int stride = capture.Stride;
                for(int i = 0; i < pixelCount; i++) {
                    uint avgR = 0;
                    uint avgG = 0;
                    uint avgB = 0;

                    float startX = width * i;
                    uint avgCount = 0;
                    for(float x = startX; x < startX + width; x++) {
                        for(int y = 0; y < capture.Height; y++) {
                            int index = y * stride + (int)x * capture.BytesPerPixel;
                            avgR += data[index + 2];
                            avgG += data[index + 1];
                            avgB += data[index];
                            avgCount++;
                        }
                    }

                    avgR /= avgCount;
                    avgG /= avgCount;
                    avgB /= avgCount;

                    writer.WriteRgb((byte)avgR, (byte)avgG, (byte)avgB, true);
                }
            }
        }
    }
#pragma warning restore CA1416
}
