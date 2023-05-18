#pragma once

#include "Arduino.h"

// buffers are for the weak
namespace uart {
    void begin() {
        UBRR0 = 0;
        UCSR0A = (1 << U2X0);
        UCSR0B = (1 << TXEN0) | (1 << RXEN0);
        UCSR0C = (1 << UCSZ00) | (1 << UCSZ01);
    }

    inline bool canRead() {
        return UCSR0A & (1 << RXC0);
    }

    inline uint8_t read() {
        return UDR0;
    }

    inline void write(uint8_t data) {
        while(!(UCSR0A & (1 << UDRE0))) { }
        UDR0 = data;
    }
};
