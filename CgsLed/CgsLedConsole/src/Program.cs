using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedController.Service;

namespace CgsLedConsole;

internal static class Program {
    private const string DefaultPortName = "COM2";
    private const int DefaultBaudRate = 2000000;

    private static BinaryWriter _writer = null!;
    private static readonly CommandNode commands = new("", new CommandNode[] {
        new("start", args => {
            switch(args.Length) {
                case 0: Start();
                    break;
                case 1: {
                    if(int.TryParse(args[0], out int baudRate))
                        Start(baudRate);
                    else
                        Start(args[0]);
                    break;
                }
                default: Start(args[0], int.Parse(args[1], CultureInfo.InvariantCulture));
                    break;
            }
        }),
        new("stop", _ => _writer.Write((byte)MessageType.Stop)),
        new("quit", _ => _writer.Write((byte)MessageType.Quit)),
        new("off", _ => _writer.Write((byte)MessageType.SetPowerOff)),
        new("mode", args => {
            _writer.Write((byte)MessageType.SetMode);
            _writer.Write(args[0]);
        }),
        new("cfg", new CommandNode[] {
            new("svc", _ => _writer.Write((byte)MessageType.ReloadConfig)),
            new("mode", args => {
                _writer.Write((byte)MessageType.ReloadModeConfig);
                _writer.Write(args[0]);
            })
        })
    });

    private static void Main(string[] args) {
        using MemoryStream stream = new(1024);
        using BinaryWriter writer = new(stream, Encoding.Default);
        stream.Position = sizeof(int);
        _writer = writer;
        commands.Run(args);
        _writer = null!;
        stream.Position = 0;
        writer.Write((int)stream.Length - sizeof(int));

        IPEndPoint ip = new(IPAddress.Loopback, 42069);
        using TcpClient client = new();
        client.Connect(ip);
        using NetworkStream networkStream = client.GetStream();
        stream.WriteTo(networkStream);
    }

    private static void Start(int baudRate) => Start(DefaultPortName, baudRate);
    private static void Start(string portName = DefaultPortName, int baudRate = DefaultBaudRate) {
        _writer.Write((byte)MessageType.Start);
        _writer.Write((byte)Array.IndexOf(SerialPort.GetPortNames(), portName));
        _writer.Write(baudRate);
    }
}
