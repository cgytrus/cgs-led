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

using CgsLedService.Modes.Ambilight;
using CgsLedService.Modes.Fire;
using CgsLedService.Modes.Music;
using CgsLedService.Modes.Music.Fft;
using CgsLedService.Modes.Music.Vu;
using CgsLedService.Modes.Music.Waveform;
using CgsLedService.Modes.StandBy;

namespace CgsLedService;

internal static class Program {
    private const string PortName = "COM2";

    private static readonly string configDir =
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "", "config");
    private static readonly string modesConfigDir = Path.Combine(configDir, "modes");
    private const string ConfigName = "main.json";

    private static readonly JsonSerializerOptions jsonOpts = new(JsonSerializerOptions.Default) {
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true
    };

    private static bool _running = true;

    private static LedController? _led;

    private static readonly MusicConfig musicConfig = new(null, false, new MusicColors());
    private static readonly IReadOnlyDictionary<string, LedMode?> modes = new Dictionary<string, LedMode?> {
        { "off", null },
        { "standby", new StandByMode() },
        { "fire", new FireMode() },
        { "fft", new FftMode(new FftMode.Configuration(musicConfig)) },
        { "waveform", new WaveformMode(new WaveformMode.Configuration(musicConfig)) },
        {
            "vu",
            new VuMode(new VuMode.Configuration(musicConfig with { colors = new MusicColors(0f, 120f, 0f, -120f) }))
        },
        { "ambilight", new AmbilightMode(new AmbilightMode.Configuration()) }
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
            new SerialPortLedWriter(new int[] { 177, 82, 30 }, port));
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
        string configPath = Path.Combine(configDir, ConfigName);
        if(File.Exists(configPath))
            _led.config =
                JsonSerializer.Deserialize<LedController.Configuration>(File.ReadAllText(configPath), jsonOpts) ??
                _led.config;
        else {
            Directory.CreateDirectory(configDir);
            File.WriteAllText(configPath, JsonSerializer.Serialize(_led.config, jsonOpts));
        }
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

        Type configType = type.GetGenericArguments()[0];
        PropertyInfo? config = type.GetProperty(nameof(LedMode<LedMode>.config));
        if(config is null)
            return;

        string configPath = Path.Combine(modesConfigDir, $"{mode}.json");
        if(File.Exists(configPath))
            config.SetValue(ledMode, JsonSerializer.Deserialize(File.ReadAllText(configPath), configType, jsonOpts));
        else {
            Directory.CreateDirectory(modesConfigDir);
            File.WriteAllText(configPath, JsonSerializer.Serialize(config.GetValue(ledMode), configType, jsonOpts));
        }
    }
}
