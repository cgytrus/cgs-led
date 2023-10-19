using System.Collections.ObjectModel;

using CgsLedServiceTypes;
using CgsLedServiceTypes.Config;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CgsLedGui.Views;

public sealed partial class ScreenCaptureConfigControl {
    private ScreenCaptureConfig _config = new();
    private readonly ObservableCollection<string> _screens = new();

    public ScreenCaptureConfigControl() {
        InitializeComponent();
        {
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.GetConfig);
            client.writer.Write("screen");
            _config = ConfigFile.LoadOrSave(client.reader.ReadString(), _config);
        }
        UpdateSelectors();
    }

    public void Save() {
        if(screen.Visibility == Visibility.Collapsed) {
            if(window.Visibility == Visibility.Collapsed) {
                _config = new ScreenCaptureConfig {
                    screen = -1,
                    window = null
                };
            }
            else {
                _config = new ScreenCaptureConfig {
                    screen = -1,
                    window = window.Text
                };
            }
        }
        else {
            _config = new ScreenCaptureConfig {
                screen = screen.SelectedIndex,
                window = null
            };
        }
        {
            using App.Client client = App.GetClient();
            client.writer.Write((byte)MessageType.GetConfig);
            client.writer.Write("screen");
            ConfigFile.Save(client.reader.ReadString(), _config);
        }
    }

    private void UpdateSelectors() {
        if(_config.window != null)
            captureType.SelectedIndex = 1;
        else if(_config.screen >= 0)
            captureType.SelectedIndex = 0;
        else
            captureType.SelectedIndex = 2;
    }

    private void SwitchToSelection(int index) {
        switch(index) {
            case 0:
                screen.Visibility = Visibility.Visible;
                window.Visibility = Visibility.Collapsed;
                _screens.Clear();
                {
                    using App.Client client = App.GetClient();
                    client.writer.Write((byte)MessageType.GetScreens);
                    int count = client.reader.ReadInt32();
                    for(int i = 0; i < count; i++)
                        _screens.Add(client.reader.ReadString());
                }
                break;
            case 1:
                screen.Visibility = Visibility.Collapsed;
                window.Visibility = Visibility.Visible;
                window.Text = _config.window;
                break;
            case 2:
                screen.Visibility = Visibility.Collapsed;
                window.Visibility = Visibility.Collapsed;
                break;
        }
    }
    private void CaptureType_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
        SwitchToSelection(captureType.SelectedIndex);
    }
}
