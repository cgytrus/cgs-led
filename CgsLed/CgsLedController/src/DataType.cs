namespace CgsLedController;

public enum DataType : byte {
    None,
    Reset,
    SettingsReset,
    Power,
    Brightness,
    RawData,
    Ping
}
