using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using CgsLedController;

using CgsLedServiceTypes.Config;

using NAudio.CoreAudioApi;
using NAudio.Wave;

using JetBrains.Annotations;

using MonoMod.RuntimeDetour;

using NAudio.CoreAudioApi.Interfaces;

namespace CgsLedService.Helpers;

public sealed class AudioCapture(AudioCaptureConfig config) : IDisposable {
    public delegate void SampleAddedFunc(float sample, int channel, TimeSpan time);

    public AudioCaptureConfig config { get; set; } = config;

    public WaveFormat format { get; private set; } = new(0, 1);

    private WasapiCapture? _capture;
    private SampleAddedFunc? _sampleAdded;
    private SampleAddedFunc? _sampleAddedMono;

    private static int _capturePid;
    private static WaveFormat? _captureFormat;
    private static int _captureBufferSize;

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
        _capturePid = config.pid != 0 || config.exe is null ? config.pid :
            Process.GetProcessesByName(config.exe).FirstOrDefault()?.Id ?? 0;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        if(_capturePid == 0) {
            _capture = new WasapiLoopbackCapture();
        }
        else {
            _captureFormat = WasapiLoopbackCapture.GetDefaultLoopbackCaptureDevice().AudioClient.MixFormat;
            _captureBufferSize = _captureFormat.SampleRate / 10;

            MethodInfo fromGetAudioClient = typeof(MMDevice).GetMethod("GetAudioClient", flags)!;
            MethodInfo toGetAudioClient = typeof(AudioCapture).GetMethod(nameof(GetAudioClientHook), flags)!;

            MethodInfo fromGetMixFormat = typeof(AudioClient).GetMethod($"get_{nameof(AudioClient.MixFormat)}", flags)!;
            MethodInfo toGetMixFormat = typeof(AudioCapture).GetMethod(nameof(GetMixFormatHook), flags)!;

            using(new Hook(fromGetAudioClient, toGetAudioClient))
            using(new Hook(fromGetMixFormat, toGetMixFormat)) {
                _capture = new WasapiLoopbackCapture();
            }
        }

        format = _capture.WaveFormat;

        _capture.DataAvailable += OnData;
        _capture.DataAvailable += OnDataMono;

        if(_capturePid == 0) {
            _capture.StartRecording();
        }
        else {
            MethodInfo fromGetBufferSize = typeof(AudioClient).GetMethod($"get_{nameof(AudioClient.BufferSize)}", flags)!;
            MethodInfo toGetBufferSize = typeof(AudioCapture).GetMethod(nameof(GetBufferSizeHook), flags)!;

            using(new Hook(fromGetBufferSize, toGetBufferSize)) {
                _capture.StartRecording();
            }
        }

        while(_capture.CaptureState == CaptureState.Starting) { }
    }

#region WINAPI stuff

    [UsedImplicitly, StructLayout(LayoutKind.Sequential)]
    private struct AudioClientActivationParams {
        public AudioClientActivationType activationType;
        public AudioClientProcessLoopbackParams processLoopbackParams;
    }

    [UsedImplicitly]
    private enum AudioClientActivationType {
        Default,
        ProcessLoopback
    }

    [UsedImplicitly, StructLayout(LayoutKind.Sequential)]
    private struct AudioClientProcessLoopbackParams {
        public int targetProcessId;
        public ProcessLoopbackMode processLoopbackMode;
    }

    [UsedImplicitly]
    private enum ProcessLoopbackMode {
        IncludeTargetProcessTree,
        ExcludeTargetProcessTree
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    private static extern int ActivateAudioInterfaceAsync(
        [In, MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        [In] in PropVariant activationParams,
        [In] IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
    private interface IActivateAudioInterfaceCompletionHandler {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
    private interface IActivateAudioInterfaceAsyncOperation {
        void GetActivateResult(out int activateResult, out IAudioClient activateInterface);
    }
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90")]
    private interface IAgileObject;
    private class ActivateAudioInterfaceCompletionHandler
        : IActivateAudioInterfaceCompletionHandler, IAgileObject {
        private readonly TaskCompletionSource<IAudioClient> _tcs = new();
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation) {
            activateOperation.GetActivateResult(out int hr, out IAudioClient unk);
            Exception? ex = Marshal.GetExceptionForHR(hr, new IntPtr(-1));
            if(ex is not null) {
                _tcs.TrySetException(ex);
                return;
            }
            try { _tcs.SetResult(unk); }
            catch(Exception e) { _tcs.TrySetException(e); }
        }
        public TaskAwaiter<IAudioClient> GetAwaiter() => _tcs.Task.GetAwaiter();
    }

    [UsedImplicitly]
    private static unsafe AudioClient GetAudioClientHook(MMDevice self) {
        IAudioClient res;
        AudioClientActivationParams actParams = new() {
            activationType = AudioClientActivationType.ProcessLoopback,
            processLoopbackParams = new AudioClientProcessLoopbackParams {
                targetProcessId = _capturePid,
                processLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree
            }
        };
        int actParamsSize = sizeof(AudioClientActivationParams);
        nint actParamsPtr = Marshal.AllocHGlobal(actParamsSize);
        try {
            Marshal.StructureToPtr(actParams, actParamsPtr, false);
            PropVariant propVar = new() {
                vt = (short)VarEnum.VT_BLOB,
                blobVal = new Blob {
                    Length = actParamsSize,
                    Data = actParamsPtr
                }
            };
            ActivateAudioInterfaceCompletionHandler handler = new();
            Marshal.ThrowExceptionForHR(ActivateAudioInterfaceAsync("VAD\\Process_Loopback",
                Guid.Parse("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), in propVar, handler, out IActivateAudioInterfaceAsyncOperation _));
            res = handler.GetAwaiter().GetResult();
        }
        finally {
            Marshal.FreeHGlobal(actParamsPtr);
        }
        return new AudioClient(res);
    }

    [UsedImplicitly]
    private static WaveFormat? GetMixFormatHook(AudioClient self) => _captureFormat;

    [UsedImplicitly]
    private static int GetBufferSizeHook(AudioClient self) => _captureBufferSize;

#endregion

    private void OnData(object? _, WaveInEventArgs args) {
        if(_capture is null || _sampleAdded is null)
            return;
        WaveFormat format = _capture.WaveFormat;
        int channelSize = format.BlockAlign / format.Channels;
        TimeSpan time = LedController.time;

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
        TimeSpan time = LedController.time;

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
