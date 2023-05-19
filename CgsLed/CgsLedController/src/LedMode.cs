﻿using System.Diagnostics;

using JetBrains.Annotations;

namespace CgsLedController;

[PublicAPI]
public abstract class LedMode {
    private const int HeaderSize = 1;

    public TimeSpan time => _stopwatch.Elapsed;

    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    protected LedWriter writer { get; private set; } = null!;

    public void Start(LedWriter writer) {
        this.writer = writer;
        _stopwatch.Restart();
        Main();
    }

    public virtual void StopMode() { }

    protected virtual void Main() { }

    public abstract void Update();
    public abstract void Draw(int strip);
}

[PublicAPI]
public abstract class LedMode<TConfig> : LedMode {
    public TConfig config { get; set; }
    protected LedMode(TConfig config) => this.config = config;
}
