using NAudio.Dsp;

namespace CgsLedService.Modes.Music.Fft;

internal sealed class SampleAggregator {
    public event EventHandler<FftEventArgs>? fftCalculated;
    public bool performFft { get; set; }

    private readonly Complex[] _fftBuffer;
    private readonly FftEventArgs _fftArgs;
    private int _fftPos;
    private readonly int _fftLength;
    private readonly int _m;

    public SampleAggregator(int fftLength) {
        if(!IsPowerOfTwo(fftLength))
            throw new ArgumentException("FFT Length must be a power of two");
        _fftLength = fftLength;
        _m = (int)Math.Log(fftLength, 2.0);
        _fftBuffer = new Complex[fftLength];
        _fftArgs = new FftEventArgs(_fftBuffer);
    }

    private static bool IsPowerOfTwo(int x) => (x & x - 1) == 0;

    public void Add(float value) {
        if(!performFft || fftCalculated == null) return;
        _fftBuffer[_fftPos].X = (float)(value * FastFourierTransform.HammingWindow(_fftPos, _fftLength));
        _fftBuffer[_fftPos].Y = 0;
        _fftPos++;
        if(_fftPos < _fftLength) return;
        _fftPos = 0;
        FastFourierTransform.FFT(true, _m, _fftBuffer);
        fftCalculated(this, _fftArgs);
    }
}

public class FftEventArgs : EventArgs {
    public FftEventArgs(Complex[] result) => this.result = result;
    public Complex[] result { get; }
}
