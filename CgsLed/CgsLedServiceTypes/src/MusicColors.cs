using CgsLedController;

namespace CgsLedServiceTypes;

public readonly record struct MusicColors(
    float hueSpeed = 5f,
    float hueOffset = 0f,
    float rightHueOffset = 30f,
    float hueRange = 120f,
    float saturation = 0.7f,
    bool gammaCorrection = true) {
    public MusicColors() : this(gammaCorrection: true) { }
    public void Write(LedBuffer buffer, int strip, float time, Span<float> h, Span<float> v) {
        int ledCount = buffer.ledCounts[strip];
        for(int i = 0; i < ledCount; i++) {
            float x = (float)i / ledCount;
            buffer.WriteHsv(time * hueSpeed + hueOffset + x * rightHueOffset + h[i] * hueRange, saturation, v[i],
                gammaCorrection);
        }
    }
}
