using System.IO.Ports;

namespace CgsLedController;

public class LedController {
    private readonly SerialPort _port;
    private readonly byte[] _portBuffer = new byte[4];
    private readonly object _portLock = new();

    public CustomMode? customMode { get; private set; }

    public LedController(SerialPort port) => _port = port;

    public void Start() {
        _port.Open();
        Thread pingThread = new(PingThread);
        pingThread.Start();
    }

    private void PingThread() {
        while(!_port.IsOpen) { }
        while(_port.IsOpen) {
            Thread.Sleep(6900);
            if(customMode?.stopped is not true)
                continue;
            if(!_port.IsOpen)
                break;
            _portBuffer[0] = (byte)DataType.Ping;
            WritePort(1);
        }
    }

    public void Stop() {
        customMode?.StopMode();
        while(_port is { IsOpen: true, BytesToWrite: > 0 })
            Thread.Sleep(100);
        lock(_portLock) {
            _port.Close();
        }
    }

    public void ResetController() {
        _portBuffer[0] = (byte)DataType.Reset;
        WritePort(1);
    }

    public void ResetSettings() {
        _portBuffer[0] = (byte)DataType.SettingsReset;
        WritePort(1);
    }

    public void SetMode(BuiltInMode builtInMode) {
        // we should use the SetMode(CustomMode) overload to set a custom mode
        if(builtInMode == BuiltInMode.Custom)
            return;
        customMode?.StopMode();
        _portBuffer[0] = (byte)DataType.Mode;
        _portBuffer[1] = (byte)builtInMode;
        WritePort(2);
    }

    public void SetMode(CustomMode customMode) {
        this.customMode?.StopMode();

        _portBuffer[0] = (byte)DataType.Mode;
        _portBuffer[1] = (byte)BuiltInMode.Custom;

        this.customMode = customMode;
        this.customMode.Start(_port, _portLock);

        WritePort(2);
    }

    public void SetBrightness(byte brightness) {
        _portBuffer[0] = (byte)DataType.Brightness;
        _portBuffer[1] = brightness;
        WritePort(2);
    }

    private void WritePort(int count) {
        lock(_portLock) {
            _port.Write(_portBuffer, 0, count);
        }
    }
}
