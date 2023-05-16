using System.Diagnostics;

using CgsLedController;

namespace CgsLedService.Modes.StandBy;

public class StandByMode : LedMode {
    public override bool running => _running;
    private bool _running;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public StandByMode(Configuration config) : base(config) { }

    public override void StopMode() {
        _running = false;
    }

    protected override void Main() {
        _running = true;
        _stopwatch.Restart();
    }

    protected override void Frame() {
        for(int i = 0; i < writer.totalLedCount; i++) {
            byte offset =
                (byte)MathF.Min(MathF.Max(MathF.Sin((_stopwatch.ElapsedMilliseconds + i * 100) / 1000f) * 40f, 0f),
                    255f);
            writer.WriteRgb(offset, (byte)int.Clamp(255 + offset, 0, 255), offset, false);
        }
    }
}
