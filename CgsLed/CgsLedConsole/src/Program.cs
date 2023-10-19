using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedServiceTypes;

namespace CgsLedConsole;

internal static class Program {
    private static BinaryWriter _writer = null!;
    private static BinaryReader _reader = null!;
    private static readonly CommandNode commands = new("", new CommandNode[] {
        new("start", _ => _writer.Write((byte)MessageType.Start)),
        new("stop", _ => _writer.Write((byte)MessageType.Stop)),
        new("quit", _ => _writer.Write((byte)MessageType.Quit)),
        new("mode", args => {
            _writer.Write((byte)MessageType.SetMode);
            _writer.Write(args[0]);
            _writer.Write(args[1]);
        }),
        new("reload", _ => _writer.Write((byte)MessageType.Reload)),
        new("config", args => {
            _writer.Write((byte)MessageType.GetConfig);
            _writer.Write(args.Length >= 1 ? args[0] : "");
            Console.WriteLine(_reader.ReadString());
        })
    });

    private static void Main(string[] args) {
        using TcpClient client = new();
        client.Connect(new IPEndPoint(IPAddress.Loopback, 42069));
        using NetworkStream stream = client.GetStream();
        _writer = new BinaryWriter(stream, Encoding.Default);
        _reader = new BinaryReader(stream, Encoding.Default);
        commands.Run(args);
    }
}
