using CgsLedController;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Modes.Music;

public abstract class MusicMode<TConfig> : LedMode<TConfig>, IDisposable
    where TConfig : MusicMode<TConfig>.Configuration {
    public abstract record Configuration(MusicConfig music);

    protected abstract bool forceMono { get; }

    protected float[]? hues { get; private set; }
    protected float[]? values { get; private set; }

    private WasapiCapture? _capture;

    protected MusicMode(TConfig config) : base(config) { }

    public override void StopMode() {
        _capture?.StopRecording();
        while(_capture is not null && _capture.CaptureState != CaptureState.Stopped) { }
        _capture = null;
    }

    protected override void Main() {
        hues = new float[writer.ledCounts.Max()];
        values = new float[hues.Length];
        _capture = config.music.process is null ? new WasapiLoopbackCapture() :
            new WasapiProcessLoopbackCapture(config.music.process,
                config.music.excludeProcess ? AppCaptureThingy.ProcessLoopbackMode.ExcludeTargetProcessTree :
                    AppCaptureThingy.ProcessLoopbackMode.IncludeTargetProcessTree);

        int blockAlign = _capture.WaveFormat.BlockAlign;
        int channels = _capture.WaveFormat.Channels;
        int channelSize = blockAlign / channels;
        float sampleRate = _capture.WaveFormat.SampleRate;

        void OnData(object? _, WaveInEventArgs args) {
            TimeSpan time = this.time;
            for(int i = 0; i < args.BytesRecorded; i += blockAlign) {
                for(int j = 0; j < channels; j++)
                    AddSampleInternal(BitConverter.ToSingle(args.Buffer, i + j * channelSize), j, time);
                time += TimeSpan.FromSeconds(1f / sampleRate);
            }
        }
        void OnDataMono(object? _, WaveInEventArgs args) {
            TimeSpan time = this.time;
            for(int i = 0; i < args.BytesRecorded; i += blockAlign) {
                float total = 0f;
                for(int j = 0; j < channels; j++)
                    total += BitConverter.ToSingle(args.Buffer, i + j * channelSize);
                total /= channels;
                AddSampleInternal(total, 0, time);
                time += TimeSpan.FromSeconds(1f / sampleRate);
            }
        }

        _capture.DataAvailable += forceMono ? OnDataMono : OnData;
        if(_capture is WasapiProcessLoopbackCapture processCapture)
            processCapture.InitializeProcessCaptureDevice();
        _capture.StartRecording();
    }

    private void AddSampleInternal(float sample, int channel, TimeSpan time) {
        AddSample(sample, channel, time);
    }

    protected abstract void AddSample(float sample, int channel, TimeSpan time);

    public override void Draw(int strip) {
        if(hues is null || values is null)
            return;
        float time = (float)this.time.TotalSeconds;
        int ledCount = writer.ledCounts[strip];
        for(int i = 0; i < ledCount; i++) {
            float x = (float)i / ledCount;
            config.music.colors.WritePixel(writer, time, x, hues[i], values[i]);
        }
    }

    public void Dispose() {
        if(_capture is not null)
            GC.SuppressFinalize(this);
    }
}
