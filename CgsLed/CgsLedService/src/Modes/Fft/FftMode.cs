using CgsLedController;

using CgsLedService.Helpers;

namespace CgsLedService.Modes.Fft;

public class FftMode : LedMode<FftMode.Configuration> {
    public record Configuration(
        MusicColors colors,
        int showStart = 0,
        int showCount = 56,
        float noiseCut = 0.25f,
        bool mirror = true);

    private readonly AudioCapture _capture;

    private FftEffect? _fft;

    private float[]? _rawFft;

    private bool _fftReady = true;
    private int _fftAddCounter;
    private bool _newFrame;

    public FftMode(AudioCapture capture, Configuration config) : base(config) => _capture = capture;

    protected override void Main() {
        const int fftBinCount = 512;
        _rawFft = new float[fftBinCount];
        _fft = new FftEffect(fftBinCount);
        _fft.fftUpdated += (_, _) => UpdateFft(_fft.fft);
        _fft.running = true;
        _capture.AddMonoListener(AddSample);
    }

    public override void StopMode() {
        if(_fft is not null)
            _fft.running = false;
        _fft = null;
        _capture.RemoveListener(AddSample);
    }

    private void AddSample(float sample, int channel, TimeSpan time) => _fft?.AddSample(sample);

    private void UpdateFft(IReadOnlyList<float> fft) {
        while(!_fftReady) { }

        if(_newFrame) {
            _fftAddCounter = 0;
            _newFrame = false;
        }

        for(int i = 0; i < config.showCount; i++) {
            if(_fftAddCounter <= 0)
                _rawFft![i] = 0f;
            _rawFft![i] += fft[config.showStart + i];
        }

        _fftAddCounter++;
    }

    public override void Update() { }
    public override void Draw(int strip) {
        if(_rawFft is null)
            return;

        _fftReady = false;

        int fullLedCount = writer.ledCounts[strip];
        int ledCount = config.mirror ? writer.halfLedCounts[strip] : fullLedCount;

        Span<float> hues = stackalloc float[fullLedCount];
        Span<float> values = stackalloc float[fullLedCount];

        for(int i = 0; i < ledCount; i++) {
            if(_fftAddCounter == 0) {
                hues[i] = 0f;
                values[i] = 0f;
                continue;
            }

            float position = (float)i / ledCount;
            float bin = FftEffect.GetBin(_rawFft, _fftAddCounter, config.showCount, position);
            bin = FftEffect.ProcessBin(bin, config.noiseCut);
            bin = MathF.Max(MathF.Min(bin, 1f), 0f);
            hues[i] = bin;
            values[i] = bin;
            if(!config.mirror)
                continue;
            hues[fullLedCount - i - 1] = bin;
            values[fullLedCount - i - 1] = bin;
        }

        config.colors.Write(writer, strip, (float)time.TotalSeconds, hues, values);

        _newFrame = true;
        _fftReady = true;
    }
}
