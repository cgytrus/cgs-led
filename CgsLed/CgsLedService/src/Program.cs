using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

using CgsLedController;

using CgsLedService.Helpers;
using CgsLedService.Modes.Ambilight;
using CgsLedService.Modes.Fft;
using CgsLedService.Modes.Fire;
using CgsLedService.Modes.StandBy;
using CgsLedService.Modes.Vu;
using CgsLedService.Modes.Waveform;

using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

namespace CgsLedService;

using ScreenCapture = Helpers.ScreenCapture;

internal static class Program {
    private const string PortName = "COM2";

    private static readonly string configDir =
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "", "config");
    private static readonly string modesConfigDir = Path.Combine(configDir, "modes");
    private const string MainConfigName = "main.json";
    private const string ScreenCaptureConfigName = "screen.json";

    private static bool _running = true;

    private static readonly IReadOnlyList<int> ledCounts = new int[] { 177, 82, 30 };
    private static LedController? _led;

    private static readonly IReadOnlyDictionary<string, int> aliases = new Dictionary<string, int> {
        { "window", 0 }, { "win", 0 }, { "w", 0 },
        { "door", 1 }, { "d", 1 },
        { "monitor", 2 }, { "mon", 2 }, { "m", 2 }
    };
    private static readonly IReadOnlyDictionary<int, string> inverseAliases = new Dictionary<int, string> {
        { 0, "window" },
        { 1, "door" },
        { 2, "monitor" }
    };

    private static readonly AudioCapture audioCapture = new();
    private static readonly ScreenCapture screenCapture = new(new ScreenCaptureConfig(), ledCounts);

    private static readonly IReadOnlyDictionary<string, LedMode?> modes = new Dictionary<string, LedMode?> {
        { "off", null },
        { "standby", new StandByMode() },
        { "fire", new FireMode() },
        { "fft", new FftMode(audioCapture, new FftModeConfig(new MusicColors())) },
        { "waveform", new WaveformMode(audioCapture, new WaveformModeConfig(new MusicColors())) },
        { "vu", new VuMode(audioCapture, new VuModeConfig(new MusicColors(0f, 120f, 0f, -120f))) },
        { "ambilight", new AmbilightMode(screenCapture) }
    };
    private static readonly IReadOnlyDictionary<LedMode, string> inverseModes =
        modes.Skip(1).ToDictionary(pair => pair.Value!, pair => pair.Key);

    private readonly record struct Client(TcpClient handler, NetworkStream stream, BinaryReader reader,
        BinaryWriter writer) : IDisposable {
        public void Dispose() {
            writer.Write((byte)0);
            handler.Dispose();
            stream.Dispose();
            reader.Dispose();
            writer.Dispose();
        }
    }

    private static void Main() {
        Start();
        Reload();
        SetMode("fire", "all");

        IPEndPoint ip = new(IPAddress.Loopback, 42069);
        TcpListener listener = new(ip);
        try {
            listener.Start();

            while(_running) {
                TcpClient handler = listener.AcceptTcpClient();
                NetworkStream stream = handler.GetStream();
                Client client = new() {
                    handler = handler,
                    stream = stream,
                    reader = new BinaryReader(stream, Encoding.Default),
                    writer = new BinaryWriter(stream, Encoding.Default)
                };
                try {
                    ReadMessage(client);
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

    private static void ReadMessage(Client client) {
        switch((MessageType)client.reader.ReadByte()) {
            case MessageType.Start:
                Start();
                client.Dispose();
                break;
            case MessageType.Stop:
                Stop();
                client.Dispose();
                break;
            case MessageType.Quit:
                Stop();
                _running = false;
                client.Dispose();
                break;
            case MessageType.GetRunning:
                client.writer.Write(_led is not null);
                client.Dispose();
                break;
            case MessageType.GetModes:
                GetModes(modes => {
                    client.writer.Write(modes.Count);
                    foreach((string mode, string strip) in modes) {
                        client.writer.Write(mode);
                        client.writer.Write(strip);
                    }
                    client.Dispose();
                });
                break;
            case MessageType.GetMode:
                GetMode(client.reader.ReadString(), mode => {
                    client.writer.Write(mode);
                    client.Dispose();
                });
                break;
            case MessageType.SetMode:
                SetMode(client.reader.ReadString(), client.reader.ReadString());
                client.Dispose();
                break;
            case MessageType.Reload:
                Reload();
                client.Dispose();
                break;
            case MessageType.GetConfig:
                client.writer.Write(GetConfig(client.reader.ReadString()));
                client.Dispose();
                break;
            default:
                Console.WriteLine("Unknown message");
                client.Dispose();
                break;
        }
    }

    private static void Start() {
        const int baudRate = 2000000;
        Console.WriteLine($"Starting on port {PortName} with baud rate {baudRate}");
        SerialPort port = new(PortName, baudRate, Parity.None, 8, StopBits.One);
        _led = new LedController(new LedControllerConfig(), new SerialPortLedWriter(ledCounts, port));
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
        if(!aliases.TryGetValue(strip, out int i) && !int.TryParse(strip, out i))
            return;
        Console.WriteLine($"Setting strip {i} mode to {mode.ToLower()}");
        _led.SetMode(i, ledMode);
    }

    private static void GetModes(Action<IList<(string mode, string strip)>> callback) {
        if(!CheckRunning()) {
            callback(ImmutableList<(string mode, string strip)>.Empty);
            return;
        }
        _led.GetModes(raw => {
            try {
                callback(raw.Select((mode, strip) =>
                    (mode is null ? "off" : inverseModes[mode], inverseAliases[strip])).ToList());
            }
            catch(Exception ex) {
                Console.WriteLine("Failed to read message:");
                Console.WriteLine(ex.ToString());
            }
        });
    }

    private static void GetMode(string strip, Action<string> callback) {
        if(!CheckRunning()) {
            callback(string.Empty);
            return;
        }
        _led.GetModes(raw => {
            try {
                if(!aliases.TryGetValue(strip, out int i) && !int.TryParse(strip, out i)) {
                    callback("wrong strip");
                    return;
                }
                LedMode? mode = raw[i];
                callback(mode is null ? "off" : inverseModes[mode]);
            }
            catch(Exception ex) {
                Console.WriteLine("Failed to read message:");
                Console.WriteLine(ex.ToString());
            }
        });
    }

    private static void Reload() {
        if(!CheckRunning())
            return;

        Console.WriteLine("Reloading main config");
        _led.config = ConfigFile.LoadOrSave(configDir, MainConfigName, _led.config);

        Console.WriteLine("Reloading screen capture config");
        screenCapture.config = ConfigFile.LoadOrSave(configDir, ScreenCaptureConfigName, screenCapture.config);
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
        config?.SetValue(ledMode, ConfigFile.LoadOrSave(modesConfigDir, $"{mode}.json", config.GetValue(ledMode)));
    }

    private static string GetConfig(string path) {
        switch(path) {
            case "main": return Path.Combine(configDir, MainConfigName);
            case "screen": return Path.Combine(configDir, ScreenCaptureConfigName);
            case "mode": return modesConfigDir;
        }
        if(!path.StartsWith("mode/", StringComparison.Ordinal))
            return configDir;
        string mode = path[5..];
        return modes.ContainsKey(mode) ? Path.Combine(modesConfigDir, $"{mode}.json") : modesConfigDir;
    }
}
