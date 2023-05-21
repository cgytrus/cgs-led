using CgsLedController;

namespace CgsLedService.Modes.StandBy;

public class StandByMode : LedMode {
    public override void Update() { }
    public override void Draw(int strip) {
        for(int i = 0; i < writer.ledCounts[strip]; i++) {
            byte offset =
                (byte)MathF.Min(MathF.Max(MathF.Sin(((float)time.TotalMilliseconds + i * 100f) / 1000f) * 40f, 0f),
                    255f);
            writer.WriteRgb(offset, (byte)int.Clamp(255 + offset, 0, 255), offset, false);
        }
    }
}
