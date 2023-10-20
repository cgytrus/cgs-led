using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedWriter {
    public abstract bool isOpen { get; }

    public abstract void Open();
    public abstract void Close();

    public abstract void Ping(LedBuffer buffer);
    public abstract void Write(byte[] bytes, int count);
}
