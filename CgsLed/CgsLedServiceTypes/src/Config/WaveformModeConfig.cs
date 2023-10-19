namespace CgsLedServiceTypes.Config;

public record WaveformModeConfig(
    MusicColors colors,
    float bufferSeconds = 1f,
    float displaySeconds = 0.15f,
    int avgCount = 87);
