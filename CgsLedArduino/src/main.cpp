void(*reset)(void) = 0;

#include <Arduino.h>
#include <EEPROM.h>
#include <FastLED.h>

enum class Mode : uint8_t {
    Standby,
    Custom,
    Test,
    Fire,
    Off
};

enum class DataType : uint8_t {
    None,
    Reset,
    SettingsReset,
    Mode,
    Brightness,
    RawData,
    FftData,
    FftMirroredData,
    Ping
};

constexpr uint8_t relayPin = 3;

constexpr uint8_t pin0 = 5;
constexpr size_t ledCount0 = 177;
constexpr size_t halfLedCount0 = ledCount0 % 2 == 0 ? ledCount0 / 2 : ledCount0 / 2 + 1;
constexpr size_t start0 = 0;
constexpr size_t end0 = start0 + ledCount0 - 1;

constexpr uint8_t pin1 = 6;
constexpr size_t ledCount1 = 82;
constexpr size_t halfLedCount1 = ledCount1 % 2 == 0 ? ledCount1 / 2 : ledCount1 / 2 + 1;
constexpr size_t start1 = end0 + 1;
constexpr size_t end1 = start1 + ledCount1 - 1;

constexpr uint8_t pin2 = 9;
constexpr size_t ledCount2 = 30;
constexpr size_t halfLedCount2 = ledCount2 % 2 == 0 ? ledCount2 / 2 : ledCount2 / 2 + 1;
constexpr size_t start2 = end1 + 1;

constexpr size_t totalLedCount = ledCount0 + ledCount1 + ledCount2;
constexpr size_t totalDataCount = totalLedCount * 3;

constexpr uint8_t defaultBrightness = 63; // 0-255

constexpr uint8_t currentEepromVersion = 0;
constexpr uint8_t eepromVersion = 0;
constexpr uint8_t eepromBrightness = 1;

constexpr int32_t baudRate = 1000000;
constexpr int32_t dataTimeout = 7000; // ms

Mode mode = Mode::Off;
CRGB leds[totalLedCount];

void resetSettings() {
    EEPROM.write(eepromVersion, currentEepromVersion);
    EEPROM.write(eepromBrightness, defaultBrightness);
}

void loadSettings() {
    if(EEPROM.read(eepromVersion) != currentEepromVersion)
        resetSettings();
    FastLED.setBrightness(EEPROM.read(eepromBrightness));
}

void setMode(Mode newMode) {
    mode = newMode;
    if(mode == Mode::Off) {
        FastLED.showColor(CRGB::Black);
        digitalWrite(relayPin, LOW);
    }
    else
        digitalWrite(relayPin, HIGH);
}

void setup() {
    pinMode(relayPin, OUTPUT);
    FastLED.addLeds<WS2812B, pin0, GRB>(leds, 0, ledCount0);
    FastLED.addLeds<WS2812B, pin1, GRB>(leds, ledCount0, ledCount1);
    FastLED.addLeds<WS2812B, pin2, GRB>(leds, ledCount0 + ledCount1, ledCount2);

    setMode(mode);
    Serial.begin(baudRate);
    loadSettings();

    // reset any pending data
    int dataInt;
    do {
        dataInt = Serial.read();
    } while(dataInt >= 0);
}

DataType dataType = DataType::None;
void endPacket() {
    dataType = DataType::None;
}

uint8_t readNext() {
    int dataInt;
    do {
        dataInt = Serial.read();
    } while(dataInt < 0);
    return static_cast<uint8_t>(dataInt);
}

void readReset() {
    reset();
}

void readSettingsReset() {
    resetSettings();
}

void readMode() {
    setMode(static_cast<Mode>(readNext()));
    if(mode == Mode::Custom)
        Serial.write(0);
}

void readBrightness() {
    uint8_t data = readNext();
    FastLED.setBrightness(data);
    EEPROM.write(eepromBrightness, data);
}

void readRawData() {
    auto rawData = reinterpret_cast<uint8_t*>(leds);
    for(size_t i = 0; i < totalDataCount; i++) {
        uint8_t data = readNext();
        rawData[i] = data;
    }
    FastLED.show();
    Serial.write(0);
}

void readFftData() {
    for(size_t i = 0; i < totalLedCount; i++) {
        uint8_t data = readNext();
        leds[i].setHSV(data / 3, 255, data);
    }
    FastLED.show();
    Serial.write(0);
}
void readFftMirroredData() {
    for(size_t i = 0; i < halfLedCount0; i++) {
        uint8_t data = readNext();
        leds[start0 + i].setHSV(data / 3, 255, data);
        leds[start0 + ledCount0 - 1 - i] = leds[start0 + i];
    }
    for(size_t i = 0; i < halfLedCount1; i++) {
        uint8_t data = readNext();
        leds[start1 + i].setHSV(data / 3, 255, data);
        leds[start1 + ledCount1 - 1 - i] = leds[start1 + i];
    }
    for(size_t i = 0; i < halfLedCount2; i++) {
        uint8_t data = readNext();
        leds[start2 + i].setHSV(data / 3, 255, data);
        leds[start2 + ledCount2 - 1 - i] = leds[start2 + i];
    }
    FastLED.show();
    Serial.write(0);
}

void readPing() {
    setMode(mode);
}

unsigned long lastDataTime = 0;
void tryReadSerial() {
    int dataInt = Serial.read();
    if(dataInt < 0)
        return;
    dataType = static_cast<DataType>(dataInt);
    switch(dataType) {
        case DataType::None: break;
        case DataType::Reset: readReset();
            break;
        case DataType::SettingsReset: readSettingsReset();
            break;
        case DataType::Mode: readMode();
            break;
        case DataType::Brightness: readBrightness();
            break;
        case DataType::RawData: readRawData();
            break;
        case DataType::FftData: readFftData();
            break;
        case DataType::FftMirroredData: readFftMirroredData();
            break;
        case DataType::Ping: readPing();
            break;
    }
    endPacket();
    lastDataTime = millis();
}

void drawModeStandby() {
    for(size_t i = 0; i < totalLedCount; i++) {
        int offset = (int)(sin((double)(millis() + i * 100) / 1000.0) * 40.0);
        int sec = constrain(offset, 0, 255);
        leds[i].setRGB(sec, constrain(255 + offset, 0, 255), sec);
    }
}

void drawModeTest() {
    for(size_t i = 0; i < totalLedCount; i++) {
        leds[i].setRGB(255, 255, 255);
    }
}

void drawModeFire() {
    unsigned long time = millis() / 10;
    fill_2dnoise8(leds, totalLedCount, 1, false, 1, 0, 30, 0, 1, time, 1, 0, 1, 0, 1, 0, false);
}

void loop() {
    tryReadSerial();

    switch(mode) {
        case Mode::Off: break;
        case Mode::Custom:
            if((millis() - lastDataTime) >= dataTimeout) {
                endPacket();
                setMode(Mode::Off);
            }
            return;
        case Mode::Standby: drawModeStandby();
            break;
        case Mode::Test: drawModeTest();
            break;
        case Mode::Fire: drawModeFire();
            break;
    }

    if((millis() - lastDataTime) >= dataTimeout)
        setMode(Mode::Off);

    if(mode != Mode::Off)
        FastLED.show();
}
