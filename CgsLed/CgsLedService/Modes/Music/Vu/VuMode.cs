namespace CgsLedService.Modes.Music.Vu;

public class VuMode : MusicMode<VuMode.Configuration> {
    public new record Configuration(
        TimeSpan period,
        float volume,
        string? process,
        bool excludeProcess,
        MusicColors colors,
        int sampleCount = 16,
        float falloffSpeed = 1f) :
        MusicMode<Configuration>.Configuration(period, volume, process, excludeProcess, colors);

    protected override bool forceMono => false;

    private readonly float[] _samples = new float[2];
    private readonly int[] _sampleCounts = new int[2];
    private readonly float[] _display = new float[2];

    public VuMode(Configuration config) : base(config) { }

    protected override void AddSample(float sample, int channel, TimeSpan time) {
        if(_samples[channel] >= config.sampleCount) {
            float display = MoreMath.SampleToUnitDb(MathF.Sqrt(_samples[channel] / _sampleCounts[channel]));
            _display[channel] = display;

            _samples[channel] = 0f;
            _sampleCounts[channel] = 0;
        }
        _samples[channel] += sample * sample;
        _sampleCounts[channel]++;
    }

    protected override void Frame(float deltaTime) {
        if(hues is null || values is null)
            return;

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int ledCount = writer.ledCounts[strip];
            int ledStart = writer.ledStarts[strip];
            int rightCount = writer.halfLedCounts[strip];
            int leftCount = ledCount - rightCount;
            for(int i = 0; i < leftCount; i++) {
                hues[ledStart + i] = (float)i / leftCount;
                values[ledStart + i] = Math.Clamp(_display[0] * leftCount - i, 0f, 1f);
            }
            for(int i = 0; i < rightCount; i++) {
                hues[ledStart + ledCount - i - 1] = (float)i / rightCount;
                values[ledStart + ledCount - i - 1] = Math.Clamp(_display[1] * rightCount - i, 0f, 1f);
            }
        }

        for(int i = 0; i < _display.Length; i++)
            _display[i] = Math.Max(_display[i] - config.falloffSpeed * deltaTime, 0f);

        base.Frame(deltaTime);
    }
}
