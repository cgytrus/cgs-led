using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedController.Service;

namespace CgsLedConsole;

internal static class Program {
    private static BinaryWriter _writer = null!;
    private static readonly CommandNode commands = new("", new CommandNode[] {
        new("start", _ => _writer.Write((byte)MessageType.Start)),
        new("stop", _ => _writer.Write((byte)MessageType.Stop)),
        new("quit", _ => _writer.Write((byte)MessageType.Quit)),
        new("off", _ => _writer.Write((byte)MessageType.SetPowerOff)),
        new("mode", args => {
            _writer.Write((byte)MessageType.SetMode);
            _writer.Write(args[0]);
            _writer.Write(args[1]);
        }),
        new("reload", _ => _writer.Write((byte)MessageType.Reload))
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
}
