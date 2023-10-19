using CgsLedServiceTypes;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class GeneralPage {
    public GeneralPage() {
        InitializeComponent();
        using App.Client client = App.GetClient();
        client.writer.Write((byte)MessageType.GetRunning);
        startStop.Content = client.reader.ReadBoolean() ? "Stop" : "Start";
    }

    private void StartStop_OnClick(object sender, RoutedEventArgs e) {
        bool running;
        {
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.GetRunning);
            running = client.reader.ReadBoolean();
        }

        if(running) {
            startStop.Content = "Stop";
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.Stop);
        }
        else {
            startStop.Content = "Start";
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.Start);
        }
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        using App.Client client = App.GetClient();
        client.writer.Write((byte)MessageType.Reload);
    }
}
