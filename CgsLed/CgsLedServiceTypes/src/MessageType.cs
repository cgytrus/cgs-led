namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Quit,
    GetStrips,
    GetModes,
    GetMode,
    SetMode,
    Reload,
    GetConfig,
    GetScreens
}
