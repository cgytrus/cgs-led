using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

namespace CgsLedController;

public class LedController {
    public bool showFps { get; set; }
    public LedMode? customMode { get; private set; }

    private bool _stopping;

    private LedMode? _nextCustomMode;
    private bool _changeCustomMode;

    private readonly LedWriter _writer;
    private bool _updateLock = true;

    private readonly Stopwatch _timer = Stopwatch.StartNew();
    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frames;
    private const float FpsFrequency = 1f;

    public LedController(SerialPort port, IReadOnlyList<int> ledCounts) => _writer = new LedWriter(port, ledCounts);

    public void Start() {
        _writer.Open();
        Thread pingThread = new(MainThread);
        pingThread.Start();
    }

    private void MainThread() {
        while(!_writer.isOpen) { }
        while(_writer.isOpen)
            Update();
        customMode?.StopMode();
        _stopping = false;
    }

    private void Update() {
        _timer.Restart();

        _updateLock = true;

        if(_changeCustomMode) {
            _changeCustomMode = false;
            customMode?.StopMode();
            _nextCustomMode?.Start(_writer);
            customMode = _nextCustomMode;
        }

        if(customMode?.running is true)
            customMode.Update();
        _writer.Send();
        if(_stopping)
            _writer.Close();

        _updateLock = false;

        TimeSpan toWait = (customMode?.running is not true ? TimeSpan.FromSeconds(1f) : customMode.period) -
            _timer.Elapsed;
        if(toWait.Ticks > 0)
            Thread.Sleep(toWait);

        if(!showFps)
            return;
        _frames++;

        if(_fpsTimer.Elapsed.TotalSeconds < FpsFrequency)
            return;
        Console.WriteLine((_frames / _fpsTimer.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture));
        _fpsTimer.Restart();
        _frames = 0;
    }

    public void Stop() {
        SetPowerOff();
        _stopping = true;
    }

    public void ResetController() {
        WaitForLock();
        _writer.Write1((byte)DataType.Reset);
    }

    public void ResetSettings() {
        WaitForLock();
        _writer.Write1((byte)DataType.SettingsReset);
    }

    public void SetPowerOff() {
        WaitForLock();
        _nextCustomMode = null;
        _changeCustomMode = true;
        _writer.Write2((byte)DataType.Power, 0);
    }

    public void SetMode(LedMode mode) {
        WaitForLock();
        _nextCustomMode = mode;
        _changeCustomMode = true;
        _writer.Write2((byte)DataType.Power, 1);
    }

    public void SetBrightness(byte brightness) {
        WaitForLock();
        _writer.Write2((byte)DataType.Brightness, brightness);
    }

    private void WaitForLock() {
        while(_updateLock) { }
    }
}
