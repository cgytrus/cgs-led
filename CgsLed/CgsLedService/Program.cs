using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
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
    private const string DefaultPortName = "COM2";
    private const int DefaultBaudRate = 2000000;

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

    private static readonly IReadOnlyDictionary<string, LedMode> modes = new Dictionary<string, LedMode> {
        { "standby", new StandByMode(new LedMode.Configuration(TimeSpan.Zero)) },
        { "fire", new FireMode(new LedMode.Configuration(TimeSpan.Zero)) },
        { "fft", new FftMode(new FftMode.Configuration(TimeSpan.Zero, 100f / 8f, "Signal", true, new MusicColors())) },
        { "waveform", new WaveformMode(new WaveformMode.Configuration(TimeSpan.Zero, 100f / 50f, "Signal", true, new MusicColors())) },
        {
            "vu",
            new VuMode(new VuMode.Configuration(TimeSpan.Zero, 100f / 50f, "Signal", true,
                new MusicColors(hueSpeed: 0f, hueOffset: 120f, hueRange: -120f)))
        },
        { "ambilight", new AmbilightMode(new AmbilightMode.Configuration(TimeSpan.Zero)) }
    };

    private static void Main(string[] args) {
        Start();
        ReloadConfig();
        foreach(string mode in modes.Keys)
            ReloadModeConfig(mode);
        SetMode("fire");

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
                Start(SerialPort.GetPortNames()[reader.ReadByte()], reader.ReadInt32());
                break;
            case MessageType.Stop:
                Stop();
                break;
            case MessageType.Quit:
                Stop();
                _running = false;
                break;
            case MessageType.SetPowerOff:
                SetPowerOff();
                break;
            case MessageType.SetMode:
                SetMode(reader.ReadString());
                break;
            case MessageType.ReloadConfig:
                ReloadConfig();
                break;
            case MessageType.ReloadModeConfig:
                ReloadModeConfig(reader.ReadString());
                break;
            default:
                Console.WriteLine("Unknown message");
                break;
        }
    }

    private static void Start(string portName = DefaultPortName, int baudRate = DefaultBaudRate) {
        Console.WriteLine($"Starting on port {portName} with baud rate {baudRate}");
        SerialPort port = new(portName, baudRate, Parity.None, 8, StopBits.One);
        _led = new LedController(new LedController.Configuration(0.25f, false), port, new int[] { 177, 82, 30 });
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

    private static void SetPowerOff() {
        if(!CheckRunning())
            return;
        Console.WriteLine("Powering off");
        _led.SetPowerOff();
    }

    private static void SetMode(string mode) {
        if(!CheckRunning() || !modes.TryGetValue(mode, out LedMode? ledMode))
            return;
        Console.WriteLine($"Setting mode to {mode.ToLower()}");
        _led.SetMode(ledMode);
    }

    private static void ReloadConfig() {
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
    }

    private static void ReloadModeConfig(string mode) {
        if(!modes.TryGetValue(mode, out LedMode? ledMode))
            return;
        Console.WriteLine($"Reloading {mode} config");
        string configPath = Path.Combine(modesConfigDir, $"{mode}.json");
        if(File.Exists(configPath)) {
            ledMode.genericConfig =
                (LedMode.Configuration?)JsonSerializer.Deserialize(File.ReadAllText(configPath), ledMode.configType,
                    jsonOpts) ?? ledMode.genericConfig;
            if(_led is not null && _led.mode == ledMode)
                _led.SetMode(ledMode);
        }
        else {
            Directory.CreateDirectory(modesConfigDir);
            File.WriteAllText(configPath,
                JsonSerializer.Serialize(ledMode.genericConfig, ledMode.configType, jsonOpts));
        }
    }
}
