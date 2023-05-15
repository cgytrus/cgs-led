namespace CgsLedController.Service;

public enum MessageType : byte {
    Start,
    Stop,
    ResetController,
    ResetSettings,
    SetBrightness,
    SetMode,
    SetFftMode,
    SetFftConfig,
    SetAmbilightMode,
    SetAmbilightConfig
}
