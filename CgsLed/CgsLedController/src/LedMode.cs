using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedMode {
    private const int HeaderSize = 1;

    public TimeSpan period { get; set; } = TimeSpan.FromSeconds(1f / 20f);

    public abstract bool running { get; }

    protected LedWriter writer { get; private set; } = null!;

    public void Start(LedWriter writer) {
        this.writer = writer;
        Main();
    }

    public abstract void StopMode();

    public void Update() {
        writer.Write1((byte)DataType.RawData);
        Frame();
    }

    protected abstract void Main();

    protected abstract void Frame();
}
