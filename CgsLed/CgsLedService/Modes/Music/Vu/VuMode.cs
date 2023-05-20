namespace CgsLedService.Modes.Music.Vu;

public class VuMode : MusicMode<VuMode.Configuration> {
    public new record Configuration(
        MusicConfig music,
        int sampleCount = 16,
        float falloffSpeed = 1f) :
        MusicMode<VuMode.Configuration>.Configuration(music);

    protected override bool forceMono => false;

    private readonly float[] _samples = new float[2];
    private readonly int[] _sampleCounts = new int[2];
    private readonly float[] _display = new float[2];
    private readonly TimeSpan[] _lastDisplayTimes = new TimeSpan[2];

    public VuMode(Configuration config) : base(config) { }

    protected override void AddSample(float sample, int channel, TimeSpan time) {
        if(_samples[channel] >= config.sampleCount) {
            float display = MoreMath.SampleToUnitDb(MathF.Sqrt(_samples[channel] / _sampleCounts[channel]));
            if(GetDisplay(channel) <= display) {
                _display[channel] = display;
                _lastDisplayTimes[channel] = this.time;
            }

            _samples[channel] = 0f;
            _sampleCounts[channel] = 0;
        }
        _samples[channel] += sample * sample;
        _sampleCounts[channel]++;
    }

    private float GetDisplay(int channel) =>
        _display[channel] - config.falloffSpeed * (float)(time - _lastDisplayTimes[channel]).TotalSeconds;

    public override void Update() { }
    public override void Draw(int strip) {
        if(hues is null || values is null)
            return;

        int ledCount = writer.ledCounts[strip];
        int rightCount = writer.halfLedCounts[strip];
        int leftCount = ledCount - rightCount;
        for(int i = 0; i < leftCount; i++) {
            hues[i] = (float)i / leftCount;
            values[i] = Math.Clamp(GetDisplay(0) * leftCount - i, 0f, 1f);
        }
        for(int i = 0; i < rightCount; i++) {
            hues[ledCount - i - 1] = (float)i / rightCount;
            values[ledCount - i - 1] = Math.Clamp(GetDisplay(1) * rightCount - i, 0f, 1f);
        }

        base.Draw(strip);
    }
}
