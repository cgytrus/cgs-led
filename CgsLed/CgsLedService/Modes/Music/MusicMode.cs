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
        values = new float[writer.totalLedCount];
        _capture = new WasapiLoopbackCapture();
        int blockAlign = _capture.WaveFormat.BlockAlign;
        int channels = _capture.WaveFormat.Channels;
        int channelSize = blockAlign / channels;
        _capture.DataAvailable += (_, args) => {
            for(int i = 0; i < args.BytesRecorded; i += blockAlign) {
                float total = 0f;
                for(int j = 0; j < blockAlign; j += channelSize)
                    total += BitConverter.ToSingle(args.Buffer, i + j) * config.volume;
                total /= channels;
                AddSample(total);
            }
        };
        _capture.StartRecording();
    }

    protected abstract void AddSample(float sample);

    protected override void Frame() {
        if(values is null)
            return;
        float time = (float)_stopwatch.Elapsed.TotalSeconds;
        for(int i = 0; i < writer.totalLedCount; i++)
            config.colors.WritePixel(writer, time, values[i]);
    }

    public void Dispose() {
        if(_capture is not null)
            GC.SuppressFinalize(this);
    }
}
