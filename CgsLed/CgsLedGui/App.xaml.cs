using System;
using System.IO.Ports;

using CgsLedController;

using Microsoft.UI.Xaml;

using AmbilightMode = CgsLedController.AmbilightMode;

namespace CgsLedGui;

public partial class App {
    internal static LedController? led { get; private set; }
    internal static string port => _port.PortName;
    internal static int baudRate => _port.BaudRate;

    private Window? _window;

    private static SerialPort _port;

    public App() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
        _window = new MainWindow();
        _window.Activate();
    }

    internal static void StartLed(string portName, int baudRate) {
        _port = new(portName, baudRate, Parity.None, 8, StopBits.One);
        led = new LedController(_port) {
            fftMode = new FftMode(_port) {
                ledCounts = new int[] { 177, 82, 30 },
                //period = TimeSpan.FromSeconds(1f / 54f),
                period = TimeSpan.Zero, // no limit
                showFps = false,
                volume = 100f / 8f,
                showStart = 0,
                showCount = 56,
                noiseCut = 0.25f,
                mirror = true
            },
            ambilightMode = new AmbilightMode(_port) {
                ledCounts = new int[] { 177, 82, 30 },
                period = TimeSpan.Zero, // no limit
                showFps = false,
                screen = 0
            }
        };
        led.Start();
    }

    internal static void StopLed() {
        led?.Stop();
        led = null;
    }
}
