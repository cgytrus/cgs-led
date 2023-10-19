using System.Diagnostics;

using Windows.UI;

using CgsLedServiceTypes;

using Microsoft.Graphics.Canvas.UI.Xaml;

namespace CgsLedGui.Views;

public sealed partial class MusicColorsRenderer {
    public MusicColors colors { get; set; }

    private readonly Stopwatch _timer = Stopwatch.StartNew();

    public MusicColorsRenderer() => InitializeComponent();

    private void CanvasControl_OnDraw(CanvasControl sender, CanvasDrawEventArgs args) {
        for(int i = 0; i < 60; i++) {
            const float size = 10f;
            float x = i / 59f;
            float time = (float)_timer.Elapsed.TotalSeconds;
            for(int j = 1; j <= 6; j++) {
                float h = time * colors.hueSpeed + colors.hueOffset + x * colors.rightHueOffset + j / 6f * colors.hueRange;
                args.DrawingSession.FillRectangle(i * size, j * size, size, size, FromHsv(h, colors.saturation, j / 6f));
            }
        }
        sender.Invalidate();
    }

    private static Color FromHsv(float h, float s, float v) {
        float r;
        float g;
        float b;

        if(s <= 0f) {
            byte bv = (byte)(v * 255f);
            return Color.FromArgb(255, bv, bv, bv);
        }

        float hh = h;
        while(hh >= 360f)
            hh -= 360f;
        while(hh < 0f)
            hh += 360f;
        hh /= 60f;
        int i = (int)hh;
        float ff = hh - i;
        float p = v * (1f - s);
        float q = v * (1f - (s * ff));
        float t = v * (1f - (s * (1f - ff)));

        switch(i) {
            case 0:
                r = v;
                g = t;
                b = p;
                break;
            case 1:
                r = q;
                g = v;
                b = p;
                break;
            case 2:
                r = p;
                g = v;
                b = t;
                break;
            case 3:
                r = p;
                g = q;
                b = v;
                break;
            case 4:
                r = t;
                g = p;
                b = v;
                break;
            default:
                r = v;
                g = p;
                b = q;
                break;
        }

        return Color.FromArgb(255, (byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f));
    }
}
