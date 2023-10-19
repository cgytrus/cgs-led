namespace CgsLedServiceTypes;

public enum MessageType : byte {
    Start,
    Stop,
    Quit,
    SetMode,
    Reload,
    GetConfig
}
