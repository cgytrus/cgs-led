namespace CgsLedServiceTypes.Config;

public record FftModeConfig(
    MusicColors colors,
    int showStart = 0,
    int showCount = 56,
    float noiseCut = 0.25f,
    bool mirror = true);
