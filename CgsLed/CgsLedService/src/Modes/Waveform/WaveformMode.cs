using CgsLedController;

using CgsLedService.Helpers;

using CgsLedServiceTypes.Config;

namespace CgsLedService.Modes.Waveform;

public sealed class WaveformMode : LedMode<WaveformModeConfig> {
    private readonly AudioCapture _capture;

    private int bufferSize => (int)(_capture.format.SampleRate * config.bufferSeconds);
    private int displayCount => (int)(_capture.format.SampleRate * config.displaySeconds / config.avgCount);

    private record struct Sample(float sum, int count, TimeSpan time) { public float value => sum / count; }
    private List<Sample>? _samples;
    private int _displayTail;
    private int _showDisplayTail;

    private readonly object _samplesLock = new();

    public WaveformMode(AudioCapture capture, WaveformModeConfig config) : base(config) => _capture = capture;

    public override void Start() {
        _capture.AddMonoListener(AddSample);
        lock(_samplesLock) {
            _samples = new List<Sample>(bufferSize);
            for(int i = 0; i < bufferSize; i++)
                _samples.Add(new Sample(0f, 0, TimeSpan.Zero));
            _displayTail = _samples.Count;
        }
    }

    public override void StopMode() => _capture.RemoveListener(AddSample);

    private void AddSample(float sample, int channel, TimeSpan time) {
        lock(_samplesLock) {
            if(_samples is null)
                return;
            while(_samples.Count > bufferSize) {
                _samples.RemoveAt(0);
                _displayTail--;
            }
            sample = Math.Abs(sample);
            if(_samples[^1].count >= config.avgCount)
                _samples.Add(new Sample(sample, 1, time));
            else {
                Sample s = _samples[^1];
                s.sum += sample;
                s.count++;
                _samples[^1] = s;
            }
        }
    }

    public override void Update() {
        lock(_samplesLock) {
            if(_samples is null)
                return;
            while(_displayTail < _samples.Count && time >= _samples[Math.Max(_displayTail, 0)].time)
                _displayTail++;
            _showDisplayTail = Math.Min(_displayTail, _samples.Count);
        }
    }
    public override void Draw(LedWriter writer, int strip) {
        if(_samples is null)
            return;

        int ledCount = writer.ledCounts[strip];

        Span<float> hues = stackalloc float[ledCount];
        Span<float> values = stackalloc float[ledCount];

        lock(_samplesLock) {
            for(int i = 0; i < ledCount; i++) {
                float progress = (float)i / ledCount * displayCount;
                int index = (Math.Max(_showDisplayTail - displayCount, 0) + (int)progress) % _samples.Count;
                int nextIndex = (index + 1) % _samples.Count;
                float bin = MoreMath.Lerp(_samples[index].value, _samples[nextIndex].value, progress - index);
                bin = MathF.Max(MathF.Min(bin, 1f), 0f);
                hues[i] = bin;
                values[i] = bin;
            }
        }

        config.colors.Write(writer, strip, (float)time.TotalSeconds, hues, values);
    }
}
