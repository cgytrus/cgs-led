using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedServiceTypes;

namespace CgsLedConsole;

internal static class Program {
    private static readonly CommandNode commands = new("", new CommandNode[] {
        new("quit", _ => {
            using IpcContext context = GetIpc();
            context.writer.Write((byte)MessageType.Quit);
        }),
        new("strips", _ => {
            using IpcContext context = GetIpc();
            context.writer.Write((byte)MessageType.GetStrips);
            int count = context.reader.ReadInt32();
            for(int i = 0; i < count; i++) {
                Console.Write(i);
                Console.Write(' ');
                int aliasCount = context.reader.ReadInt32();
                for(int j = 0; j < aliasCount; j++) {
                    Console.Write(context.reader.ReadString());
                    Console.Write(' ');
                }
                Console.WriteLine(context.reader.ReadInt32());
            }
            if(count == 0)
                Console.WriteLine("no strips");
        }),
        new("mode", args => {
            using IpcContext context = GetIpc();
            switch(args.Length) {
                case 0:
                case 1 when args[0] == "all":
                    context.writer.Write((byte)MessageType.GetModes);
                    int count = context.reader.ReadInt32();
                    for(int i = 0; i < count; i++) {
                        string mode = context.reader.ReadString();
                        string strip = context.reader.ReadString();
                        Console.WriteLine($"{strip} {mode}");
                    }
                    if(count == 0)
                        Console.WriteLine("no strips");
                    break;
                case 1:
                    context.writer.Write((byte)MessageType.GetMode);
                    context.writer.Write(args[0]);
                    Console.WriteLine(context.reader.ReadString());
                    break;
                default:
                    context.writer.Write((byte)MessageType.SetMode);
                    context.writer.Write(args[0]);
                    context.writer.Write(args[1]);
                    break;
            }
        }),
        new("reload", _ => {
            using IpcContext context = GetIpc();
            context.writer.Write((byte)MessageType.Reload);
        }),
        new("config", args => {
            {
                using IpcContext context = GetIpc();
                context.writer.Write((byte)MessageType.GetConfig);
                context.writer.Write(args.Length >= 1 ? args[0] : "");
                string path = context.reader.ReadString();
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
            }
            {
                using IpcContext context = GetIpc();
                context.writer.Write((byte)MessageType.Reload);
            }
        }),
        new("screens", _ => {
            using IpcContext context = GetIpc();
            context.writer.Write((byte)MessageType.GetScreens);
            int count = context.reader.ReadInt32();
            for(int i = 0; i < count; i++) {
                string name = context.reader.ReadString();
                Console.WriteLine($"{i} {name}");
            }
            if(count == 0)
                Console.WriteLine("no screens");
        }),
        new("watch", _ => {
            int[] ledCounts;
            string[] aliases;
            {
                using IpcContext context = GetIpc();
                context.writer.Write((byte)MessageType.GetStrips);
                ledCounts = new int[context.reader.ReadInt32()];
                aliases = new string[ledCounts.Length];
                for(int i = 0; i < ledCounts.Length; i++) {
                    int aliasCount = context.reader.ReadInt32();
                    for(int j = 0; j < aliasCount; j++) {
                        aliases[i] = context.reader.ReadString();
                    }
                    ledCounts[i] = context.reader.ReadInt32();
                }
            }
            {
                using IpcContext context = GetIpc();
                context.writer.Write((byte)MessageType.StreamLeds);
                while(true) {
                    byte dataType = context.reader.ReadByte();
                    if(dataType != 1) {
                        context.Flush();
                        continue;
                    }
                    (int left, int top) = Console.GetCursorPosition();
                    for(int strip = 0; strip < ledCounts.Length; strip++) {
                        int ledCount = ledCounts[strip];
                        Console.Write(aliases[strip]);
                        Console.Write(' ');
                        for(int i = 0; i < ledCount; i++) {
                            (int rl, int rt) = Console.GetCursorPosition();
                            Console.Write("       ");
                            Console.SetCursorPosition(rl, rt);
                            Console.Write(context.reader.ReadByte().ToString("x2"));
                            Console.Write(context.reader.ReadByte().ToString("x2"));
                            Console.Write(context.reader.ReadByte().ToString("x2"));
                            Console.Write(' ');
                        }
                        Console.WriteLine();
                    }
                    Console.SetCursorPosition(left, top);
                }
            }
        })
    });

    private static void Main(string[] args) => commands.Run(args);

    private static IpcContext GetIpc() {
        TcpClient client = new();
        client.Connect(new IPEndPoint(IPAddress.Loopback, 42069));
        NetworkStream stream = client.GetStream();
        return new IpcContext {
            client = client,
            stream = stream,
            reader = new BinaryReader(stream, Encoding.Default),
            writer = new BinaryWriter(stream, Encoding.Default)
        };
    }
}
