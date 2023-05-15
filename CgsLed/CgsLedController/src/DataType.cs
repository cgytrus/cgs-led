namespace CgsLedController;

public enum DataType : byte {
    None,
    Reset,
    SettingsReset,
    Mode,
    Brightness,
    RawData,
    FftData,
    FftMirroredData,
    Ping
}
