using CgsLedController;

namespace CgsLedService.Modes.Music;

public readonly record struct MusicColors(
    bool gammaCorrection = true,
    float hueSpeed = 5f,
    float hueRange = 120f,
    float saturation = 0.7f) {
    public MusicColors() : this(true) { }
    public void WritePixel(LedWriter writer, float time, float x) =>
        writer.WriteHsv((time * hueSpeed + x * hueRange) % 360f, saturation, x, gammaCorrection);
}
