namespace CgsLedService.Modes.Music.Waveform;

public class WaveformMode : MusicMode<WaveformMode.Configuration> {
    public new record Configuration(
        TimeSpan period,
        float volume,
        MusicColors colors,
        int bufferSize = 128,
        int sampleCount = 16384) :
        MusicMode<Configuration>.Configuration(period, volume, colors);

    protected override bool forceMono => true;

    private List<float>? _samples;
    private List<int>? _sampleCounts;

    private readonly object _samplesLock = new();

    public WaveformMode(Configuration config) : base(config) { }

    protected override void Main() {
        lock(_samplesLock) {
            _samples = new List<float>(config.bufferSize);
            _sampleCounts = new List<int>(config.bufferSize);
            for(int i = 0; i < config.bufferSize; i++)
                _samples.Add(0f);
        }
        base.Main();
    }

    protected override void AddSample(float sample, int channel) {
        lock(_samplesLock) {
            if(_samples is null || _sampleCounts is null)
                return;
            while(_samples.Count > config.bufferSize) {
                _samples.RemoveAt(0);
                _sampleCounts.RemoveAt(0);
            }
            sample = Math.Abs(sample);
            if(GetSampleCountAt(_samples.Count - 1) >= config.sampleCount / config.bufferSize)
                _samples.Add(sample);
            else
                _samples[^1] += sample;
            IncSampleCountAt(_samples.Count - 1);
        }
    }
    private int GetSampleCountAt(int index) => _sampleCounts is null ? 0 :
        index >= _sampleCounts.Count ? 0 : _sampleCounts[index];
    private void IncSampleCountAt(int index) {
        if(_sampleCounts is null)
            return;
        while(index >= _sampleCounts.Count)
            _sampleCounts.Add(0);
        _sampleCounts[index]++;
    }

    protected override void Frame() {
        lock(_samplesLock) {
            if(_samples is null || hues is null || values is null)
                return;
        }

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int ledCount = writer.ledCounts[strip];
            int ledStart = writer.ledStarts[strip];
            lock(_samplesLock) {
                for(int i = 0; i < ledCount; i++) {
                    float progress = (float)i / ledCount * _samples.Count;
                    int index = (int)progress;
                    int nextIndex = (index + 1) % _samples.Count;
                    float bin = MoreMath.Lerp(_samples[index] / GetSampleCountAt(index),
                        _samples[nextIndex] / GetSampleCountAt(nextIndex), progress - index);
                    bin = MathF.Max(MathF.Min(bin, 1f), 0f);
                    hues[ledStart + i] = bin;
                    values[ledStart + i] = bin;
                }
            }
        }

        base.Frame();
    }
}
