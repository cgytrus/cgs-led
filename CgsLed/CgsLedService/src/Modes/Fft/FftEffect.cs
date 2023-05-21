using NAudio.Dsp;

namespace CgsLedService.Modes.Fft;

public class FftEffect {
    public event EventHandler? fftUpdated;
    public IReadOnlyList<float> fft => _fft;

    public bool running {
        get => _sampleAggregator.performFft;
        set => _sampleAggregator.performFft = value;
    }

    //private const float FftNoiseCut = 0.08f;
    private readonly float[] _fft;
    private readonly SampleAggregator _sampleAggregator;

    public FftEffect(int binCount) {
        _fft = new float[binCount];
        _sampleAggregator = new SampleAggregator(binCount * 2);
        _sampleAggregator.fftCalculated += (_, args) => UpdateFft(args.result);
    }

    public void AddSample(float sample) => _sampleAggregator.Add(sample);

    private void UpdateFft(IReadOnlyList<Complex> rawFft) {
        for(int i = 0; i < _fft.Length; i++) {
            // X = real, Y = imaginary
            Complex current = rawFft[i];

            // normalization
            /*float maxValue = MathF.Max(MathF.Abs(current.X), MathF.Abs(current.Y));
            if(maxValue > 0f) {
                current.X /= maxValue;
                current.Y /= maxValue;
            }*/

            // db conversion
            //float value = 10f * MathF.Log10(current.X * current.X + current.Y * current.Y);
            float value = MathF.Sqrt(current.X * current.X + current.Y * current.Y);
            //float value = MathF.Log10((current.X * current.X + current.Y * current.Y) / 10f) + 2f;

            _fft[i] = value;
        }

        fftUpdated?.Invoke(this, EventArgs.Empty);
    }

    public static float GetBin(IReadOnlyList<float> fft, int addCount, int count, float position) {
        count = Math.Min(count, fft.Count);
        float posA = position * position;
        float posB = (position - 0.4f) * 1.4f + 0.16f;
        float posC = position / 4f;
        position = MathF.Max(MathF.Max(posA, posB), posC);
        float fftIndexF = position * (count - 1);
        int fftIndex = (int)MathF.Floor(fftIndexF);
        float currentFft = fft[fftIndex] / addCount;
        //return currentFft;
        float nextFft = currentFft;
        if(fftIndex + 1 < count) nextFft = fft[fftIndex + 1] / addCount;
        return Lerp(currentFft, nextFft, SmoothStep(fftIndexF - fftIndex));
    }

    public static float ProcessBin(float bin, float noiseCut) {
        // 0.649... is sin(sqrt(2.0) / 2.0) which is sin(degrees(45))
        bin = MathF.Sqrt(MathF.Min(bin / 0.649636939f, 1f));
        float x = bin;
        /*float a = x / 5f;
        float b = MathF.Sqrt((x - noiseCut) / (1f - noiseCut));*/
        float a = x / 5f;
        float b = (x - noiseCut) / (1f - noiseCut);
        /*float a = MathF.Log2(x + 1f);
        a *= a;
        float b = MathF.Log2(x - noiseCut + 1f);*/
        bin = MathF.Max(a, b);
        return bin;
    }


    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    private static float SmoothStep(float x) => x * x * (3f - 2f * x);
}
