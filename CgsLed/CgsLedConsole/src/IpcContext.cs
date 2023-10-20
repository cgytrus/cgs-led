using System.Net.Sockets;

namespace CgsLedConsole;

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
