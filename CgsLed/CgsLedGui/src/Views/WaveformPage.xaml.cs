using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class WaveformPage {
    private WaveformModeConfig _config = new(new MusicColors());

    public WaveformPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("mode/waveform");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
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
