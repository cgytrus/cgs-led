using CgsLedController;

namespace CgsLedService;

public readonly record struct MusicColors(
    float hueSpeed = 5f,
    float hueOffset = 0f,
    float rightHueOffset = 30f,
    float hueRange = 120f,
    float saturation = 0.7f,
    bool gammaCorrection = true) {
    public MusicColors() : this(gammaCorrection: true) { }
    public void Write(LedWriter writer, int strip, float time, Span<float> h, Span<float> v) {
        int ledCount = writer.ledCounts[strip];
        for(int i = 0; i < ledCount; i++) {
            float x = (float)i / ledCount;
            writer.WriteHsv(time * hueSpeed + hueOffset + x * rightHueOffset + h[i] * hueRange, saturation, v[i],
                gammaCorrection);
        }
    }
}
