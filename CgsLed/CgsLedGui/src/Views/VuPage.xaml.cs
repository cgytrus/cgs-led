using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class VuPage {
    private VuModeConfig _config = new(new MusicColors());

    public VuPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/vu");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
        sampleCount.Value = _config.sampleCount;
        falloffSpeed.Value = _config.falloffSpeed;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        _config = _config with {
            sampleCount = (int)sampleCount.Value,
            falloffSpeed = (float)falloffSpeed.Value
        };
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/vu");
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
