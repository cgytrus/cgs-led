﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedServiceTypes;

namespace CgsLedConsole;

internal static class Program {
    private static BinaryWriter _writer = null!;
    private static BinaryReader _reader = null!;
    private static readonly CommandNode commands = new("", new CommandNode[] {
        new("quit", _ => _writer.Write((byte)MessageType.Quit)),
        new("strips", _ => {
            _writer.Write((byte)MessageType.GetStrips);
            int count = _reader.ReadInt32();
            for(int i = 0; i < count; i++) {
                Console.Write(i);
                Console.Write(' ');
                int aliasCount = _reader.ReadInt32();
                for(int j = 0; j < aliasCount; j++) {
                    Console.Write(_reader.ReadString());
                    Console.Write(' ');
                }
                Console.WriteLine(_reader.ReadInt32());
            }
            if(count == 0)
                Console.WriteLine("no strips");
        }),
        new("mode", args => {
            switch(args.Length) {
                case 0:
                case 1 when args[0] == "all":
                    _writer.Write((byte)MessageType.GetModes);
                    int count = _reader.ReadInt32();
                    for(int i = 0; i < count; i++) {
                        string mode = _reader.ReadString();
                        string strip = _reader.ReadString();
                        Console.WriteLine($"{strip} {mode}");
                    }
                    if(count == 0)
                        Console.WriteLine("no strips");
                    break;
                case 1:
                    _writer.Write((byte)MessageType.GetMode);
                    _writer.Write(args[0]);
                    Console.WriteLine(_reader.ReadString());
                    break;
                default:
                    _writer.Write((byte)MessageType.SetMode);
                    _writer.Write(args[0]);
                    _writer.Write(args[1]);
                    break;
            }
        }),
        new("reload", _ => _writer.Write((byte)MessageType.Reload)),
        new("config", args => {
            _writer.Write((byte)MessageType.GetConfig);
            _writer.Write(args.Length >= 1 ? args[0] : "");
            string path = _reader.ReadString();
            if(!File.Exists(path)) {
                Console.WriteLine($"invalid config file path: {path}");
                return;
            }
            Process process = new() {
                StartInfo = new ProcessStartInfo {
                    UseShellExecute = true,
                    FileName = path
                }
            };
            process.Start();
            process.WaitForExit();
            Main(new[] { "reload" });
        }),
        new("screens", _ => {
            _writer.Write((byte)MessageType.GetScreens);
            int count = _reader.ReadInt32();
            for(int i = 0; i < count; i++) {
                string name = _reader.ReadString();
                Console.WriteLine($"{i} {name}");
            }
            if(count == 0)
                Console.WriteLine("no screens");
        })
    });

    private static void Main(string[] args) {
        using TcpClient client = new();
        client.Connect(new IPEndPoint(IPAddress.Loopback, 42069));
        using NetworkStream stream = client.GetStream();
        _writer = new BinaryWriter(stream, Encoding.Default);
        _reader = new BinaryReader(stream, Encoding.Default);
        commands.Run(args);
        _reader.Read();
    }
}
