using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedWriter {
    public abstract bool isOpen { get; }

    public abstract void Ping(LedBuffer buffer);
    public abstract void Write(LedBuffer.LedData[] data, int count, float brightness);
}
