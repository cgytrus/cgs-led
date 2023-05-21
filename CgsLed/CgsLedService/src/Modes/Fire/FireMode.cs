using CgsLedController;

namespace CgsLedService.Modes.Fire;

public class FireMode : LedMode {
    public override void Update() { }
    public override void Draw(int strip) {
        float time = (float)this.time.TotalSeconds * 0.8f;
        for(int i = 0; i < writer.ledCounts[strip]; i++) {
            float valueNoise = Perlin.Get(i * 0.25f, 0f, time);
            float hueNoise = Perlin.Get(i * 0.1f, time * 0.5f, 0f);
            writer.WriteHsv(hueNoise * 60f, 1f, valueNoise, true);
        }
    }
}
