namespace CgsLedService.Modes.Music.Waveform;

public class WaveformMode : MusicMode<WaveformMode.Configuration> {
    public new record Configuration(
        TimeSpan period,
        float volume,
        string? process,
        bool excludeProcess,
        MusicColors colors,
        int bufferSize = 48000,
        int displayCount = 80) :
        MusicMode<Configuration>.Configuration(period, volume, process, excludeProcess, colors);

    protected override bool forceMono => true;

    private record struct Sample(float sum, int count, TimeSpan time) { public float value => sum / count; }
    private List<Sample>? _samples;
    private int _displayTail;

    private readonly object _samplesLock = new();

    public WaveformMode(Configuration config) : base(config) { }

    protected override void Main() {
        lock(_samplesLock) {
            _samples = new List<Sample>(config.bufferSize);
            for(int i = 0; i < config.bufferSize; i++)
                _samples.Add(new Sample(0f, 0, TimeSpan.Zero));
            _displayTail = _samples.Count;
        }
        base.Main();
    }

    protected override void AddSample(float sample, int channel, TimeSpan time) {
        lock(_samplesLock) {
            if(_samples is null)
                return;
            while(_samples.Count > config.bufferSize) {
                _samples.RemoveAt(0);
                _displayTail--;
            }
            sample = Math.Abs(sample);
            if(_samples[^1].count >= config.displayCount)
                _samples.Add(new Sample(sample, 1, time));
            else {
                Sample s = _samples[^1];
                s.sum += sample;
                s.count++;
                _samples[^1] = s;
            }
        }
    }

    protected override void Frame(float deltaTime) {
        lock(_samplesLock) {
            if(_samples is null || hues is null || values is null)
                return;
            while(_displayTail < _samples.Count && time >= _samples[Math.Max(_displayTail, 0)].time)
                _displayTail++;
        }
        int displayTail = Math.Min(_displayTail, _samples.Count);

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int ledCount = writer.ledCounts[strip];
            int ledStart = writer.ledStarts[strip];
            lock(_samplesLock) {
                for(int i = 0; i < ledCount; i++) {
                    float progress = (float)i / ledCount * config.displayCount;
                    int index = Math.Max(displayTail - config.displayCount, 0) + (int)progress;
                    int nextIndex = (index + 1) % _samples.Count;
                    float bin = MoreMath.Lerp(_samples[index].value, _samples[nextIndex].value, progress - index);
                    bin = MathF.Max(MathF.Min(bin, 1f), 0f);
                    hues[ledStart + i] = bin;
                    values[ledStart + i] = bin;
                }
            }
        }

        base.Frame(deltaTime);
    }
}
