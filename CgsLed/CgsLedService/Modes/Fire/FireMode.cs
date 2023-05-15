using System.Diagnostics;

using CgsLedController;

namespace CgsLedService.Modes.Fire;

public class FireMode : CustomMode {
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
        float time = _stopwatch.ElapsedMilliseconds / 10f;
        for(int i = 0; i < writer.totalLedCount; i++) {
            float valueNoise = Perlin.Get(i / 30f, 0f, time);
            float hueNoise = Perlin.Get(i, 0f, 0f);
            writer.WriteHSV(hueNoise * 360f, 1f, valueNoise, false);
        }
    }
}
