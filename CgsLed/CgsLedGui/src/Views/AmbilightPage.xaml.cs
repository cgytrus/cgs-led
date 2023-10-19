using CgsLedServiceTypes;

using Microsoft.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class AmbilightPage {
    public AmbilightPage() => InitializeComponent();

    private void Apply_OnClick(object sender, RoutedEventArgs e) {
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
