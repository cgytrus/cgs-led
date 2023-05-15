// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO.Ports;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CgsLedGui.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class GeneralPage {
    public GeneralPage() {
        InitializeComponent();
        startStop.Content = App.led is null ? "Start" : "Stop";
        port.Items.Clear();
        foreach(string portName in SerialPort.GetPortNames())
            port.Items.Add(portName);
        port.SelectedValue ??= App.led is null ? "COM4" : App.port;
        baudRate.SelectedValue ??= App.led is null ? 2000000 : App.baudRate;
    }

    private void StartStop_OnClick(object sender, RoutedEventArgs e) {
        if(App.led is null) {
            startStop.Content = "Stop";
            App.StartLed((string)port.SelectedValue, (int)baudRate.SelectedValue);
        }
        else {
            startStop.Content = "Start";
            App.StopLed();
        }
    }

    private void ResetController_OnClick(object sender, RoutedEventArgs e) => App.led?.ResetController();
    private void ResetSettings_OnClick(object sender, RoutedEventArgs e) => App.led?.ResetSettings();

    private void Brightness_OnValueChanged(object sender, RangeBaseValueChangedEventArgs e) =>
        App.led?.SetBrightness((byte)(e.NewValue / 100d * 255d));
}
