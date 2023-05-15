using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;

using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class CustomMode {
    private const int HeaderSize = 1;

    public int[] ledCounts { get; init; } = Array.Empty<int>();
    public TimeSpan period { get; set; } = TimeSpan.FromSeconds(1f / 20f);
    public bool showFps { get; set; }

    protected abstract DataType dataType { get; }
    protected abstract bool running { get; }
    public bool stopped => _done;

    protected int totalLedCount { get; private set; }
    protected int totalHalfLedCount { get; private set; }
    protected int[] ledStarts { get; private set; } = Array.Empty<int>();
    protected int[] halfLedCounts { get; private set; } = Array.Empty<int>();
    protected int[] halfLedStarts { get; private set; } = Array.Empty<int>();

    private SerialPort _port = null!;
    private object _portLock = null!;

    private bool _frameReady;

    private readonly byte[] _ledData = new byte[1024];
    private int _totalDataCount;

    private bool _done = true;

    public void Start(SerialPort port, object portLock) {
        _port = port;
        _portLock = portLock;

        halfLedCounts = ledCounts.Select(c => c % 2 == 0 ? c / 2 : c / 2 + 1).ToArray();
        totalLedCount = 0;
        totalHalfLedCount = 0;
        ledStarts = new int[ledCounts.Length];
        halfLedStarts = new int[ledCounts.Length];
        for(int i = 0; i < ledCounts.Length; i++) {
            ledStarts[i] = totalLedCount;
            halfLedStarts[i] = totalHalfLedCount;
            totalLedCount += ledCounts[i];
            totalHalfLedCount += halfLedCounts[i];
        }

        Thread mainThread = new(MainThread);
        mainThread.Start();
    }

    public virtual void StopMode() {
        while(!_done) { }
    }

    private void MainThread() {
        StopMode();
        _done = false;

        Main();
        while(!running) { }

        Stopwatch timer = Stopwatch.StartNew();
        Stopwatch fpsTimer = Stopwatch.StartNew();
        Stopwatch notReadyTimer = Stopwatch.StartNew();
        int frames = 0;
        const int framesPerMeasure = 60;

        while(running) {
            timer.Restart();

            bool frameRan = TryRunFrame(notReadyTimer);

            TimeSpan toWait = period - timer.Elapsed;
            if(toWait.Ticks > 0)
                Thread.Sleep(toWait);

            if(!showFps || !frameRan)
                continue;
            frames++;

            if(frames < framesPerMeasure)
                continue;
            Console.WriteLine((frames / fpsTimer.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            fpsTimer.Restart();
            frames = 0;
        }

        MainEnd();
        _done = true;
    }

    protected abstract void Main();

    protected virtual void MainEnd() { }

    private bool TryRunFrame(Stopwatch notReadyTimer) {
        if(!_frameReady) {
            DataType dataType = this.dataType;
            Debug.Assert(dataType is DataType.RawData or DataType.FftData or DataType.FftMirroredData,
                "Illegal data type");

            _ledData[0] = (byte)dataType;
            _totalDataCount = HeaderSize;
            Frame();
            _frameReady = true;
            notReadyTimer.Restart();

            Debug.Assert(_totalDataCount == dataType switch {
                DataType.RawData => HeaderSize + totalLedCount * 3,
                DataType.FftData => HeaderSize + totalLedCount,
                DataType.FftMirroredData => HeaderSize + halfLedCounts.Sum(),
                _ => _totalDataCount
            }, "Wrong data count");
        }

        if(_totalDataCount <= 0)
            return false;

        bool forceReady = notReadyTimer.Elapsed.TotalSeconds >= 1d;
        bool ready = _port.BytesToRead > 0 && _port.ReadByte() == 0 || forceReady;

        // ReSharper disable once InvertIf
        if(ready) {
            if(forceReady) {
                Console.WriteLine("The controller didn't respond within a second after the last frame,");
                Console.WriteLine("forcing another frame to hopefully wake it up");
            }

            lock(_portLock) {
                _port.Write(_ledData, 0, _totalDataCount);
            }
            _totalDataCount = 0;
            _frameReady = false;
        }
#if DEBUG
        else if(period.Ticks > 0)
            LogLaggedFrame();
#endif

        return ready;
    }

    protected virtual void Frame() { }

#if DEBUG
    private static void LogLaggedFrame() {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Warning! Lagged frame");
        Console.ForegroundColor = ConsoleColor.Gray;
    }
#endif

    protected void Set1(byte value) {
        _ledData[_totalDataCount] = value;
        _totalDataCount++;
    }

    protected void Set3(byte a, byte b, byte c) {
        _ledData[_totalDataCount] = a;
        _ledData[_totalDataCount + 1] = b;
        _ledData[_totalDataCount + 2] = c;
        _totalDataCount += 3;
    }

    // 2.8
    /*private static readonly byte[] gamma8 = {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
        2, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5,
        5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10,
        10, 10, 11, 11, 11, 12, 12, 13, 13, 13, 14, 14, 15, 15, 16, 16,
        17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 24, 24, 25,
        25, 26, 27, 27, 28, 29, 29, 30, 31, 32, 32, 33, 34, 35, 35, 36,
        37, 38, 39, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 50,
        51, 52, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68,
        69, 70, 72, 73, 74, 75, 77, 78, 79, 81, 82, 83, 85, 86, 87, 89,
        90, 92, 93, 95, 96, 98, 99, 101, 102, 104, 105, 107, 109, 110, 112, 114,
        115, 117, 119, 120, 122, 124, 126, 127, 129, 131, 133, 135, 137, 138, 140, 142,
        144, 146, 148, 150, 152, 154, 156, 158, 160, 162, 164, 167, 169, 171, 173, 175,
        177, 180, 182, 184, 186, 189, 191, 193, 196, 198, 200, 203, 205, 208, 210, 213,
        215, 218, 220, 223, 225, 228, 231, 233, 236, 239, 241, 244, 247, 249, 252, 255
    };*/
    // 2.2
    private static readonly byte[] gamma8 = {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6,
        6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 11, 11, 11, 12,
        12, 13, 13, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18, 19, 19,
        20, 20, 21, 22, 22, 23, 23, 24, 25, 25, 26, 26, 27, 28, 28, 29,
        30, 30, 31, 32, 33, 33, 34, 35, 35, 36, 37, 38, 39, 39, 40, 41,
        42, 43, 43, 44, 45, 46, 47, 48, 49, 49, 50, 51, 52, 53, 54, 55,
        56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71,
        73, 74, 75, 76, 77, 78, 79, 81, 82, 83, 84, 85, 87, 88, 89, 90,
        91, 93, 94, 95, 97, 98, 99, 100, 102, 103, 105, 106, 107, 109, 110, 111,
        113, 114, 116, 117, 119, 120, 121, 123, 124, 126, 127, 129, 130, 132, 133, 135,
        137, 138, 140, 141, 143, 145, 146, 148, 149, 151, 153, 154, 156, 158, 159, 161,
        163, 165, 166, 168, 170, 172, 173, 175, 177, 179, 181, 182, 184, 186, 188, 190,
        192, 194, 196, 197, 199, 201, 203, 205, 207, 209, 211, 213, 215, 217, 219, 221,
        223, 225, 227, 229, 231, 234, 236, 238, 240, 242, 244, 246, 248, 251, 253, 255
    };
    protected void SetColor(byte r, byte g, byte b) {
        Set3(gamma8[r], gamma8[g], gamma8[b]);
    }
}
