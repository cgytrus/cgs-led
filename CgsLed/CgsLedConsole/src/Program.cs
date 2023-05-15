using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedController;
using CgsLedController.Service;

namespace CgsLedConsole;

internal static class Program {
    private const string DefaultPortName = "COM2";
    private const int DefaultBaudRate = 1000000;

    private static void Main(string[] args) {
        IPEndPoint ip = new(IPAddress.Loopback, 42069);
        using TcpClient client = new();
        client.Connect(ip);
        using NetworkStream stream = client.GetStream();

        // TODO: jesus fucking christ
        switch(args[0]) {
            case "start": Start(stream, args.AsSpan()[1..]);
                break;
            case "stop": stream.WriteByte((byte)MessageType.Stop);
                break;
            case "controller" when args[1] == "reset": stream.WriteByte((byte)MessageType.ResetController);
                break;
            case "cfg" when args[1] == "reset": stream.WriteByte((byte)MessageType.ResetSettings);
                break;
            case "bright": {
                byte brightness = byte.Parse(args[1], CultureInfo.InvariantCulture);
                stream.WriteByte((byte)MessageType.SetBrightness);
                stream.WriteByte(brightness);
                break;
            }
            case "mode": {
                string mode = args[1];
                switch(mode) {
                    case "fft":
                        stream.WriteByte((byte)MessageType.SetFftMode);
                        break;
                    case "ambilight":
                        stream.WriteByte((byte)MessageType.SetAmbilightMode);
                        break;
                    default:
                        BuiltInMode builtInMode = Enum.Parse<BuiltInMode>(mode, true);
                        stream.WriteByte((byte)MessageType.SetMode);
                        stream.WriteByte((byte)builtInMode);
                        break;
                }
                break;
            }
            case "fft":
                switch(args[1]) {
                    case "vol": {
                        int val = int.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.Volume);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "rate": {
                        int val = int.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.Rate);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "from": {
                        int val = int.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.From);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "to": {
                        int val = int.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.To);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "noise": {
                        float val = float.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.Noise);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "mirror": {
                        bool val = bool.Parse(args[2]);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.Mirror);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "fps": {
                        bool val = bool.Parse(args[2]);
                        stream.WriteByte((byte)MessageType.SetFftConfig);
                        stream.WriteByte((byte)FftConfigType.Fps);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    default: Console.WriteLine("Unknown command");
                        break;
                }
                break;
            case "ambilight":
                switch(args[1]) {
                    case "rate": {
                        int rate = int.Parse(args[2], CultureInfo.InvariantCulture);
                        TimeSpan val = rate == 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(1f / rate);
                        stream.WriteByte((byte)MessageType.SetAmbilightConfig);
                        stream.WriteByte((byte)AmbilightConfigType.Rate);
                        stream.Write(BitConverter.GetBytes(val.Ticks));
                        break;
                    }
                    case "fps": {
                        bool val = bool.Parse(args[2]);
                        stream.WriteByte((byte)MessageType.SetAmbilightConfig);
                        stream.WriteByte((byte)AmbilightConfigType.Fps);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "screen": {
                        int val = int.Parse(args[2], CultureInfo.InvariantCulture);
                        stream.WriteByte((byte)MessageType.SetAmbilightConfig);
                        stream.WriteByte((byte)AmbilightConfigType.Screen);
                        stream.Write(BitConverter.GetBytes(val));
                        break;
                    }
                    case "window" when args.Length > 2: {
                        stream.WriteByte((byte)MessageType.SetAmbilightConfig);
                        stream.WriteByte((byte)AmbilightConfigType.Window);
                        Span<byte> bytes = stackalloc byte[256];
                        int length = Encoding.Default.GetBytes(args[2], bytes);
                        stream.Write(BitConverter.GetBytes(length));
                        stream.Write(bytes[..length]);
                        break;
                    }
                    case "window":
                        stream.WriteByte((byte)MessageType.SetAmbilightConfig);
                        stream.WriteByte((byte)AmbilightConfigType.WindowReset);
                        break;
                    default: Console.WriteLine("Unknown command");
                        break;
                }
                break;
            default: Console.WriteLine("Unknown command");
                break;
        }
    }


    private static void Start(Stream stream, Span<string> args) {
        switch(args.Length) {
            case 0: Start(stream);
                break;
            case 1: {
                if(int.TryParse(args[0], out int baudRate))
                    Start(stream, baudRate);
                else
                    Start(stream, args[0]);
                break;
            }
            default: Start(stream, args[0], int.Parse(args[1], CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void Start(Stream stream, int baudRate) => Start(stream, DefaultPortName, baudRate);
    private static void Start(Stream stream, string portName = DefaultPortName, int baudRate = DefaultBaudRate) {
        stream.WriteByte((byte)MessageType.Start);
        stream.WriteByte((byte)Array.IndexOf(SerialPort.GetPortNames(), portName));
        stream.Write(BitConverter.GetBytes(baudRate));
    }

    // ReSharper disable once UnusedMember.Local
    private static void DrawProgressBar(float step, float value) {
        Console.Write('|');
        for(float fullBar = 0f; fullBar <= 1f; fullBar += step)
            Console.Write(fullBar < value || value >= 1f ? '#' : '-');
        Console.Write('|');
    }
}
