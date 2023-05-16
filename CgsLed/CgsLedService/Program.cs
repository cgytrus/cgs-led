﻿using System.Diagnostics.CodeAnalysis;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

using CgsLedController;
using CgsLedController.Service;

using CgsLedService.Modes.Ambilight;
using CgsLedService.Modes.Fft;
using CgsLedService.Modes.Fire;
using CgsLedService.Modes.StandBy;

namespace CgsLedService;

internal static class Program {
    private const string DefaultPortName = "COM2";
    private const int DefaultBaudRate = 1000000;

    private static bool _running = true;

    private static LedController? _led;

    private static readonly StandByMode standByMode = new() { period = TimeSpan.Zero };
    private static readonly FireMode fireMode = new() { period = TimeSpan.Zero };

    private static readonly FftMode fftMode = new() {
        period = TimeSpan.Zero,
        volume = 100f / 8f,
        showStart = 0,
        showCount = 56,
        noiseCut = 0.25f,
        mirror = true
    };

    private static readonly AmbilightMode ambilightMode = new() {
        period = TimeSpan.Zero,
        screen = 0
    };

    private static void Main(string[] args) {
        Start();
        _led?.SetMode(fireMode);

        IPEndPoint ip = new(IPAddress.Loopback, 42069);
        TcpListener listener = new(ip);
        try {
            listener.Start();

            while(_running) {
                using TcpClient handler = listener.AcceptTcpClient();
                using NetworkStream stream = handler.GetStream();
                try { ReadMessage(stream); }
                catch(Exception ex) {
                    Console.WriteLine("Failed to read message:");
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        finally {
            listener.Stop();
            _led?.Stop();
        }
    }

    // TODO: holy fuck
    private static void ReadMessage(NetworkStream stream) {
        while(!stream.DataAvailable) { }
        MessageType type = (MessageType)stream.ReadByte();
        switch(type) {
            case MessageType.Start:
                while(stream.Socket.Available < 5) { }
                int port = stream.ReadByte();
                Span<byte> baudRateBytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                int baudRate = BitConverter.ToInt32(baudRateBytes);
                Start(SerialPort.GetPortNames()[port], baudRate);
                break;
            case MessageType.Stop:
                Stop();
                break;
            case MessageType.Quit:
                Stop();
                _running = false;
                break;
            case MessageType.ResetController:
                ResetController();
                break;
            case MessageType.ConfigReset:
                ConfigReset();
                break;
            case MessageType.ConfigFps:
                while(!stream.DataAvailable) { }
                Span<byte> bytes = stackalloc byte[1] { (byte)stream.ReadByte() };
                ConfigFps(BitConverter.ToBoolean(bytes));
                break;
            case MessageType.ConfigBrightness:
                while(!stream.DataAvailable) { }
                ConfigBrightness((byte)stream.ReadByte());
                break;
            case MessageType.SetPowerOff:
                SetPowerOff();
                break;
            case MessageType.SetStandByMode:
                SetMode(standByMode);
                break;
            case MessageType.SetFireMode:
                SetMode(fireMode);
                break;
            case MessageType.SetFftMode:
                SetMode(fftMode);
                break;
            case MessageType.SetFftConfig:
                SetFftConfig((FftConfigType)stream.ReadByte(), stream);
                break;
            case MessageType.SetAmbilightMode:
                SetMode(ambilightMode);
                break;
            case MessageType.SetAmbilightConfig:
                SetAmbilightConfig((AmbilightConfigType)stream.ReadByte(), stream);
                break;
            default:
                Console.WriteLine("Unknown message");
                break;
        }
    }

    private static void Start(string portName = DefaultPortName, int baudRate = DefaultBaudRate) {
        Console.WriteLine($"Starting on port {portName} with baud rate {baudRate}");
        SerialPort port = new(portName, baudRate, Parity.None, 8, StopBits.One);
        _led = new LedController(port, new int[] { 177, 82, 30 });
        _led.Start();
        Console.WriteLine("Ready");
    }

    private static void Stop() {
        Console.WriteLine("Stopping...");
        _led?.Stop();
        _led = null;
        Console.WriteLine("Stopped");
    }

    [MemberNotNullWhen(true, nameof(_led))]
    private static bool CheckRunning() {
        if(_led is not null)
            return true;
        Console.WriteLine("Not running");
        return false;
    }

    private static void ResetController() {
        if(!CheckRunning())
            return;
        Console.WriteLine("Resetting controller");
        _led.ResetController();
    }

    private static void ConfigReset() {
        if(!CheckRunning())
            return;
        Console.WriteLine("Resetting settings");
        _led.ResetSettings();
    }

    private static void ConfigFps(bool showFps) {
        if(!CheckRunning())
            return;
        Console.WriteLine($"Setting show FPS to {showFps}");
        _led.showFps = showFps;
    }

    private static void ConfigBrightness(byte brightness) {
        if(!CheckRunning())
            return;
        Console.WriteLine($"Setting brightness to {brightness.ToString()}");
        _led.SetBrightness(brightness);
    }

    private static void SetPowerOff() {
        if(!CheckRunning())
            return;
        Console.WriteLine($"Powering off");
        _led.SetPowerOff();
    }

    private static void SetMode(LedMode mode) {
        if(!CheckRunning())
            return;
        Console.WriteLine($"Setting mode to {mode.GetType().Name}");
        _led.SetMode(mode);
    }

    private static void SetFftConfig(FftConfigType type, NetworkStream stream) {
        switch(type) {
            case FftConfigType.Volume:
                while(stream.Socket.Available < 4) { }
                Span<byte> bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                fftMode.volume = 100f / BitConverter.ToInt32(bytes);
                break;
            case FftConfigType.Rate:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                fftMode.period = TimeSpan.FromSeconds(1f / BitConverter.ToInt32(bytes));
                break;
            case FftConfigType.From:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                fftMode.showStart = BitConverter.ToInt32(bytes);
                break;
            case FftConfigType.To:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                fftMode.showCount = BitConverter.ToInt32(bytes);
                break;
            case FftConfigType.Noise:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                fftMode.noiseCut = BitConverter.ToSingle(bytes);
                break;
            case FftConfigType.Mirror:
                while(stream.Socket.Available < 1) { }
                bytes = stackalloc byte[1] { (byte)stream.ReadByte() };
                fftMode.mirror = BitConverter.ToBoolean(bytes);
                break;
        }
    }

    private static void SetAmbilightConfig(AmbilightConfigType type, NetworkStream stream) {
        switch(type) {
            case AmbilightConfigType.Rate:
                while(stream.Socket.Available < 4) { }
                Span<byte> bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                ambilightMode.period = TimeSpan.FromSeconds(1f / BitConverter.ToInt32(bytes));
                break;
            case AmbilightConfigType.Screen:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                ambilightMode.screen = BitConverter.ToInt32(bytes);
                ambilightMode.window = null;
                if(_led?.customMode == ambilightMode)
                    _led.SetMode(ambilightMode);
                break;
            case AmbilightConfigType.Window:
                while(stream.Socket.Available < 4) { }
                bytes = stackalloc byte[4] {
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte(),
                    (byte)stream.ReadByte()
                };
                int length = BitConverter.ToInt32(bytes);
                while(stream.Socket.Available < length) { }
                bytes = stackalloc byte[256];
                bytes = bytes[..length];
                stream.ReadExactly(bytes);
                ambilightMode.screen = -1;
                ambilightMode.window = Encoding.Default.GetString(bytes);
                if(_led?.customMode == ambilightMode)
                    _led.SetMode(ambilightMode);
                break;
            case AmbilightConfigType.WindowReset:
                ambilightMode.screen = -1;
                ambilightMode.window = null;
                if(_led?.customMode == ambilightMode)
                    _led.SetMode(ambilightMode);
                break;
        }
    }
}
