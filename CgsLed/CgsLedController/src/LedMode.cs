using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedMode {
    protected static TimeSpan time => LedController.time;

    public virtual void Start() { }
    public virtual void StopMode() { }

    public virtual void Update() { }
    public abstract void Draw(LedWriter writer, int strip);
}

[PublicAPI]
public abstract class LedMode<TConfig> : LedMode {
    public TConfig config { get; set; }
    protected LedMode(TConfig config) => this.config = config;
}
