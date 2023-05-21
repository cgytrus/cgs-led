using System.Diagnostics;

using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CgsLedService.Helpers;

public sealed class AudioCapture : IDisposable {
    public delegate void SampleAddedFunc(float sample, int channel, TimeSpan time);

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    private WasapiCapture? _capture;
    private SampleAddedFunc? _sampleAdded;
    private SampleAddedFunc? _sampleAddedMono;

    public void AddListener(SampleAddedFunc callback) {
        _sampleAdded += callback;
        UpdateCaptureState();
    }
    public void AddMonoListener(SampleAddedFunc callback) {
        _sampleAddedMono += callback;
        UpdateCaptureState();
    }
    public void RemoveListener(SampleAddedFunc callback) {
        _sampleAdded -= callback;
        _sampleAddedMono -= callback;
        UpdateCaptureState();
    }

    private void UpdateCaptureState() {
        if(_sampleAdded is null && _sampleAddedMono is null && _capture is not null)
            StopCapture();
        else if((_sampleAdded is not null || _sampleAddedMono is not null) && _capture is null)
            StartCapture();
    }

    private void StartCapture() {
        _capture = new WasapiLoopbackCapture();

        _capture.DataAvailable += OnData;
        _capture.DataAvailable += OnDataMono;
        _capture.StartRecording();

        while(_capture.CaptureState == CaptureState.Starting) { }
    }

    private void OnData(object? _, WaveInEventArgs args) {
        if(_capture is null || _sampleAdded is null)
            return;
        WaveFormat format = _capture.WaveFormat;
        int channelSize = format.BlockAlign / format.Channels;
        TimeSpan time = _stopwatch.Elapsed;

        for(int i = 0; i < args.BytesRecorded; i += format.BlockAlign) {
            for(int j = 0; j < format.Channels; j++)
                _sampleAdded?.Invoke(BitConverter.ToSingle(args.Buffer, i + j * channelSize), j, time);
            time += TimeSpan.FromSeconds(1f / format.SampleRate);
        }
    }

    private void OnDataMono(object? _, WaveInEventArgs args) {
        if(_capture is null || _sampleAddedMono is null)
            return;
        WaveFormat format = _capture.WaveFormat;
        int channelSize = format.BlockAlign / format.Channels;
        TimeSpan time = _stopwatch.Elapsed;

        for(int i = 0; i < args.BytesRecorded; i += format.BlockAlign) {
            float total = 0f;
            for(int j = 0; j < format.Channels; j++)
                total += BitConverter.ToSingle(args.Buffer, i + j * channelSize);
            total /= format.Channels;
            _sampleAddedMono?.Invoke(total, 0, time);
            time += TimeSpan.FromSeconds(1f / format.SampleRate);
        }
    }

    private void StopCapture() {
        if(_capture is null)
            return;
        _capture.DataAvailable -= OnData;
        _capture.DataAvailable -= OnDataMono;
        _capture.StopRecording();
        while(_capture.CaptureState != CaptureState.Stopped) { }
        Dispose();
    }

    public void Dispose() {
        _capture?.Dispose();
        _capture = null;
    }
}
