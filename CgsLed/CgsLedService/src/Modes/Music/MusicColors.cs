using CgsLedController;

namespace CgsLedService.Modes.Music;

public readonly record struct MusicColors(
    float hueSpeed = 5f,
    float hueOffset = 0f,
    float rightHueOffset = 30f,
    float hueRange = 120f,
    float saturation = 0.7f,
    bool gammaCorrection = true) {
    public MusicColors() : this(gammaCorrection: true) { }
    public void WritePixel(LedWriter writer, float time, float x, float h, float v) =>
        writer.WriteHsv(time * hueSpeed + hueOffset + x * rightHueOffset + h * hueRange, saturation, v, gammaCorrection);
}
