using CgsLedController;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Modes.Fft;

public class FftMode : CustomMode, IDisposable {
    public float volume { get; set; } = 1f;
    public int showStart { get; set; } = 384;
    public int showCount { get; set; } = 64;
    public float noiseCut { get; set; } = 0.08f;
    public bool mirror { get; set; } = true;

    protected override DataType dataType => mirror ? DataType.FftMirroredData : DataType.FftData;
    protected override bool running => _capture?.CaptureState == CaptureState.Capturing;

    private WasapiLoopbackCapture? _capture;
    private FftEffect? _fft;

    private float[]? _rawFft;

    private bool _fftReady = true;
    private int _fftAddCounter;
    private bool _newFrame;

    public override void StopMode() {
        _capture?.StopRecording();
        base.StopMode();
    }

    protected override void Main() {
        const int fftBinCount = 512;
        _rawFft = new float[fftBinCount];
        _fft = new FftEffect(fftBinCount);
        _fft.fftUpdated += (_, _) => UpdateFft(_fft.fft);

        _capture = new WasapiLoopbackCapture();
        int blockAlign = _capture.WaveFormat.BlockAlign;
        _capture.DataAvailable += (_, args) => {
            for(int index = 0; index < args.BytesRecorded; index += blockAlign) {
                float sample = BitConverter.ToSingle(args.Buffer, index)/* * volume*/;
                _fft.AddSample(sample);
            }
        };

        _capture.StartRecording();
        _fft.running = true;
    }

    protected override void MainEnd() {
        while(_capture is not null && _capture.CaptureState != CaptureState.Stopped)
            Thread.Sleep(1000);
        if(_fft is not null)
            _fft.running = false;
        _capture = null;
        _fft = null;
    }

    private void UpdateFft(IReadOnlyList<float> fft) {
        while(!_fftReady) { }

        if(_newFrame) {
            _fftAddCounter = 0;
            _newFrame = false;
        }

        for(int i = 0; i < showCount; i++) {
            if(_fftAddCounter <= 0) _rawFft![i] = fft[showStart + i] * volume;
            else _rawFft![i] += fft[showStart + i] * volume;
        }

        _fftAddCounter++;
    }

    protected override void Frame() {
        _fftReady = false;

        for(byte strip = 0; strip < ledCounts.Length; strip++) {
            int ledCount = mirror ? halfLedCounts[strip] : ledCounts[strip];

            for(int i = 0; i < ledCount; i++) {
                if(_fftAddCounter == 0) {
                    Set1(0);
                    continue;
                }

                float position = (float)i / ledCount;
                float bin = FftEffect.GetBin(_rawFft!, _fftAddCounter, showCount, position);
                bin = FftEffect.ProcessBin(bin, noiseCut);
                byte value = (byte)MathF.Max(MathF.Min(bin * 255f, 255f), 0f);
                Set1(value);
            }
        }

        _newFrame = true;
        _fftReady = true;
    }

    public void Dispose() {
        if(_capture is not null) GC.SuppressFinalize(this);
    }
}
