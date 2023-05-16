using System.Diagnostics;

using CgsLedController;

namespace CgsLedService.Modes.Fire;

public class FireMode : LedMode {
    public override bool running => _running;
    private bool _running;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public override void StopMode() {
        _running = false;
    }

    protected override void Main() {
        _running = true;
        _stopwatch.Restart();
    }

    protected override void Frame() {
        float time = (float)_stopwatch.Elapsed.TotalSeconds * 0.8f;
        for(int i = 0; i < writer.totalLedCount; i++) {
            float valueNoise = Perlin.Get(i * 0.25f, 0f, time);
            float hueNoise = Perlin.Get(i * 0.1f, time * 0.5f, 0f);
            writer.WriteHsv(hueNoise * 60f, 1f, valueNoise, true);
        }
    }
}
