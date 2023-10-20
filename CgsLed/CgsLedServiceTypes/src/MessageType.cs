namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Quit,
    GetModes,
    GetMode,
    SetMode,
    Reload,
    GetConfig,
    GetScreens
}
