using CgsLedController;

using ScreenCapture.NET;

namespace CgsLedService.Modes.Ambilight;

using ScreenCapture = Helpers.ScreenCapture;

public class AmbilightMode : LedMode {
    private readonly ScreenCapture _screenCapture;

    public AmbilightMode(ScreenCapture screenCapture) => _screenCapture = screenCapture;

    public override void Update() => _screenCapture.Update();
    public override void Draw(int strip) {
        int pixelCount = writer.ledCounts[strip];

        IReadOnlyList<CaptureZone> captures = _screenCapture.captures;
        CaptureZone capture;
        lock(_screenCapture.capturesLock)
            capture = captures[strip];
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
