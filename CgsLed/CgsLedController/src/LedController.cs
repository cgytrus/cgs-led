using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

namespace CgsLedController;

public class LedController {
    public record Configuration(float brightness, bool showFps);
    public Configuration config { get; set; }
    public LedMode? mode { get; private set; }

    private bool _stopping;

    private LedMode? _nextMode;
    private bool _changeMode;

    private readonly LedWriter _writer;
    private bool _updateLock = true;

    private readonly Stopwatch _timer = Stopwatch.StartNew();
    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frames;
    private const float FpsFrequency = 1f;

    public LedController(Configuration config, SerialPort port, IReadOnlyList<int> ledCounts) {
        this.config = config;
        _writer = new LedWriter(this, port, ledCounts);
    }

    public void Start() {
        _writer.Open();
        Thread pingThread = new(MainThread);
        pingThread.Start();
    }

    private void MainThread() {
        while(!_writer.isOpen) { }
        while(_writer.isOpen)
            Update();
        mode?.StopMode();
        _stopping = false;
    }

    private void Update() {
        _timer.Restart();

        _updateLock = true;

        if(_changeMode) {
            _changeMode = false;
            mode?.StopMode();
            _nextMode?.Start(_writer);
            mode = _nextMode;
        }

        if(mode?.running is true)
            mode.Update();
        _writer.Send();
        if(_stopping)
            _writer.Close();

        _updateLock = false;

        TimeSpan toWait =
            (mode?.running is not true ? TimeSpan.FromSeconds(5f) : mode.genericConfig.period) -
            _timer.Elapsed;
        if(toWait.Ticks > 0)
            Thread.Sleep(toWait);

        if(!config.showFps)
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

    public void SetPowerOff() {
        WaitForLock();
        _nextMode = null;
        _changeMode = true;
        _writer.Write2((byte)DataType.Power, 0);
    }

    public void SetMode(LedMode mode) {
        WaitForLock();
        _nextMode = mode;
        _changeMode = true;
        _writer.Write2((byte)DataType.Power, 1);
    }

    private void WaitForLock() {
        while(_updateLock) { }
    }
}
