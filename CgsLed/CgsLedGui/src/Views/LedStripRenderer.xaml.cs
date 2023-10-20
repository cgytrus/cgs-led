using System;
using System.Linq;

using Windows.UI;

using CgsLedServiceTypes;

using Microsoft.Graphics.Canvas.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class LedStripRenderer : IDisposable {
    private readonly int[] _ledCounts;
    private readonly string[] _aliases;
    private readonly App.IpcContext _context;

    private const float Size = 6f;
    private const float OffsetX = 0f;
    private const float OffsetY = 1f;
    private const float OffsetYPing = 2f;
    private const float SizePing = 4f;

    public LedStripRenderer() {
        InitializeComponent();
        {
            using App.IpcContext context = App.GetIpc();
            context.writer.Write((byte)MessageType.GetStrips);
            _ledCounts = new int[context.reader.ReadInt32()];
            _aliases = new string[_ledCounts.Length];
            for(int i = 0; i < _ledCounts.Length; i++) {
                int aliasCount = context.reader.ReadInt32();
                for(int j = 0; j < aliasCount; j++) {
                    _aliases[i] = context.reader.ReadString();
                }
                _ledCounts[i] = context.reader.ReadInt32();
            }
        }

        canvas.Width = (_ledCounts.Max() - 1) * (Size + OffsetX) + Size;
        canvas.Height = _ledCounts.Length * (Size + OffsetY) + OffsetYPing + SizePing;

        _context = App.GetIpc();
        _context.writer.Write((byte)MessageType.StreamLeds);
    }

    private void CanvasControl_OnDraw(CanvasControl sender, CanvasDrawEventArgs args) {
        bool hadData = false;
        for(int i = 0; i < 2; i++) {
            byte dataType = _context.reader.ReadByte();
            if(dataType == 1) {
                hadData = true;
                break;
            }
            _context.Flush();
            args.DrawingSession.FillRectangle(0f, _ledCounts.Length * (Size + OffsetY) + OffsetYPing, SizePing, SizePing,
                Color.FromArgb(255, 0, 255, 0));
        }

        if(!hadData) {
            sender.Invalidate();
            return;
        }

        for(int strip = 0; strip < _ledCounts.Length; strip++) {
            int ledCount = _ledCounts[strip];
            for(int i = 0; i < ledCount; i++) {
                args.DrawingSession.FillRectangle(i * (Size + OffsetX), strip * (Size + OffsetY), Size, Size,
                    Color.FromArgb(255, _context.reader.ReadByte(), _context.reader.ReadByte(),
                        _context.reader.ReadByte()));
            }
        }

        sender.Invalidate();
    }

    public void Dispose() => _context.Dispose();
}
