namespace CgsLedService.Modes.Music.Vu;

public class VuMode : MusicMode<VuMode.Configuration> {
    public new record Configuration(
        TimeSpan period,
        float volume,
        MusicColors colors,
        float falloff = 0.04f) :
        MusicMode<Configuration>.Configuration(period, volume, colors);

    protected override bool forceMono => false;

    private float[] _samples = new float[2];

    public VuMode(Configuration config) : base(config) { }

    protected override void AddSample(float sample, int channel) {
        sample = Math.Abs(sample);
        if(sample < _samples[channel])
            _samples[channel] = Math.Max(_samples[channel] - config.falloff, 0f);
        else
            _samples[channel] = sample;
    }

    protected override void Frame() {
        if(hues is null || values is null)
            return;

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int ledCount = writer.ledCounts[strip];
            int ledStart = writer.ledStarts[strip];
            int rightCount = writer.halfLedCounts[strip];
            int leftCount = ledCount - rightCount;
            for(int i = 0; i < leftCount; i++) {
                float progress = (float)i / leftCount;
                float sample = MathF.Log10(_samples[0] + 1f) / MathF.Log10(2f) * leftCount;
                hues[ledStart + i] = progress;
                values[ledStart + i] = Math.Clamp(sample - i, 0f, 1f);
            }
            for(int i = 0; i < rightCount; i++) {
                float progress = (float)i / rightCount;
                float sample = MathF.Log10(_samples[1] + 1f) / MathF.Log10(2f) * rightCount;
                hues[ledStart + ledCount - i - 1] = progress;
                values[ledStart + ledCount - i - 1] = Math.Clamp(sample - i, 0f, 1f);
            }
        }

        base.Frame();
    }
}
