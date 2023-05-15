namespace CgsLedController.Service;

public enum MessageType : byte {
    Start,
    Stop,
    Quit,
    ResetController,
    ConfigReset,
    ConfigFps,
    ConfigBrightness,
    SetPowerOff,
    SetStandByMode,
    SetFireMode,
    SetFftMode,
    SetFftConfig,
    SetAmbilightMode,
    SetAmbilightConfig
}
