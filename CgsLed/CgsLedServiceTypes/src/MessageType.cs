namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Quit,
    GetStrips,
    GetModes,
    GetMode,
    SetMode,
    SetFreddy,
    Reload,
    GetConfig,
    GetScreens,
    StreamLeds
}
