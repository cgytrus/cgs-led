namespace CgsLedService.Modes.Music.Fft;

public class FftMode : MusicMode<FftMode.Configuration> {
    public new record Configuration(
        TimeSpan period,
        float volume,
        string? process,
        bool excludeProcess,
        MusicColors colors,
        int showStart = 0,
        int showCount = 56,
        float noiseCut = 0.25f,
        bool mirror = true) :
        MusicMode<Configuration>.Configuration(period, volume, process, excludeProcess, colors);

    protected override bool forceMono => true;

    private FftEffect? _fft;

    private float[]? _rawFft;

    private bool _fftReady = true;
    private int _fftAddCounter;
    private bool _newFrame;

    public FftMode(Configuration config) : base(config) { }

    public override void StopMode() {
        if(_fft is not null)
            _fft.running = false;
        _fft = null;
        base.StopMode();
    }

    protected override void Main() {
        const int fftBinCount = 512;
        _rawFft = new float[fftBinCount];
        _fft = new FftEffect(fftBinCount);
        _fft.fftUpdated += (_, _) => UpdateFft(_fft.fft);
        base.Main();
        _fft.running = true;
    }

    protected override void AddSample(float sample, int channel, TimeSpan time) => _fft?.AddSample(sample);

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

    // ReSharper disable once CognitiveComplexity
    protected override void Frame(float deltaTime) {
        if(_rawFft is null || hues is null || values is null)
            return;

        _fftReady = false;

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int fullLedCount = writer.ledCounts[strip];
            int ledCount = config.mirror ? writer.halfLedCounts[strip] : fullLedCount;
            int ledStart = writer.ledStarts[strip];
            for(int i = 0; i < ledCount; i++) {
                if(_fftAddCounter == 0) {
                    hues[ledStart + i] = 0f;
                    values[ledStart + i] = 0f;
                    continue;
                }

                float position = (float)i / ledCount;
                float bin = FftEffect.GetBin(_rawFft, _fftAddCounter, config.showCount, position);
                bin = FftEffect.ProcessBin(bin, config.noiseCut);
                bin = MathF.Max(MathF.Min(bin, 1f), 0f);
                hues[ledStart + i] = bin;
                values[ledStart + i] = bin;
                if(!config.mirror)
                    continue;
                hues[ledStart + fullLedCount - i - 1] = bin;
                values[ledStart + fullLedCount - i - 1] = bin;
            }
        }

        base.Frame(deltaTime);

        _newFrame = true;
        _fftReady = true;
    }
}
