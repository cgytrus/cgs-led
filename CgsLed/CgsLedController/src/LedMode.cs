using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedMode {
    private const int HeaderSize = 1;

    public record Configuration(TimeSpan period);

    public virtual Type configType => typeof(Configuration);
    public Configuration genericConfig { get; set; }

    protected LedMode(Configuration config) => genericConfig = config;

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

[PublicAPI]
public abstract class LedMode<TConfig> : LedMode where TConfig : LedMode.Configuration {
    public override Type configType => typeof(TConfig);
    public TConfig config {
        get => (TConfig)genericConfig;
        set => genericConfig = value;
    }

    protected LedMode(TConfig config) : base(config) { }
}
