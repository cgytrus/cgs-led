using CgsLedController;

namespace CgsLedService;

public sealed class IpcLedWriter : LedWriter, IDisposable {
    public override bool isOpen => _context.client.Connected;

    private readonly IpcContext _context;

    public IpcLedWriter(IpcContext context) => _context = context;

    public override void Ping(LedBuffer buffer) { }
    public override void Write(LedBuffer.LedData[] data, int count, float brightness) {
        Span<byte> bytes = stackalloc byte[count];
        for(int i = 0; i < count; i++)
            bytes[i] = data[i].data;
        try { _context.writer.Write(bytes); }
        catch(IOException) { /* ignore */ }
    }

    public void Dispose() => _context.Dispose();
}
