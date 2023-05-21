namespace CgsLedService.Modes.Music.Fft;

public class FftMode : MusicMode<FftMode.Configuration> {
    public new record Configuration(
        MusicConfig music,
        int showStart = 0,
        int showCount = 56,
        float noiseCut = 0.25f,
        bool mirror = true) :
        MusicMode<FftMode.Configuration>.Configuration(music);

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

    public override void Update() { }
    public override void Draw(int strip) {
        if(_rawFft is null || hues is null || values is null)
            return;

        _fftReady = false;

        int fullLedCount = writer.ledCounts[strip];
        int ledCount = config.mirror ? writer.halfLedCounts[strip] : fullLedCount;
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

        base.Draw(strip);

        _newFrame = true;
        _fftReady = true;
    }
}
