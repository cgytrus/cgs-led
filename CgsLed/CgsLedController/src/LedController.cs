using System.Diagnostics;
using System.Globalization;

namespace CgsLedController;

public class LedController {
    public record Configuration(float brightness, bool showFps);
    public Configuration config { get; set; }

    public static TimeSpan time => stopwatch.Elapsed;

    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    private bool _stopping;

    private readonly HashSet<LedMode> _modes = new();
    private readonly LedMode?[] _modeMap;
    private readonly List<Action> _schedule = new();
    private readonly object _scheduleLock = new();

    private readonly LedWriter _writer;

    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frames;
    private const float FpsFrequency = 1f;

    public LedController(Configuration config, LedWriter writer) {
        this.config = config;
        _writer = writer;
        _writer.brightness = config.brightness;
        _modeMap = new LedMode?[writer.ledCounts.Count];
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
            _writer.Write((byte)DataType.Data);
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
                _writer.Write(0, 0, 0);
        else
            mode.Draw(_writer, strip);
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
        SetMode(null);
        _stopping = true;
    }

    public void SetMode(LedMode? mode) {
        ScheduleAction(() => {
            for(int i = 0; i < _modeMap.Length; i++)
                _modeMap[i] = mode;
            UpdateModes();
        });
    }

    public void SetMode(int strip, LedMode? mode) {
        ScheduleAction(() => {
            _modeMap[strip] = mode;
            UpdateModes();
        });
    }

    public void GetModes(Action<LedMode?[]> callback) {
        ScheduleAction(() => {
            callback((LedMode?[])_modeMap.Clone());
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
            mode.Start();
        _writer.Write((byte)DataType.Power, _modes.Count == 0 ? (byte)0 : (byte)1);
        _writer.brightness = config.brightness;
    }

    private void ScheduleAction(Action action) {
        lock(_scheduleLock) {
            _schedule.Add(action);
        }
    }
}
