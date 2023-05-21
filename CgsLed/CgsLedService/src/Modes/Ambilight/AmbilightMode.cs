using CgsLedController;

using ScreenCapture.NET;

namespace CgsLedService.Modes.Ambilight;

using ScreenCapture = Helpers.ScreenCapture;

public class AmbilightMode : LedMode<AmbilightMode.Configuration> {
    public record Configuration(int screen = 0, string? window = null);

    private readonly ScreenCapture _screenCapture;
    private readonly List<CaptureZone> _captures = new();

    public AmbilightMode(ScreenCapture screenCapture, Configuration config) : base(config) =>
        _screenCapture = screenCapture;

    protected override void Main() {
        ScreenCapture.CaptureInfo info = _screenCapture.GetCaptureInfo(config.screen, config.window);

        int botHeight = info.height / 5;
        ScreenCapture.CaptureInfo bottomInfo = info with { y = info.y + info.height - botHeight, height = botHeight };

        _captures.Add(_screenCapture.RegisterCaptureZone(info, writer.ledCounts[0]));
        _captures.Add(_screenCapture.RegisterCaptureZone(info, writer.ledCounts[1]));
        _captures.Add(_screenCapture.RegisterCaptureZone(bottomInfo, writer.ledCounts[2]));
    }

    public override void StopMode() {
        foreach(CaptureZone zone in _captures)
            _screenCapture.UnregisterCaptureZone(zone);
    }

    public override void Update() => _screenCapture.Update();
    public override void Draw(int strip) {
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
