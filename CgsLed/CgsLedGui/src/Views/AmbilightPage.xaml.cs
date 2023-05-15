// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO.Ports;

using CgsLedController;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

using AmbilightMode = CgsLedController.AmbilightMode;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CgsLedGui.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AmbilightPage {
    public AmbilightPage() => InitializeComponent();

    private void Select_OnClick(object sender, RoutedEventArgs e) => App.led?.SetMode(BuiltInMode.Ambilight);

    private void Rate_OnValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) {
        AmbilightMode? mode = App.led?.ambilightMode;
        if(mode is not null)
            mode.period = args.NewValue <= 0d ? TimeSpan.Zero : TimeSpan.FromSeconds(1d / args.NewValue);
    }
}
