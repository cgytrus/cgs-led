using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using CgsLedController;
using CgsLedController.Service;

using CgsLedService.Helpers;
using CgsLedService.Modes.Ambilight;
using CgsLedService.Modes.Fft;
using CgsLedService.Modes.Fire;
using CgsLedService.Modes.StandBy;
using CgsLedService.Modes.Vu;
using CgsLedService.Modes.Waveform;

namespace CgsLedService;

using ScreenCapture = Helpers.ScreenCapture;

internal static class Program {
    private const string PortName = "COM2";

    private static readonly string configDir =
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "", "config");
    private static readonly string modesConfigDir = Path.Combine(configDir, "modes");
    private const string MainConfigName = "main.json";
    private const string ScreenCaptureConfigName = "screen.json";

    private static readonly JsonSerializerOptions jsonOpts = new(JsonSerializerOptions.Default) {
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    private static bool _running = true;

    private static readonly IReadOnlyList<int> ledCounts = new int[] { 177, 82, 30 };
    private static LedController? _led;

    private static readonly AudioCapture audioCapture = new();
    private static readonly ScreenCapture screenCapture = new(new ScreenCapture.Configuration(), ledCounts);

    private static readonly IReadOnlyDictionary<string, LedMode?> modes = new Dictionary<string, LedMode?> {
        { "off", null },
        { "standby", new StandByMode() },
        { "fire", new FireMode() },
        { "fft", new FftMode(audioCapture, new FftMode.Configuration(new MusicColors())) },
        { "waveform", new WaveformMode(audioCapture, new WaveformMode.Configuration(new MusicColors())) },
        { "vu", new VuMode(audioCapture, new VuMode.Configuration(new MusicColors(0f, 120f, 0f, -120f))) },
        { "ambilight", new AmbilightMode(screenCapture) }
    };

    private static void Main(string[] args) {
        Start();
        Reload();
        SetMode("fire", "all");

        IPEndPoint ip = new(IPAddress.Loopback, 42069);
        TcpListener listener = new(ip);
        try {
            listener.Start();

            while(_running) {
                using TcpClient handler = listener.AcceptTcpClient();
                using NetworkStream networkStream = handler.GetStream();
                using BinaryReader reader = new(networkStream, Encoding.Default);
                try {
                    while(networkStream.Socket.Available < 4) { }
                    int readLength = reader.ReadInt32();
                    while(networkStream.Socket.Available < readLength) { }
                    ReadMessage(reader);
                }
                catch(Exception ex) {
                    Console.WriteLine("Failed to read message:");
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        finally {
            listener.Stop();
            _led?.Stop();
        }
    }

    private static void ReadMessage(BinaryReader reader) {
        switch((MessageType)reader.ReadByte()) {
            case MessageType.Start:
                Start();
                break;
            case MessageType.Stop:
                Stop();
                break;
            case MessageType.Quit:
                Stop();
                _running = false;
                break;
            case MessageType.SetMode:
                SetMode(reader.ReadString(), reader.ReadString());
                break;
            case MessageType.Reload:
                Reload();
                break;
            default:
                Console.WriteLine("Unknown message");
                break;
        }
    }

    private static void Start() {
        const int baudRate = 2000000;
        Console.WriteLine($"Starting on port {PortName} with baud rate {baudRate}");
        SerialPort port = new(PortName, baudRate, Parity.None, 8, StopBits.One);
        _led = new LedController(new LedController.Configuration(0.5f, false),
            new SerialPortLedWriter(ledCounts, port));
        _led.Start();
        Console.WriteLine("Ready");
    }

    private static void Stop() {
        Console.WriteLine("Stopping...");
        _led?.Stop();
        _led = null;
        Console.WriteLine("Stopped");
    }

    [MemberNotNullWhen(true, nameof(_led))]
    private static bool CheckRunning() {
        if(_led is not null)
            return true;
        Console.WriteLine("Not running");
        return false;
    }

    private static void SetMode(string mode, string strip) {
        if(!CheckRunning() || !modes.TryGetValue(mode, out LedMode? ledMode))
            return;
        if(strip == "all") {
            Console.WriteLine($"Setting mode to {mode.ToLower()}");
            _led.SetMode(ledMode);
            return;
        }
        if(!int.TryParse(strip, out int i))
            return;
        Console.WriteLine($"Setting strip {i} mode to {mode.ToLower()}");
        _led.SetMode(i, ledMode);
    }

    private static void Reload() {
        if(!CheckRunning())
            return;

        Console.WriteLine("Reloading main config");
        _led.config = LoadOrSaveConfig(configDir, MainConfigName, _led.config);

        Console.WriteLine("Reloading screen capture config");
        screenCapture.config = LoadOrSaveConfig(configDir, ScreenCaptureConfigName, screenCapture.config);
        screenCapture.Reload(ledCounts);

        foreach(string mode in modes.Keys)
            ReloadMode(mode);

        Console.WriteLine("Restarting modes");
        _led.Reload();
    }

    private static void ReloadMode(string mode) {
        if(!modes.TryGetValue(mode, out LedMode? ledMode))
            return;

        Type? type = ledMode?.GetType().BaseType;
        if(type is null || !type.IsGenericType)
            return;
        Console.WriteLine($"Reloading {mode} config");
        PropertyInfo? config = type.GetProperty(nameof(LedMode<LedMode>.config));
        config?.SetValue(ledMode, LoadOrSaveConfig(modesConfigDir, $"{mode}.json", config.GetValue(ledMode)));
    }

    private static TConfig LoadOrSaveConfig<TConfig>(string dir, string file, TConfig current) {
        string configPath = Path.Combine(dir, file);
        if(File.Exists(configPath))
            return JsonSerializer.Deserialize<TConfig>(File.ReadAllText(configPath), jsonOpts) ?? current;
        Directory.CreateDirectory(dir);
        File.WriteAllText(configPath, JsonSerializer.Serialize(current, jsonOpts));
        return current;
    }

    private static object? LoadOrSaveConfig(string dir, string file, object? current) {
        string configPath = Path.Combine(dir, file);
        if(File.Exists(configPath))
            return JsonSerializer.Deserialize(File.ReadAllText(configPath), current?.GetType() ?? typeof(object),
                jsonOpts) ?? current;
        Directory.CreateDirectory(dir);
        File.WriteAllText(configPath, JsonSerializer.Serialize(current, jsonOpts));
        return current;
    }
}
