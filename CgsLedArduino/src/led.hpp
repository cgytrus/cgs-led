/*
    GitHub: https://github.com/GyverLibs/microLED
    AlexGyver & Egor 'Nich1con' Zaharov, alex@alexgyver.ru
    https://alexgyver.ru/
    MIT License

    modified by ConfiG
*/
#pragma once

#include <Arduino.h>

enum M_order {
    // r=00, g=01, b=10
    ORDER_RGB = 0b000110,
    ORDER_GRB = 0b010010,
};

struct led_data {
    uint8_t pin;
    size_t size;
    constexpr led_data(uint8_t pin, size_t count) : pin(pin), size(count * 3) { }
};

struct pin_data {
    size_t start, size;
    uint8_t pin;
    volatile uint8_t* _dat_port;
    uint8_t _dat_mask, _mask_h, _mask_l;

    pin_data() {}

    pin_data(uint8_t pin, size_t start, size_t size) {
        this->pin = pin;
        this->size = size;
        this->start = start;
        _dat_mask = digitalPinToBitMask(pin);
        _dat_port = portOutputRegister(digitalPinToPort(pin));
        *portModeRegister(digitalPinToPort(pin)) |= _dat_mask;
    }

    inline void update() {
        _mask_h = _dat_mask | *_dat_port;
        _mask_l = ~_dat_mask & *_dat_port;
    }
};

template<M_order order, pin_data* pins, size_t count, uint8_t* data>
class microLed {
public:
    microLed() {}

    __attribute__((optimize("unroll-loops")))
    static inline void show() {
        uint8_t sregSave = SREG;
        cli();

        for(size_t i = 0; i < count; i++)
            pins[i].update();
        for(size_t i = 0; i < count; i++) {
            pin_data pin = pins[i];
            for(size_t j = 0; j < pin.size; j += 3) {
                if constexpr (order == M_order::ORDER_RGB) {
                    sendRaw(data[pin.start + j]);
                    sendRaw(data[pin.start + j + 1]);
                    sendRaw(data[pin.start + j + 2]);
                }
                else if constexpr (order == M_order::ORDER_GRB) {
                    sendRaw(data[pin.start + j + 1]);
                    sendRaw(data[pin.start + j]);
                    sendRaw(data[pin.start + j + 2]);
                }
            }
        }

        SREG = sregSave;
    }

    // tick = 0.0625us

    __attribute__((optimize("unroll-loops")))
    static inline void sendRaw(uint8_t x) {
        for(size_t i = 0; i < 8; ++i) {
            asm volatile
            (
            "ST X, %[SET_H]   \n\t"
            "SBRS %[DATA], 7  \n\t"
            "ST X, %[SET_L]   \n\t"
            "LSL  %[DATA]     \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "NOP              \n\t"
            "ST X, %[SET_L]   \n\t"
            :
            :[DATA] "r" (x),
            [SET_H] "r" (pins[0]._mask_h),
            [SET_L] "r" (pins[0]._mask_l),
            "x" (pins[0]._dat_port)
            );
        }
    }
};
