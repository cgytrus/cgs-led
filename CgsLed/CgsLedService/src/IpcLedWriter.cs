using CgsLedController;

namespace CgsLedService;

public sealed class IpcLedWriter : LedWriter, IDisposable {
    public override bool isOpen => _context.client.Connected;

    private readonly IpcContext _context;

    public IpcLedWriter(IpcContext context) => _context = context;

    public override void Ping(LedBuffer buffer) { }
    public override void Write(byte[] bytes, int count) {
        try { _context.writer.Write(bytes, 0, count); }
        catch(IOException) { /* ignore */ }
    }

    public void Dispose() => _context.Dispose();
}
