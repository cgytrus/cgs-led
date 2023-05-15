// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CgsLedGui.ViewModels;

namespace CgsLedGui.Views;

public sealed partial class ShellPage {
    public static ShellPage? shellHandler { get; set; }
    public ShellViewModel viewModel { get; } = new();

    public ShellPage() {
        InitializeComponent();
        shellHandler = this;
        viewModel.Initialize(shellFrame, navView);
        shellFrame.Navigate(typeof(GeneralPage));
    }
}
