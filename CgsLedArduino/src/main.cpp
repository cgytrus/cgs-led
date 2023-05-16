#define NO_CORRECTION 1
#define NO_CLOCK_CORRECTION 0

#include <Arduino.h>
#include <FastLED.h>

enum class DataType : uint8_t {
    None,
    Power,
    RawData,
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

constexpr int32_t baudRate = 1000000;
constexpr int32_t dataTimeout = 7000; // ms

bool power = false;
CRGB leds[totalLedCount];
bool pendingShow = false;

void setPower(bool newPower) {
    power = newPower;
    if(!power)
        digitalWrite(relayPin, LOW);
    else
        digitalWrite(relayPin, HIGH);
}

void setup() {
    pinMode(relayPin, OUTPUT);
    FastLED.addLeds<WS2812B, pin0, GRB>(leds, 0, ledCount0);
    FastLED.addLeds<WS2812B, pin1, GRB>(leds, ledCount0, ledCount1);
    FastLED.addLeds<WS2812B, pin2, GRB>(leds, ledCount0 + ledCount1, ledCount2);

    setPower(power);
    Serial.begin(baudRate);

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

unsigned long lastDataTime = 0;
bool readNextNoData = false;

uint8_t readNext() {
    int dataInt;
    do {
        if((millis() - lastDataTime) >= dataTimeout) {
            readNextNoData = true;
            return 0;
        }
        dataInt = Serial.read();
    } while(dataInt < 0);
    return static_cast<uint8_t>(dataInt);
}

void readPower() {
    uint8_t data = readNext();
    if(readNextNoData)
        return;
    setPower(data > 0);
}

void readRawData() {
    auto rawData = reinterpret_cast<uint8_t*>(leds);
    for(size_t i = 0; i < totalDataCount; i++) {
        uint8_t data = readNext();
        if(readNextNoData)
            return;
        rawData[i] = data;
    }
    pendingShow = true;
}

void readPing() {
    if(pendingShow)
        FastLED.show();
    pendingShow = false;
    Serial.write(0); // pong
}

void tryReadSerial() {
    int dataInt = Serial.read();
    if(dataInt < 0)
        return;
    dataType = static_cast<DataType>(dataInt);
    switch(dataType) {
        case DataType::None: break;
        case DataType::Power: readPower();
            break;
        case DataType::RawData: readRawData();
            break;
        case DataType::Ping: readPing();
            break;
    }
    endPacket();
    if(!readNextNoData)
        lastDataTime = millis();
}

void loop() {
    tryReadSerial();

    if(!power)
        return;

    if((millis() - lastDataTime) >= dataTimeout) {
        endPacket();
        setPower(false);
        readNextNoData = false;
        pendingShow = false;
    }
}
