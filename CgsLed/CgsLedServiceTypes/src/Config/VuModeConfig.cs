namespace CgsLedServiceTypes.Config;

public record VuModeConfig(
    MusicColors colors,
    int sampleCount = 16,
    float falloffSpeed = 1f);
