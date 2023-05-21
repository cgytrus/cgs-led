using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using JetBrains.Annotations;

using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;

namespace CgsLedService;

// modding skills come in handy hehe
public static class AppCaptureThingy {
    [PublicAPI] public enum ProcessLoopbackMode { IncludeTargetProcessTree, ExcludeTargetProcessTree }
    [PublicAPI] private enum ActivationType { Default, ProcessLoopback }
    [PublicAPI, StructLayout(LayoutKind.Sequential)]
    private struct ActivationParameters {
        private ActivationType _type;
        private AudioClientProcessLoopbackParams _processLoopbackParams;
        public ActivationParameters(ActivationType type, AudioClientProcessLoopbackParams processLoopbackParams) {
            _type = type;
            _processLoopbackParams = processLoopbackParams;
        }
    }
    [PublicAPI, StructLayout(LayoutKind.Sequential)]
    private struct AudioClientProcessLoopbackParams {
        private uint _targetProcessId;
        private ProcessLoopbackMode _processLoopbackMode;
        public AudioClientProcessLoopbackParams(int targetProcessId, ProcessLoopbackMode processLoopbackMode) {
            _targetProcessId = (uint)targetProcessId;
            _processLoopbackMode = processLoopbackMode;
        }
    }

    public static bool TryGetAudioClient(string processName, ProcessLoopbackMode mode,
        [NotNullWhen(true)] out AudioClient? audioClient) {
        audioClient = null;

        Process[] processes = Process.GetProcessesByName(processName);
        if(processes.Length == 0)
            return false;

        AudioClientProcessLoopbackParams activationParams = new(processes[0].Id, mode);
        audioClient = ActivateProcessLoopback(activationParams);
        return true;
    }

#pragma warning disable SYSLIB1054
    [PreserveSig]
    [DllImport("Mmdevapi", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int ActivateAudioInterfaceAsync(
        [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
        Guid riid,
        ref PropVariant activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation result
    );
#pragma warning restore SYSLIB1054

    private static AudioClient ActivateProcessLoopback(AudioClientProcessLoopbackParams loopbackParams) {
        ActivateAudioInterfaceCompletionHandler completionHandler = new();
        ActivationParameters activationParams = new(ActivationType.ProcessLoopback, loopbackParams);
        int actParamsSize = Marshal.SizeOf(activationParams);
        nint actParamsPtr = Marshal.AllocHGlobal(actParamsSize);
        try {
            Marshal.StructureToPtr(activationParams, actParamsPtr, false);
            PropVariant propVariant = new() {
                vt = 65,
                blobVal = new Blob {
                    Length = actParamsSize,
                    Data = actParamsPtr
                }
            };
            int error = ActivateAudioInterfaceAsync("VAD\\Process_Loopback",
                typeof(IAudioClient).GUID, ref propVariant, completionHandler,
                out IActivateAudioInterfaceAsyncOperation _);
            if(error < 0)
                throw Marshal.GetExceptionForHR(error, new nint(-1)) ?? new InvalidOperationException();
            TaskAwaiter<IAudioClient> awaiter = completionHandler.GetAwaiter();
            while(!awaiter.IsCompleted) { }
            return new AudioClient(awaiter.GetResult());
        }
        finally {
            Marshal.FreeHGlobal(actParamsPtr);
        }
    }

    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("94ea2b94-e9cc-49e0-c0ff-ee64ca8f5b90")]
    [ComImport]
    private interface IAgileObject { }

    private class ActivateAudioInterfaceCompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject {
        private readonly TaskCompletionSource<IAudioClient> _tcs = new();
        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation) {
            activateOperation.GetActivateResult(out int activateResult, out object activateInterface);
            if(activateResult < 0) {
                _tcs.TrySetException(Marshal.GetExceptionForHR(activateResult, new nint(-1)) ??
                    new InvalidOperationException());
                return;
            }
            IAudioClient result = (IAudioClient)activateInterface;
            try { _tcs.SetResult(result); }
            catch(Exception ex) { _tcs.TrySetException(ex); }
        }

        public TaskAwaiter<IAudioClient> GetAwaiter() => _tcs.Task.GetAwaiter();
    }
}
