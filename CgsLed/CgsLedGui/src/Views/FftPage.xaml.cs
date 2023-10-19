using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class FftPage {
    private FftModeConfig _config = new(new MusicColors());

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

    public FftPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/fft");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
        colorsRenderer.colors = _config.colors;
        show.RangeStart = _config.showStart;
        show.RangeEnd = _config.showStart + _config.showCount - 1;
        noiseCut.Value = _config.noiseCut * 100d;
        mirror.IsChecked = _config.mirror;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        _config = _config with {
            showStart = (int)show.RangeStart,
            showCount = (int)show.RangeEnd - (int)show.RangeStart + 1,
            noiseCut = (float)(noiseCut.Value / 100d),
            mirror = mirror.IsChecked ?? false
        };
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/fft");
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
