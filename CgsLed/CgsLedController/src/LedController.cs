using System.Diagnostics;
using System.Globalization;

namespace CgsLedController;

public class LedController {
    public LedControllerConfig config { get; set; }
    public LedBuffer buffer { get; }

    public static TimeSpan time => stopwatch.Elapsed;

    private static readonly Stopwatch stopwatch = Stopwatch.StartNew();

    private bool _running;
    private bool _stopping;
    private Action? _onStop;

    private readonly List<LedWriter> _writers = new();
    private readonly HashSet<LedMode> _modes = new();
    private readonly LedMode?[] _modeMap;
    private readonly List<Action> _schedule = new();
    private readonly object _scheduleLock = new();

    private readonly Stopwatch _fpsTimer = Stopwatch.StartNew();
    private int _frames;
    private const float FpsFrequency = 1f;

    public LedController(LedControllerConfig config, LedBuffer buffer) {
        this.config = config;
        this.buffer = buffer;
        this.buffer.brightness = config.brightness;
        _modeMap = new LedMode?[buffer.ledCounts.Count];
    }

    public void Start() {
        _running = true;
        Thread thread = new(MainThread) {
            Name = "Led Controller Thread"
        };
        thread.Start();
    }

    private void MainThread() {
        while(_running)
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
            buffer.Write((byte)DataType.Data);
            for(int strip = 0; strip < _modeMap.Length; strip++)
                DrawStrip(strip);
        }

        _writers.RemoveAll(writer => {
            if(writer.isOpen)
                return false;
            if(writer is IDisposable disposable)
                disposable.Dispose();
            return true;
        });
        buffer.Send(_writers);
        if(_stopping) {
            _running = false;
            _onStop?.Invoke();
            _onStop = null;
        }

        if(_modes.Count == 0)
            Thread.Sleep(1000);

        UpdateFps();
    }

    private void DrawStrip(int strip) {
        LedMode? mode = _modeMap[strip];
        if(mode is null)
            for(int i = 0; i < buffer.ledCounts[strip]; i++)
                buffer.Write(0, 0, 0);
        else
            mode.Draw(buffer, strip);
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

    public void Stop(Action? onStop) {
        if(_onStop is null)
            _onStop = onStop;
        else if(onStop is not null) {
            Action prevOnStop = _onStop;
            _onStop = () => {
                prevOnStop();
                onStop();
            };
        }
        buffer.doPing = false;
        SetMode(null);
        _stopping = true;
    }

    public void AddWriter(LedWriter writer) {
        ScheduleAction(() => {
            _writers.Add(writer);
        });
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
        buffer.Write((byte)DataType.Power, _modes.Count == 0 ? (byte)0 : (byte)1);
        buffer.brightness = config.brightness;
    }

    private void ScheduleAction(Action action) {
        lock(_scheduleLock) {
            _schedule.Add(action);
        }
    }
}
