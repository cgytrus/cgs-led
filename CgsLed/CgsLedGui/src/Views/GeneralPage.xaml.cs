using CgsLedController;

using CgsLedServiceTypes;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class GeneralPage {
    private LedControllerConfig _config = new();

    public GeneralPage() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetRunning);
            startStop.Content = context.reader.ReadBoolean() ? "Stop" : "Start";
        }
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("main");
            _config = ConfigFile.LoadOrSave(context.reader.ReadString(), _config);
        }
        brightness.Value = _config.brightness * 100d;
    }

    private void StartStop_OnClick(object sender, RoutedEventArgs e) {
        bool running;
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetRunning);
            running = context.reader.ReadBoolean();
        }

        if(running) {
            startStop.Content = "Stop";
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.Stop);
        }
        else {
            startStop.Content = "Start";
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.Start);
        }
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        _config = _config with { brightness = (float)(brightness.Value / 100d) };
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetConfig);
            context.writer.Write("main");
            ConfigFile.Save(context.reader.ReadString(), _config);
        }
        screen.Save();
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
