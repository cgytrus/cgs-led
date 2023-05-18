using JetBrains.Annotations;

namespace CgsLedService;

[PublicAPI]
public static class MoreMath {
    public static double LerpUnclamped(double a, double b, double t) => a + (b - a) * t;
    public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
    public static double Lerp(double a, double b, double t) => t <= 0d ? a : t >= 1d ? b : LerpUnclamped(a, b, t);
    public static float Lerp(float a, float b, float t) => t <= 0f ? a : t >= 1f ? b : LerpUnclamped(a, b, t);
    public static float SampleToDb(float x) => Math.Clamp(20f * MathF.Log10(x), -60f, 0f);
    public static float DbToUnit(float x) => (x + 60f) / 60f;
    public static float SampleToUnitDb(float x) => DbToUnit(SampleToDb(x));
}
