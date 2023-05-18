using System.Diagnostics;

using CgsLedController;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Modes.Music;

public abstract class MusicMode<TConfig> : LedMode<TConfig>, IDisposable
    where TConfig : MusicMode<TConfig>.Configuration {
    public new record Configuration(
        TimeSpan period,
        float volume,
        MusicColors colors) :
        LedMode.Configuration(period);

    public override bool running => _capture?.CaptureState == CaptureState.Capturing;

    private WasapiLoopbackCapture? _capture;

    protected abstract bool forceMono { get; }

    protected float[]? hues { get; private set; }
    protected float[]? values { get; private set; }
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    protected MusicMode(TConfig config) : base(config) { }

    public override void StopMode() {
        _capture?.StopRecording();
        while(_capture is not null && _capture.CaptureState != CaptureState.Stopped) { }
        _capture = null;
    }

    protected override void Main() {
        _stopwatch.Restart();
        hues = new float[writer.totalLedCount];
        values = new float[writer.totalLedCount];
        _capture = new WasapiLoopbackCapture();
        int blockAlign = _capture.WaveFormat.BlockAlign;
        int channels = _capture.WaveFormat.Channels;
        int channelSize = blockAlign / channels;
        float sampleRate = _capture.WaveFormat.SampleRate;

        void OnData(object? _, WaveInEventArgs args) {
            TimeSpan time = this.time;
            for(int i = 0; i < args.BytesRecorded; i += blockAlign) {
                for(int j = 0; j < channels; j++)
                    AddSample(BitConverter.ToSingle(args.Buffer, i + j * channelSize) * config.volume, j, time);
                time += TimeSpan.FromSeconds(1f / sampleRate);
            }
        }
        void OnDataMono(object? _, WaveInEventArgs args) {
            TimeSpan time = this.time;
            for(int i = 0; i < args.BytesRecorded; i += blockAlign) {
                float total = 0f;
                for(int j = 0; j < channels; j++)
                    total += BitConverter.ToSingle(args.Buffer, i + j * channelSize) * config.volume;
                total /= channels;
                AddSample(total, 0, time);
                time += TimeSpan.FromSeconds(1f / sampleRate);
            }
        }

        _capture.DataAvailable += forceMono ? OnDataMono : OnData;
        _capture.StartRecording();
    }

    protected abstract void AddSample(float sample, int channel, TimeSpan time);

    protected override void Frame(float deltaTime) {
        if(hues is null || values is null)
            return;
        float time = (float)_stopwatch.Elapsed.TotalSeconds;
        for(int i = 0; i < writer.totalLedCount; i++)
            config.colors.WritePixel(writer, time, hues[i], values[i]);
    }

    public void Dispose() {
        if(_capture is not null)
            GC.SuppressFinalize(this);
    }
}
