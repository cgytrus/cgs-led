namespace CgsLedService;

public static class BinaryReaderExtensions {
    public static TimeSpan ReadTimeSpan(this BinaryReader reader) => new(reader.ReadInt64());
}
