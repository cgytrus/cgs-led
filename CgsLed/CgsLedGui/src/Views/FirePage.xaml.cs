// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.IO.Ports;

using CgsLedController;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CgsLedGui.Views;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class FirePage {
    public FirePage() => InitializeComponent();

    private void Select_OnClick(object sender, RoutedEventArgs e) => App.led?.SetMode(BuiltInMode.Fire);
}
