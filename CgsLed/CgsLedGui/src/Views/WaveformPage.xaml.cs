using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class WaveformPage {
    private WaveformModeConfig _config = new(new MusicColors());

    private MusicColors colors {
        get => _config.colors;
        set {
            _config = _config with { colors = value };
            colorsRenderer.colors = value;
        }
    }

    public float hueSpeed {
        get => colors.hueSpeed;
        set => colors = colors with { hueSpeed = value };
    }

    public float hueOffset {
        get => colors.hueOffset;
        set => colors = colors with { hueOffset = value };
    }

    public float rightHueOffset {
        get => colors.rightHueOffset;
        set => colors = colors with { rightHueOffset = value };
    }

    public float hueRange {
        get => colors.hueRange;
        set => colors = colors with { hueRange = value };
    }

    public float saturation {
        get => colors.saturation * 100f;
        set => colors = colors with { saturation = value / 100f };
    }

    public bool gammaCorrection {
        get => colors.gammaCorrection;
        set => colors = colors with { gammaCorrection = value };
    }

    public WaveformPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/waveform");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
        colorsRenderer.colors = _config.colors;
        bufferSeconds.Value = _config.bufferSeconds;
        displaySeconds.Value = _config.displaySeconds;
        avgCount.Value = _config.avgCount;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        _config = _config with {
            bufferSeconds = (float)bufferSeconds.Value,
            displaySeconds = (float)displaySeconds.Value,
            avgCount = (int)avgCount.Value
        };
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/waveform");
            ConfigFile.Save(context.reader.ReadString(), _config);
        }
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.Reload);
        }
    }

    private void Reload_OnClick(object sender, RoutedEventArgs e) {
        using App.IpcContext context = App.GetIpc();
        context.writer.Write((byte)MessageType.Reload);
    }
}
