#define NO_CORRECTION 1
#define NO_CLOCK_CORRECTION 1

#include <Arduino.h>
#include <FastLED.h>
#include "uart.hpp"

// --- SETTINGS ---

constexpr uint8_t relayPin = 3;

template<uint8_t DATA_PIN>
using strip_type = WS2812B<DATA_PIN, GRB>;
constexpr size_t stripCount = 3;
constexpr uint8_t ledPins[stripCount] = { 5, 6, 9 };
constexpr size_t ledCounts[stripCount] = { 177, 82, 30 };

constexpr int32_t baudRate = 1000000;

// ----------------

// compile-time stuff collapse these
template<typename T, size_t Size>
constexpr T arraySum(const T (&arr)[Size]) {
    T ret = 0;
    for(size_t i = 0; i < Size; ++i)
        ret += arr[i];
    return ret;
}
constexpr size_t totalDataCount = arraySum(ledCounts) * sizeof(CRGB);
template<size_t Index, uint8_t* Data>
struct add_leds_at {
    constexpr add_leds_at() {
        size_t ledStart = 0;
        for(size_t i = 0; i < Index; ++i)
            ledStart += ledCounts[i];
        FastLED.addLeds<strip_type, ledPins[Index]>(reinterpret_cast<CRGB*>(Data), ledStart, ledCounts[Index]);
        if constexpr (Index + 1 < stripCount)
            add_leds_at<Index + 1, Data>();
    }
};

enum class DataType : uint8_t {
    Power,
    Data,
    Ping
};

uint8_t data[totalDataCount];
bool pendingShow = false;

void setPower(bool power) {
    digitalWrite(relayPin, power ? HIGH : LOW);
}

void setup() {
    pinMode(relayPin, OUTPUT);
    add_leds_at<0, data>();

    setPower(false);
    uart::begin(baudRate);

    uart::write(1);
}

uint8_t readNext() {
    while(!uart::canRead()) { }
    return uart::read();
}

void readPower() {
    setPower(readNext() != 0);
}

void readData() {
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = readNext();
    pendingShow = true;
}

void fastShow() {
    CLEDController *pCur = CLEDController::head();
    while(pCur) {
        pCur->showLeds();
        pCur = pCur->next();
    }
}

void readPing() {
    if(pendingShow)
        fastShow();
    pendingShow = false;
    uart::write(0); // pong hehe
}

void loop() {
    if(!uart::canRead())
        return;
    switch(static_cast<DataType>(uart::read())) {
        case DataType::Power: readPower();
            break;
        case DataType::Data: readData();
            break;
        case DataType::Ping: readPing();
            break;
    }
}
