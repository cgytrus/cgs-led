namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Start,
    Stop,
    Quit,
    GetRunning,
    GetModes,
    GetMode,
    SetMode,
    Reload,
    GetConfig,
    GetScreens
}
