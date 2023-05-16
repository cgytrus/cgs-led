using CgsLedController;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Modes.Music;

public abstract class MusicMode<TConfig> : LedMode<TConfig>, IDisposable
    where TConfig : MusicMode<TConfig>.Configuration {
    public new record Configuration(
        TimeSpan period,
        float volume = 1f) :
        LedMode.Configuration(period);

    public override bool running => _capture?.CaptureState == CaptureState.Capturing;

    private WasapiLoopbackCapture? _capture;

    protected MusicMode(TConfig config) : base(config) { }

    public override void StopMode() {
        _capture?.StopRecording();
        while(_capture is not null && _capture.CaptureState != CaptureState.Stopped) { }
        _capture = null;
    }

    protected override void Main() {
        _capture = new WasapiLoopbackCapture();
        int blockAlign = _capture.WaveFormat.BlockAlign;
        _capture.DataAvailable += (_, args) => {
            for(int i = 0; i < args.BytesRecorded; i += blockAlign)
                AddSample(BitConverter.ToSingle(args.Buffer, i) * config.volume);
        };
        _capture.StartRecording();
    }

    protected abstract void AddSample(float sample);

    public void Dispose() {
        if(_capture is not null)
            GC.SuppressFinalize(this);
    }
}
