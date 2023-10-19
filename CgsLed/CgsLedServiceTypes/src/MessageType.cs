namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Start,
    Stop,
    Quit,
    GetModes,
    GetMode,
    SetMode,
    Reload,
    GetConfig
}
