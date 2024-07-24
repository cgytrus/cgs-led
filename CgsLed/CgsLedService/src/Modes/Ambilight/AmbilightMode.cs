using CgsLedController;

using HPPH;

using ScreenCapture.NET;

namespace CgsLedService.Modes.Ambilight;

using ScreenCapture = Helpers.ScreenCapture;

public class AmbilightMode : LedMode {
    private readonly ScreenCapture _screenCapture;

    public AmbilightMode(ScreenCapture screenCapture) => _screenCapture = screenCapture;

    public override void Update() => _screenCapture.Update();
    public override void Draw(LedBuffer buffer, int strip) {
        int pixelCount = buffer.ledCounts[strip];

        IReadOnlyList<ICaptureZone> captures = _screenCapture.captures;
        ICaptureZone capture;
        lock(_screenCapture.capturesLock)
            capture = captures[strip];
        using(capture.Lock()) {
            IImage image = capture.Image;
            float width = (float)capture.Width / pixelCount;

            for(int i = 0; i < pixelCount; i++) {
                uint avgR = 0;
                uint avgG = 0;
                uint avgB = 0;

                float startX = width * i;
                uint avgCount = 0;
                for(float x = startX; x < startX + width; x++) {
                    for(int y = 0; y < capture.Height; y++) {
                        IColor color = image[(int)x, y];
                        avgR += color.R;
                        avgG += color.G;
                        avgB += color.B;
                        avgCount++;
                    }
                }

                avgR /= avgCount;
                avgG /= avgCount;
                avgB /= avgCount;

                buffer.WriteRgb((byte)avgR, (byte)avgG, (byte)avgB, true);
            }
        }
    }
}
