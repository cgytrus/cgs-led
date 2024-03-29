using CgsLedServiceTypes;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class BasicModeControl {
    private string _mode = "off";

    public string mode {
        get => _mode;
        set {
            _mode = value;
            UpdateSelectors();
        }
    }

    public BasicModeControl() => InitializeComponent();

    private void Select_OnClick(object sender, RoutedEventArgs e) {
        if(sender is not FrameworkElement element)
            return;
        if(element.Tag is not string strip)
            return;
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.SetMode);
            context.writer.Write(mode);
            context.writer.Write(strip);
        }
        UpdateSelectors();
    }

    private void UpdateSelectors() {
        using App.IpcContext context = App.GetIpc();
        context.writer.Write((byte)MessageType.GetModes);
        int count = context.reader.ReadInt32();
        for(int i = 0; i < count; i++) {
            string mode = context.reader.ReadString();
            string strip = context.reader.ReadString();
            switch(strip) {
                case "window": window.IsEnabled = mode != this.mode;
                    break;
                case "door": door.IsEnabled = mode != this.mode;
                    break;
                case "monitor": monitor.IsEnabled = mode != this.mode;
                    break;
            }
        }
        if(count == 0) {
            window.IsEnabled = false;
            door.IsEnabled = false;
            monitor.IsEnabled = false;
        }
        all.IsEnabled = window.IsEnabled || door.IsEnabled || monitor.IsEnabled;
    }
}
