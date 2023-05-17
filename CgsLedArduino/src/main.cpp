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
constexpr led_data strips[stripCount] = { led_data(5, 177), led_data(6, 82), led_data(9, 30) };

constexpr int32_t baudRate = 1000000;

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

microLed<stripsOrder, pins, stripCount, data> led;

void setup() {
    pinMode(relayPin, OUTPUT);
    digitalWrite(relayPin, LOW);
    add_leds_at<0, data, strips, pins>();
    uart::begin(baudRate);
    uart::write(1);
}

uint8_t readNext() {
    while(!uart::canRead()) { }
    return uart::read();
}

void readPower() {
    digitalWrite(relayPin, readNext() == 0 ? LOW : HIGH);
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
