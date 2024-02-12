namespace CgsLedController;

public class LedBuffer {
    public bool doPing { get; set; } = true;
    public float brightness { get; set; } = 1f;

    public IReadOnlyList<int> ledCounts { get; }
    public int totalLedCount { get; }
    public int totalHalfLedCount { get; }
    public IReadOnlyList<int> ledStarts { get; }
    public IReadOnlyList<int> halfLedCounts { get; }
    public IReadOnlyList<int> halfLedStarts { get; }

    public readonly record struct LedData(byte data, bool useGamma, bool useBrightness) {
        public byte Get(float brightness) {
            byte res = data;
            if(useGamma)
                res = gamma8[res];
            if(useBrightness)
                res = (byte)(res * brightness);
            return res;
        }
    }

    private readonly LedData[] _ledData = new LedData[1024];
    private int _totalDataCount;

    public LedBuffer(IReadOnlyList<int> ledCounts) {
        this.ledCounts = ledCounts;
        halfLedCounts = ledCounts.Select(c => c % 2 == 0 ? c / 2 : c / 2 + 1).ToArray();
        totalLedCount = 0;
        totalHalfLedCount = 0;
        int[] ledStarts = new int[ledCounts.Count];
        int[] halfLedStarts = new int[ledCounts.Count];
        for(int i = 0; i < ledCounts.Count; i++) {
            ledStarts[i] = totalLedCount;
            halfLedStarts[i] = totalHalfLedCount;
            totalLedCount += ledCounts[i];
            totalHalfLedCount += halfLedCounts[i];
        }
        this.ledStarts = ledStarts;
        this.halfLedStarts = halfLedStarts;
    }

    public void Send(IEnumerable<LedWriter> writers) {
        foreach(LedWriter writer in writers) {
            if(doPing)
                writer.Ping(this);
            writer.Write(_ledData, _totalDataCount, brightness);
        }
        _totalDataCount = 0;
    }

    public void Write(byte value) {
        _ledData[_totalDataCount] = new LedData(value, false, false);
        _totalDataCount++;
    }

    public void Write(byte a, byte b) {
        _ledData[_totalDataCount] = new LedData(a, false, false);
        _ledData[_totalDataCount + 1] = new LedData(b, false, false);
        _totalDataCount += 2;
    }

    public void Write(byte a, byte b, byte c) {
        Write(
            new LedData(a, false, false),
            new LedData(b, false, false),
            new LedData(c, false, false)
        );
    }

    private void Write(LedData a, LedData b, LedData c) {
        _ledData[_totalDataCount] = a;
        _ledData[_totalDataCount + 1] = b;
        _ledData[_totalDataCount + 2] = c;
        _totalDataCount += 3;
    }

    // 2.8
    /*private static readonly byte[] gamma8 = {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
        2, 3, 3, 3, 3, 3, 3, 3, 4, 4, 4, 4, 4, 5, 5, 5,
        5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10,
        10, 10, 11, 11, 11, 12, 12, 13, 13, 13, 14, 14, 15, 15, 16, 16,
        17, 17, 18, 18, 19, 19, 20, 20, 21, 21, 22, 22, 23, 24, 24, 25,
        25, 26, 27, 27, 28, 29, 29, 30, 31, 32, 32, 33, 34, 35, 35, 36,
        37, 38, 39, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 50,
        51, 52, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 66, 67, 68,
        69, 70, 72, 73, 74, 75, 77, 78, 79, 81, 82, 83, 85, 86, 87, 89,
        90, 92, 93, 95, 96, 98, 99, 101, 102, 104, 105, 107, 109, 110, 112, 114,
        115, 117, 119, 120, 122, 124, 126, 127, 129, 131, 133, 135, 137, 138, 140, 142,
        144, 146, 148, 150, 152, 154, 156, 158, 160, 162, 164, 167, 169, 171, 173, 175,
        177, 180, 182, 184, 186, 189, 191, 193, 196, 198, 200, 203, 205, 208, 210, 213,
        215, 218, 220, 223, 225, 228, 231, 233, 236, 239, 241, 244, 247, 249, 252, 255
    };*/
    // 2.2
    private static readonly byte[] gamma8 = {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2,
        3, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6,
        6, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 11, 11, 11, 12,
        12, 13, 13, 13, 14, 14, 15, 15, 16, 16, 17, 17, 18, 18, 19, 19,
        20, 20, 21, 22, 22, 23, 23, 24, 25, 25, 26, 26, 27, 28, 28, 29,
        30, 30, 31, 32, 33, 33, 34, 35, 35, 36, 37, 38, 39, 39, 40, 41,
        42, 43, 43, 44, 45, 46, 47, 48, 49, 49, 50, 51, 52, 53, 54, 55,
        56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71,
        73, 74, 75, 76, 77, 78, 79, 81, 82, 83, 84, 85, 87, 88, 89, 90,
        91, 93, 94, 95, 97, 98, 99, 100, 102, 103, 105, 106, 107, 109, 110, 111,
        113, 114, 116, 117, 119, 120, 121, 123, 124, 126, 127, 129, 130, 132, 133, 135,
        137, 138, 140, 141, 143, 145, 146, 148, 149, 151, 153, 154, 156, 158, 159, 161,
        163, 165, 166, 168, 170, 172, 173, 175, 177, 179, 181, 182, 184, 186, 188, 190,
        192, 194, 196, 197, 199, 201, 203, 205, 207, 209, 211, 213, 215, 217, 219, 221,
        223, 225, 227, 229, 231, 234, 236, 238, 240, 242, 244, 246, 248, 251, 253, 255
    };
    public void WriteRgb(byte r, byte g, byte b, bool gamma) {
        Write(
            new LedData(g, gamma, true),
            new LedData(r, gamma, true),
            new LedData(b, gamma, true)
        );
    }
    public void WriteHsv(float h, float s, float v, bool gamma) {
        float r;
        float g;
        float b;

        if(s <= 0f) {
            byte bv = (byte)(v * 255f);
            WriteRgb(bv, bv, bv, gamma);
            return;
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

        WriteRgb((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), gamma);
    }
}
