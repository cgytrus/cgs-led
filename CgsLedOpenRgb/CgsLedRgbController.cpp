#include "CgsLedRgbController.hpp"

CgsLedRgbController::CgsLedRgbController(const char* port, int baud, unsigned int brightness) {
    m_serial = new serial_port(port, baud);
    m_serial->serial_set_dtr(true);

    m_buffer = new char[1024];
    memset(m_buffer, 0, 1024);

    name = "CG's LED";
    type = DEVICE_TYPE_LEDSTRIP;
    location = port;

    mode off;
    off.name = "Off";
    off.value = 0;
    off.flags = 0;
    off.color_mode = MODE_COLORS_NONE;
    this->modes.push_back(off);

    mode direct;
    direct.name = "Direct";
    direct.value = 1;
    direct.flags = MODE_FLAG_HAS_PER_LED_COLOR | MODE_FLAG_HAS_BRIGHTNESS;
    direct.brightness_min = 0;
    direct.brightness_max = 100;
    direct.brightness = brightness;
    direct.color_mode = MODE_COLORS_PER_LED;
    this->modes.push_back(direct);

    mode freddy;
    freddy.name = "Freddy";
    freddy.value = 2;
    freddy.flags = 0;
    freddy.color_mode = MODE_COLORS_NONE;
    this->modes.push_back(freddy);

    SetupZones();
}

CgsLedRgbController::~CgsLedRgbController() {
    m_serial->serial_close();
    delete m_serial;
    delete[] m_buffer;
}

void CgsLedRgbController::SetupZones() {
    zone windowZone;
    windowZone.name = "Window";
    windowZone.type = ZONE_TYPE_LINEAR;
    windowZone.leds_count = 177;
    windowZone.leds_min = windowZone.leds_count;
    windowZone.leds_max = windowZone.leds_count;
    windowZone.matrix_map = NULL;
    this->zones.push_back(windowZone);
    for (size_t i = 0; i < windowZone.leds_count; i++) {
        led x;
        x.name = "Window LED ";
        x.name.append(std::to_string(i));
        this->leds.push_back(x);
    }

    zone doorZone;
    doorZone.name = "Door";
    doorZone.type = ZONE_TYPE_LINEAR;
    doorZone.leds_count = 82;
    doorZone.leds_min = doorZone.leds_count;
    doorZone.leds_max = doorZone.leds_count;
    doorZone.matrix_map = NULL;
    this->zones.push_back(doorZone);
    for (size_t i = 0; i < doorZone.leds_count; i++) {
        led x;
        x.name = "Door LED ";
        x.name.append(std::to_string(i));
        this->leds.push_back(x);
    }

    zone monitorZone;
    monitorZone.name = "Monitor";
    monitorZone.type = ZONE_TYPE_LINEAR;
    monitorZone.leds_count = 30;
    monitorZone.leds_min = monitorZone.leds_count;
    monitorZone.leds_max = monitorZone.leds_count;
    monitorZone.matrix_map = NULL;
    this->zones.push_back(monitorZone);
    for (size_t i = 0; i < doorZone.leds_count; i++) {
        led x;
        x.name = "Monitor LED ";
        x.name.append(std::to_string(i));
        this->leds.push_back(x);
    }

    SetupColors();
}

void CgsLedRgbController::ResizeZone(int, int) { }

void CgsLedRgbController::DeviceUpdateLEDs() {
    if (this->active_mode != 1)
        return;

    size_t off = 0;
    m_buffer[off++] = static_cast<char>(DataType::Data);
    for (size_t i = 0; i < this->colors.size(); i++) {
        m_buffer[off++] = static_cast<char>(RGBGetGValue(this->colors[i]) * this->modes[1].brightness / 100.0);
        m_buffer[off++] = static_cast<char>(RGBGetRValue(this->colors[i]) * this->modes[1].brightness / 100.0);
        m_buffer[off++] = static_cast<char>(RGBGetBValue(this->colors[i]) * this->modes[1].brightness / 100.0);
    }
    m_buffer[off++] = static_cast<char>(DataType::Ping);

    // wait for the result of the last ping
    while (!m_canContinue) {
        char x;
        int read = m_serial->serial_read(&x, 1);
        if (read > 0 && x == 0)
            m_canContinue = true;
    }
    m_canContinue = false;

    m_serial->serial_write(m_buffer, static_cast<int>(off));
}

void CgsLedRgbController::UpdateZoneLEDs(int) { this->DeviceUpdateLEDs(); }

void CgsLedRgbController::UpdateSingleLED(int) { this->DeviceUpdateLEDs(); }

void CgsLedRgbController::DeviceUpdateMode() {
    while (!m_canContinue) {
        char x;
        int read = m_serial->serial_read(&x, 1);
        if (read > 0 && x == 0)
            m_canContinue = true;
    }
    m_canContinue = false;

    char data[3] {
        static_cast<char>(DataType::Power), static_cast<char>(this->active_mode),
        static_cast<char>(DataType::Ping)
    };
    m_serial->serial_write(data, 3);
}
