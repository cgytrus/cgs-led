using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.UI.Xaml;

namespace CgsLedGui;

public partial class App {
    private Window? _window;

    public App() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
        _window = new MainWindow();
        _window.Activate();
    }

    public readonly record struct IpcContext(TcpClient client, NetworkStream stream, BinaryReader reader,
        BinaryWriter writer) : IDisposable {
        public void Dispose() {
            if(client.Connected)
                reader.Read();
            client.Dispose();
            stream.Dispose();
            reader.Dispose();
            writer.Dispose();
        }

        public void Flush() {
            Span<byte> bytes = stackalloc byte[Math.Min(stream.Socket.Available, 256)];
            while(stream.DataAvailable) {
                _ = stream.Read(bytes);
            }
        }
    }

    public static IpcContext GetIpc() {
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
