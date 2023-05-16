using CgsLedController;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Modes.Music.Fft;

public class FftMode : LedMode<FftMode.Configuration>, IDisposable {
    public new record Configuration(
        TimeSpan period,
        float volume = 1f,
        int showStart = 384,
        int showCount = 64,
        float noiseCut = 0.08f,
        bool mirror = true) :
        LedMode.Configuration(period);

    public override bool running => _capture?.CaptureState == CaptureState.Capturing;

    private WasapiLoopbackCapture? _capture;
    private FftEffect? _fft;

    private float[]? _rawFft;
    private float[]? _bins;

    private bool _fftReady = true;
    private int _fftAddCounter;
    private bool _newFrame;

    public FftMode(Configuration config) : base(config) { }

    public override void StopMode() {
        _capture?.StopRecording();
        while(_capture is not null && _capture.CaptureState != CaptureState.Stopped) { }
        if(_fft is not null)
            _fft.running = false;
        _capture = null;
        _fft = null;
    }

    protected override void Main() {
        _bins = new float[writer.totalLedCount];
        const int fftBinCount = 512;
        _rawFft = new float[fftBinCount];
        _fft = new FftEffect(fftBinCount);
        _fft.fftUpdated += (_, _) => UpdateFft(_fft.fft);

        _capture = new WasapiLoopbackCapture();
        int blockAlign = _capture.WaveFormat.BlockAlign;
        _capture.DataAvailable += (_, args) => {
            for(int index = 0; index < args.BytesRecorded; index += blockAlign) {
                float sample = BitConverter.ToSingle(args.Buffer, index)/* * config.volume*/;
                _fft.AddSample(sample);
            }
        };

        _capture.StartRecording();
        _fft.running = true;
    }

    private void UpdateFft(IReadOnlyList<float> fft) {
        while(!_fftReady) { }

        if(_newFrame) {
            _fftAddCounter = 0;
            _newFrame = false;
        }

        for(int i = 0; i < config.showCount; i++) {
            if(_fftAddCounter <= 0)
                _rawFft![i] = 0f;
            _rawFft![i] += fft[config.showStart + i] * config.volume;
        }

        _fftAddCounter++;
    }

    protected override void Frame() {
        _fftReady = false;

        for(byte strip = 0; strip < writer.ledCounts.Count; strip++) {
            int fullLedCount = writer.ledCounts[strip];
            int ledCount = config.mirror ? writer.halfLedCounts[strip] : fullLedCount;
            int ledStart = writer.ledStarts[strip];
            for(int i = 0; i < ledCount; i++) {
                if(_fftAddCounter == 0) {
                    _bins![ledStart + i] = 0f;
                    continue;
                }

                float position = (float)i / ledCount;
                float bin = FftEffect.GetBin(_rawFft!, _fftAddCounter, config.showCount, position);
                bin = FftEffect.ProcessBin(bin, config.noiseCut);
                bin = MathF.Max(MathF.Min(bin, 1f), 0f);
                _bins![ledStart + i] = bin;
                if(config.mirror)
                    _bins![ledStart + fullLedCount - i - 1] = bin;
            }
        }

        for(int i = 0; i < writer.totalLedCount; i++) {
            float bin = _bins![i];
            writer.WriteHsv(bin * 120f, 1f, bin, false);
        }

        _newFrame = true;
        _fftReady = true;
    }

    public void Dispose() {
        if(_capture is not null)
            GC.SuppressFinalize(this);
    }
}
