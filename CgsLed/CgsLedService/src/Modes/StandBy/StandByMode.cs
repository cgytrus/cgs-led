using CgsLedController;

namespace CgsLedService.Modes.StandBy;

public class StandByMode : LedMode {
    public override void Draw(LedBuffer buffer, int strip) {
        for(int i = 0; i < buffer.ledCounts[strip]; i++) {
            byte offset =
                (byte)MathF.Min(MathF.Max(MathF.Sin(((float)time.TotalMilliseconds + i * 100f) / 1000f) * 40f, 0f),
                    255f);
            buffer.WriteRgb(offset, (byte)int.Clamp(255 + offset, 0, 255), offset, false);
        }
    }
}
