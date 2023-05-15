namespace CgsLedController.Service;

public enum MessageType : byte {
    Start,
    Stop,
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
