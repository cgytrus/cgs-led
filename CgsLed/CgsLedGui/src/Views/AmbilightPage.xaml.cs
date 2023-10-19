using CgsLedServiceTypes;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class AmbilightPage {
    public AmbilightPage() => InitializeComponent();

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
        screen.Save();
        {
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.Reload);
        }
    }

    private void Reload_OnClick(object sender, RoutedEventArgs e) {
        using App.Client client = App.GetClient();
        client.writer.Write((byte)MessageType.Reload);
    }
}
