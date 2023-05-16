namespace CgsLedConsole;

public static class BinaryWriterExtensions {
    public static void Write(this BinaryWriter writer, TimeSpan x) => writer.Write(x.Ticks);
}
