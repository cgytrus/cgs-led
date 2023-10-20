using System.Net.Sockets;

namespace CgsLedService;

public readonly record struct IpcContext(TcpClient client, NetworkStream stream, BinaryReader reader,
    BinaryWriter writer) : IDisposable {
    public void Dispose() {
        if(client.Connected)
            writer.Write((byte)0);
        client.Dispose();
        stream.Dispose();
        reader.Dispose();
        writer.Dispose();
    }
}
