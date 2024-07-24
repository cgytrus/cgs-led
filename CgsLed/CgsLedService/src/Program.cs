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

using ScreenCapture.NET;

namespace CgsLedService;

using ScreenCapture = Helpers.ScreenCapture;

internal static class Program {
    private const string PortName = "COM5";
    private const int BaudRate = 12000000;

    private static readonly string configDir =
        Path.Combine(Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? "", "config");
    private static readonly string modesConfigDir = Path.Combine(configDir, "modes");
    private const string MainConfigName = "main.json";
    private const string AudioCaptureConfigName = "audio.json";
    private const string ScreenCaptureConfigName = "screen.json";

    private static bool _running = true;

    private static readonly IReadOnlyList<int> ledCounts = new int[] { 177, 82, 30 };
    private static readonly LedController led = new(new LedControllerConfig(), new LedBuffer(ledCounts));
    private static readonly SerialPortLedWriter serialWriter =
        new(new SerialPort(PortName, BaudRate, Parity.None, 8, StopBits.One));

    private static readonly IReadOnlyDictionary<int, string[]> inverseAliases = new Dictionary<int, string[]> {
        { 0, new string[] { "window", "win", "w" } },
        { 1, new string[] { "door", "d" } },
        { 2, new string[] { "monitor", "mon", "m" } }
    };
    private static readonly IReadOnlyDictionary<string, int> aliases =
        new Func<IReadOnlyDictionary<string, int>>(() => {
            Dictionary<string, int> aliases = new();
            foreach((int i, string[] values) in inverseAliases)
                foreach(string alias in values)
                    aliases.Add(alias, i);
            return aliases;
        })();

    private static readonly AudioCapture audioCapture = new(new AudioCaptureConfig());
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

    private static readonly IReadOnlyDictionary<MessageType, Action<IpcContext>> handlers =
        new Dictionary<MessageType, Action<IpcContext>> {
            { MessageType.Quit, context => {
                _running = false;
                context.Dispose();
            } },
            { MessageType.GetStrips, context => {
                context.writer.Write(ledCounts.Count);
                for(int i = 0; i < ledCounts.Count; i++) {
                    if(!inverseAliases.TryGetValue(i, out string[]? aliases))
                        aliases = Array.Empty<string>();
                    context.writer.Write(aliases.Length);
                    foreach(string alias in aliases)
                        context.writer.Write(alias);
                    context.writer.Write(ledCounts[i]);
                }
                context.Dispose();
            } },
            { MessageType.GetModes, context => {
                GetModes(modes => {
                    try {
                        context.writer.Write(modes.Count);
                        foreach((string mode, string strip) in modes) {
                            context.writer.Write(mode);
                            context.writer.Write(strip);
                        }
                    }
                    catch(Exception ex) {
                        Console.WriteLine("Failed to read message:");
                        Console.WriteLine(ex.ToString());
                    }
                    context.Dispose();
                });
            } },
            { MessageType.GetMode, context => {
                GetMode(context.reader.ReadString(), mode => {
                    try {
                        context.writer.Write(mode);
                    }
                    catch(Exception ex) {
                        Console.WriteLine("Failed to read message:");
                        Console.WriteLine(ex.ToString());
                    }
                    context.Dispose();
                });
            } },
            { MessageType.SetMode, context => {
                SetMode(context.reader.ReadString(), context.reader.ReadString());
                context.Dispose();
            } },
            { MessageType.SetFreddy, context => {
                led.SetFreddy();
                context.Dispose();
            } },
            { MessageType.Reload, context => {
                Reload();
                context.Dispose();
            } },
            { MessageType.GetConfig, context => {
                context.writer.Write(GetConfig(context.reader.ReadString()));
                context.Dispose();
            } },
            { MessageType.GetScreens, context => {
                context.writer.Write(screenCapture.screenCaptures.Count);
                foreach(IScreenCapture capture in screenCapture.screenCaptures)
                    context.writer.Write(capture.Display.DeviceName);
                context.Dispose();
            } },
            { MessageType.StreamLeds, context => {
                led.AddWriter(new IpcLedWriter(context));
            } }
        };

    private static void Main() {
        Console.WriteLine($"Starting on port {PortName} with baud rate {BaudRate}");
        led.AddWriter(serialWriter);
        serialWriter.Open();
        while(!serialWriter.isOpen)
            Thread.Sleep(10);
        led.Start();
        Console.WriteLine("Ready");

        Reload();
        SetMode("fire", "all");

        TcpListener listener = new(new IPEndPoint(IPAddress.Loopback, 42069));
        try {
            listener.Start();

            while(_running) {
                TcpClient client = listener.AcceptTcpClient();
                NetworkStream stream = client.GetStream();
                IpcContext context = new() {
                    client = client,
                    stream = stream,
                    reader = new BinaryReader(stream, Encoding.Default),
                    writer = new BinaryWriter(stream, Encoding.Default)
                };
                try {
                    if(!handlers.TryGetValue((MessageType)context.reader.ReadByte(), out Action<IpcContext>? handler)) {
                        Console.WriteLine("Unknown message");
                        context.Dispose();
                        return;
                    }
                    handler(context);
                }
                catch(Exception ex) {
                    Console.WriteLine("Failed to read message:");
                    Console.WriteLine(ex.ToString());
                    context.Dispose();
                }
            }
        }
        finally {
            Console.WriteLine("Stopping...");
            listener.Stop();
            led.Stop(() => {
                serialWriter.Close();
                while(serialWriter.isOpen)
                    Thread.Sleep(10);
                Console.WriteLine("Stopped");
            });
        }
    }

    private static void SetMode(string mode, string strip) {
        if(!modes.TryGetValue(mode, out LedMode? ledMode))
            return;
        if(strip == "all") {
            Console.WriteLine($"Setting mode to {mode.ToLower()}");
            led.SetMode(ledMode);
            return;
        }
        if(!aliases.TryGetValue(strip, out int i) && !int.TryParse(strip, out i))
            return;
        Console.WriteLine($"Setting strip {i} mode to {mode.ToLower()}");
        led.SetMode(i, ledMode);
    }

    private static void GetModes(Action<IList<(string mode, string strip)>> callback) {
        led.GetModes(raw => {
            callback(raw.Select((mode, strip) =>
                (mode is null ? "off" : inverseModes[mode], inverseAliases[strip][0])).ToList());
        });
    }

    private static void GetMode(string strip, Action<string> callback) {
        led.GetModes(raw => {
            if(!aliases.TryGetValue(strip, out int i) && !int.TryParse(strip, out i)) {
                callback("wrong strip");
                return;
            }
            LedMode? mode = raw[i];
            callback(mode is null ? "off" : inverseModes[mode]);
        });
    }

    private static void Reload() {
        Console.WriteLine("Reloading main config");
        led.config = ConfigFile.LoadOrSave(configDir, MainConfigName, led.config);

        Console.WriteLine("Reloading audio capture config");
        audioCapture.config = ConfigFile.LoadOrSave(configDir, AudioCaptureConfigName, audioCapture.config);

        Console.WriteLine("Reloading screen capture config");
        screenCapture.config = ConfigFile.LoadOrSave(configDir, ScreenCaptureConfigName, screenCapture.config);
        screenCapture.Reload(ledCounts);

        foreach(string mode in modes.Keys)
            ReloadMode(mode);

        Console.WriteLine("Restarting modes");
        led.Reload();
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
