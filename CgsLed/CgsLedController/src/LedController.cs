using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

namespace CgsLedController;

public class LedController {
    public record Configuration(float brightness, bool showFps);
    public Configuration config { get; set; }

    private bool _stopping;

    private readonly HashSet<LedMode> _modes = new();
    private readonly LedMode?[] _modeMap;
    private readonly List<Action> _schedule = new();
    private readonly object _scheduleLock = new();

    private readonly LedWriter _writer;

    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frames;
    private const float FpsFrequency = 1f;

    public LedController(Configuration config, SerialPort port, IReadOnlyList<int> ledCounts) {
        this.config = config;
        _writer = new LedWriter(this, port, ledCounts);
        _modeMap = new LedMode?[ledCounts.Count];
    }

    public void Start() {
        _writer.Open();
        Thread thread = new(MainThread);
        thread.Start();
    }

    private void MainThread() {
        while(_writer.isOpen)
            Update();
        foreach(LedMode mode in _modes)
            mode.StopMode();
        _stopping = false;
    }

    private void Update() {
        lock(_scheduleLock) {
            foreach(Action action in _schedule)
                action();
            _schedule.Clear();
        }

        if(_modes.Count != 0) {
            foreach(LedMode mode in _modes)
                mode.Update();
            _writer.Write1((byte)DataType.Data);
            for(int strip = 0; strip < _modeMap.Length; strip++)
                DrawStrip(strip);
        }

        _writer.Send();
        if(_stopping)
            _writer.Close();

        if(_modes.Count == 0)
            Thread.Sleep(1000);

        UpdateFps();
    }

    private void DrawStrip(int strip) {
        LedMode? mode = _modeMap[strip];
        if(mode is null)
            for(int i = 0; i < _writer.ledCounts[strip]; i++)
                _writer.Write3(0, 0, 0);
        else
            mode.Draw(strip);
    }

    private void UpdateFps() {
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
        _writer.doPing = false;
        SetPowerOff();
        _stopping = true;
    }

    public void SetPowerOff() {
        ScheduleAction(() => {
            for(int i = 0; i < _modeMap.Length; i++)
                _modeMap[i] = null;
            UpdateModes();
            _writer.Write2((byte)DataType.Power, 0);
        });
    }

    public void SetMode(LedMode mode) {
        ScheduleAction(() => {
            for(int i = 0; i < _modeMap.Length; i++)
                _modeMap[i] = mode;
            UpdateModes();
            _writer.Write2((byte)DataType.Power, 1);
        });
    }

    public void SetMode(int strip, LedMode mode) {
        ScheduleAction(() => {
            _modeMap[strip] = mode;
            UpdateModes();
            _writer.Write2((byte)DataType.Power, 1);
        });
    }

    public void Reload() {
        ScheduleAction(UpdateModes);
    }

    private void UpdateModes() {
        foreach(LedMode mode in _modes)
            mode.StopMode();
        _modes.Clear();
        foreach(LedMode? mode in _modeMap)
            if(mode is not null)
                _modes.Add(mode);
        foreach(LedMode mode in _modes)
            mode.Start(_writer);
    }

    private void ScheduleAction(Action action) {
        lock(_scheduleLock) {
            _schedule.Add(action);
        }
    }
}
