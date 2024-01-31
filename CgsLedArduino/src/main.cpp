#define NO_CORRECTION 1
#define NO_CLOCK_CORRECTION 1

#include <Arduino.h>
#include "led.hpp"
#include "uart.hpp"

// --- SETTINGS ---

constexpr uint8_t relayPin = 3;

constexpr M_order stripsOrder = M_order::ORDER_GRB;
constexpr size_t stripCount = 3;
// have to be sorted by led count in descending order!
constexpr led_data strips[stripCount] = { led_data(5, 177), led_data(9, 82), led_data(6, 30) };

// ----------------

// compile-time stuff collapse these
template<size_t Size>
constexpr size_t arraySum(const led_data (&arr)[Size]) {
    size_t ret = 0;
    for(size_t i = 0; i < Size; ++i)
        ret += arr[i].size;
    return ret;
}
constexpr size_t totalDataCount = arraySum(strips);

template<size_t Index, uint8_t* Data, const led_data* Strips, pin_data* Pins>
struct add_leds_at {
    constexpr add_leds_at() {
        size_t ledStart = 0;
        for(size_t i = 0; i < Index; ++i)
            ledStart += Strips[i].size;
        led_data ledData = Strips[Index];
        Pins[Index] = pin_data(ledData.pin, ledStart, ledData.size);
        if constexpr (Index + 1 < stripCount)
            add_leds_at<Index + 1, Data, Strips, Pins>();
    }
};

enum class DataType : uint8_t {
    Power,
    Data,
    Ping
};

pin_data pins[stripCount];
uint8_t data[totalDataCount];
bool pendingShow = false;

// freddor
bool freddy = false;
bool freddyShown = true;
const uint8_t freddyBrightness = 63;
bool wasPowered = false;

microLed<stripsOrder, pins, stripCount, data> led;

void setup() {
    pinMode(relayPin, OUTPUT);
    digitalWrite(relayPin, LOW);
    add_leds_at<0, data, strips, pins>();
    uart::begin();
    uart::write(1);
    cli();
}

void showFreddy() {
    size_t ledIndex = strips[0].size + strips[1].size / 3 / 2 * 3 - 10 * 3;
    size_t ledIndex0 = ledIndex - 4 * 3;
    size_t ledIndex1 = ledIndex + 4 * 3;
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = 0u;
    data[ledIndex0] = freddyBrightness;
    data[ledIndex0 + 1] = freddyBrightness;
    data[ledIndex0 + 2] = freddyBrightness;
    data[ledIndex1] = freddyBrightness;
    data[ledIndex1 + 1] = freddyBrightness;
    data[ledIndex1 + 2] = freddyBrightness;
    led.show();
    freddyShown = true;
}
void hideFreddy() {
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = 0u;
    led.show();
    freddyShown = false;
}

uint8_t readNext() {
    while(!uart::canRead()) { }
    return uart::read();
}

void readPower() {
    auto value = readNext();
    digitalWrite(relayPin, value == 0 ? LOW : HIGH);
    wasPowered = value != 0;
    freddy = value == 2;
    if(freddy) {
        delayMicroseconds(65535u);
        delayMicroseconds(65535u);
        showFreddy();
    }
}

void readData() {
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = readNext();
    pendingShow = true;
}

void readPing() {
    if(pendingShow)
        led.show();
    pendingShow = false;
    uart::write(0); // pong hehe
}

void loop() {
    // freddy fazbear mode har har har har har
    if(freddy) {
        if(freddyShown)
            hideFreddy();
        else
            showFreddy();
        int waitTime = freddyShown ? rand() % 65 : rand() % 17;
        for (int i = 0; i < waitTime; i++)
            delayMicroseconds(rand());
        if (rand() % 100 == 0) {
            freddy = false;
            digitalWrite(relayPin, wasPowered ? HIGH : LOW);
        }
    }

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
