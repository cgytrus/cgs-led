using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class FftPage {
    private FftModeConfig _config = new(new MusicColors());

    public FftPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/fft");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
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
