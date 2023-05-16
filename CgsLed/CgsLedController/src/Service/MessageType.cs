﻿namespace CgsLedController.Service;

public enum MessageType : byte {
    Start,
    Stop,
    Quit,
    SetPowerOff,
    SetMode,
    ReloadConfig,
    ReloadModeConfig
}
