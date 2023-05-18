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
    size_t start, end;
    uint8_t pin;
    volatile uint8_t* _dat_port;
    uint8_t _dat_mask, _mask_h, _mask_l;

    pin_data() {}

    pin_data(uint8_t pin, size_t start, size_t size) {
        this->pin = pin;
        this->start = start;
        this->end = start + size;
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

        // TODO: implement counts other than 3
        pin_data pin0 = pins[0];
        pin_data pin1 = pins[1];
        pin_data pin2 = pins[2];
        size_t i0 = pin0.start;
        for(size_t i2 = pin2.start; i2 < pin2.end; i2 += 3) {
            send<0, 2>(i0, i2);
            i0 += 3;
        }
        for(; i0 < pin0.end; i0 += 3)
            send<0>(i0);
        for(size_t i1 = pin1.start; i1 < pin1.end; i1 += 3)
            send<1>(i1);
        //for(; i1 < pin1.end; i1 += 3) {
        //    send<0, 1>(i0, i1);
        //    i0 += 3;
        //}
        //for(; i2 < pin2.end; i2 += 3)
        //    send<2>(i2);

        SREG = sregSave;
    }

    // cycle = 0.0625us
    // 0 = 0.25+0.7 = 5+12
    // 1 = 0.65+0.3 = 12+5

    // proper timings (5-12/12-5)
    /*
            "NOP                       \n\t" // 1c
            "ST X, %[SET_H_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "SBRC %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_H_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
            "NOP                       \n\t" // 1c
    */

    template<size_t pin0>
    static inline void send(size_t i0) {
        if constexpr (order == M_order::ORDER_RGB) {
            sendRaw<pin0>(data[i0]);
            sendRaw<pin0>(data[i0 + 1]);
            sendRaw<pin0>(data[i0 + 2]);
        }
        else if constexpr (order == M_order::ORDER_GRB) {
            sendRaw<pin0>(data[i0 + 1]);
            sendRaw<pin0>(data[i0]);
            sendRaw<pin0>(data[i0 + 2]);
        }
    }

    template<size_t pin0, size_t pin1>
    static inline void send(size_t i0, size_t i1) {
        if constexpr (order == M_order::ORDER_RGB) {
            sendRaw<pin0, pin1>(data[i0], data[i1]);
            sendRaw<pin0, pin1>(data[i0 + 1], data[i1 + 1]);
            sendRaw<pin0, pin1>(data[i0 + 2], data[i1 + 2]);
        }
        else if constexpr (order == M_order::ORDER_GRB) {
            sendRaw<pin0, pin1>(data[i0 + 1], data[i1 + 1]);
            sendRaw<pin0, pin1>(data[i0], data[i1]);
            sendRaw<pin0, pin1>(data[i0 + 2], data[i1 + 2]);
        }
    }

    template<size_t pin0>
    //__attribute__((optimize("unroll-loops")))
    static inline void sendRaw(uint8_t x) {
        //for(size_t i = 0; i < 8; ++i) {
            asm volatile
            (
            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_0_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_0_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_1_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_1_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_2_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_2_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_3_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_3_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_4_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_4_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_5_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_5_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_6_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_6_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "LDI r20, 3                \n\t" // 1c
            "_DELAY_LOOP_7_%=:         \n\t"
            "DEC r20                   \n\t" // 1c
            "BRNE _DELAY_LOOP_7_%=     \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "NOP                       \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            :
            : "x" (pins[pin0]._dat_port),
              [DATA_0] "r" (x),
              [SET_H_0] "r" (pins[pin0]._mask_h),
              [SET_L_0] "r" (pins[pin0]._mask_l)
            : "r20"
            );
        //}
    }

    template<size_t pin0, size_t pin1>
    //__attribute__((optimize("unroll-loops")))
    static inline void sendRaw(uint8_t x, uint8_t y) {
        //for(size_t i = 0; i < 8; ++i) {
            asm volatile
            (
            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c

            "ST X, %[SET_H_0]          \n\t" // 2c
            "SBRS %[DATA_0], 7         \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "ST Z, %[SET_H_1]          \n\t" // 2c
            "SBRS %[DATA_1], 7         \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            "LSL %[DATA_0]             \n\t" // 1c
            "ST X, %[SET_L_0]          \n\t" // 2c
            "NOP                       \n\t" // 1c
            "LSL %[DATA_1]             \n\t" // 1c
            "ST Z, %[SET_L_1]          \n\t" // 2c
            :
            : "x" (pins[pin0]._dat_port),
              "z" (pins[pin1]._dat_port),
              [DATA_0] "r" (x),
              [DATA_1] "r" (y),
              [SET_H_0] "r" (pins[pin0]._mask_h),
              [SET_H_1] "r" (pins[pin1]._mask_h),
              [SET_L_0] "r" (pins[pin0]._mask_l),
              [SET_L_1] "r" (pins[pin1]._mask_l)
            );
        //}
    }
};
