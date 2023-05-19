using System.Reflection;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService;

public class WasapiProcessLoopbackCapture : WasapiCapture {
    private readonly int _bufferSize;

    public WasapiProcessLoopbackCapture(string processName, AppCaptureThingy.ProcessLoopbackMode loopbackMode) :
        base(WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice(), false, 5000) {
        FieldInfo? audioClientField = typeof(WasapiCapture).GetField("audioClient", ~BindingFlags.Default);
        if(audioClientField is null || !AppCaptureThingy.TryGetAudioClient(processName, loopbackMode, out AudioClient? audioClient))
            throw new InvalidOperationException("i dont fucking know");
        _bufferSize = 4410;//((AudioClient?)audioClientField.GetValue(this) ?? audioClient).BufferSize;
        audioClientField.SetValue(this, audioClient);
    }

    protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
        AudioClientStreamFlags.Loopback | base.GetAudioClientStreamFlags();

    public void InitializeProcessCaptureDevice() {
        Type type = typeof(WasapiCapture);
        FieldInfo? initialized = type.GetField("initialized", ~BindingFlags.Default);
        FieldInfo? audioClientInfo = type.GetField("audioClient", ~BindingFlags.Default);
        FieldInfo? bytesPerFrame = type.GetField("bytesPerFrame", ~BindingFlags.Default);
        FieldInfo? waveFormatInfo = type.GetField("waveFormat", ~BindingFlags.Default);
        FieldInfo? recordBuffer = type.GetField("recordBuffer", ~BindingFlags.Default);
        if(initialized is null || audioClientInfo is null || bytesPerFrame is null || waveFormatInfo is null ||
            recordBuffer is null)
            throw new InvalidOperationException("i dont fucking know");

        if((bool)(initialized.GetValue(this) ?? false))
            throw new InvalidOperationException("i dont fucking know");

        AudioClient? audioClient = (AudioClient?)audioClientInfo.GetValue(this);
        WaveFormat? waveFormat = (WaveFormat?)waveFormatInfo.GetValue(this);
        if(waveFormat is null)
            throw new InvalidOperationException("i dont fucking know");

        audioClient?.Initialize(ShareMode, GetAudioClientStreamFlags(), 50000000L, 0L, waveFormat, Guid.Empty);
        int bpf = waveFormat.Channels * waveFormat.BitsPerSample / 8;
        bytesPerFrame.SetValue(this, bpf);
        recordBuffer.SetValue(this, new byte[_bufferSize * bpf]);
        initialized.SetValue(this, true);
    }
}
