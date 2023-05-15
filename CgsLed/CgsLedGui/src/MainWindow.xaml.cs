using Microsoft.UI.Xaml;

namespace CgsLedGui;

public sealed partial class MainWindow {
    public MainWindow() => InitializeComponent();

    private void MainWindow_OnClosed(object sender, WindowEventArgs args) => App.StopLed();
}
