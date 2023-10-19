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

    public readonly record struct Client(TcpClient handler, NetworkStream stream, BinaryReader reader,
        BinaryWriter writer) : IDisposable {
        public void Dispose() {
            reader.Read();
            handler.Dispose();
            stream.Dispose();
            reader.Dispose();
            writer.Dispose();
        }
    }

    public static Client GetClient() {
        TcpClient handler = new();
        handler.Connect(new IPEndPoint(IPAddress.Loopback, 42069));
        NetworkStream stream = handler.GetStream();
        return new Client {
            handler = handler,
            stream = stream,
            reader = new BinaryReader(stream, Encoding.Default),
            writer = new BinaryWriter(stream, Encoding.Default)
        };
    }
}
