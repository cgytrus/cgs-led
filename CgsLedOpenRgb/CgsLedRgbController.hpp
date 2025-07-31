#pragma once

#include "RGBController.h"
#include "serial_port.h"
#include <string_view>

enum class DataType : char {
    Power,
    Data,
    Ping
};

class CgsLedRgbController : public RGBController {
public:
    CgsLedRgbController(const char* port, int baud, unsigned int brightness);
    ~CgsLedRgbController();

    void SetupZones();

    void ResizeZone(int zone, int new_size);

    void DeviceUpdateLEDs();
    void UpdateZoneLEDs(int zone);
    void UpdateSingleLED(int led);

    void DeviceUpdateMode();

private:
    serial_port* m_serial;
    char* m_buffer;
    bool m_canContinue = true;
};
