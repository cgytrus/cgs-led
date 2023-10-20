using CgsLedController;

namespace CgsLedService.Modes.Fire;

public class FireMode : LedMode {
    public override void Draw(LedBuffer buffer, int strip) {
        float t = (float)time.TotalSeconds * 0.8f;
        for(int i = 0; i < buffer.ledCounts[strip]; i++) {
            float valueNoise = Perlin.Get(i * 0.25f, 0f, t);
            float hueNoise = Perlin.Get(i * 0.1f, t * 0.5f, 0f);
            buffer.WriteHsv(hueNoise * 60f, 1f, valueNoise, true);
        }
    }
}
