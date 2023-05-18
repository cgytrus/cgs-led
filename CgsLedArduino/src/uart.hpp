/*
    AlexGyver & Egor 'Nich1con' Zaharov, alex@alexgyver.ru
    https://alexgyver.ru/
    MIT License

    Modified by ConfiG
*/

#pragma once

#include "Arduino.h"

namespace uart {
    #define UART_RX_BUFFER_SIZE 64
    static volatile char _UART_RX_BUFFER[UART_RX_BUFFER_SIZE];
    static volatile uint8_t _UART_RX_BUFFER_HEAD;
    static volatile uint8_t _UART_RX_BUFFER_TAIL;

    #if defined(__AVR_ATmega2560__)
    #define	USARTx_RX_vect		USART0_RX_vect
    #else
    #define	USARTx_RX_vect		USART_RX_vect
    #endif

    // =========================== INIT ========================
    void begin() {
        UBRR0 = 0;
        UCSR0A = (1 << U2X0);
        UCSR0B = ((1 << TXEN0) | (1 << RXEN0) | (1 << RXCIE0));
        UCSR0C = ((1 << UCSZ01) | (1 << UCSZ00));
        _UART_RX_BUFFER_HEAD = _UART_RX_BUFFER_TAIL = 0;
    }

    // =========================== READ ============================
    ISR(USARTx_RX_vect) {
        uint8_t data = UDR0;
        // read a 0 if parity check didnt pass
        if(UCSR0A & (1 << UPE0))
            data = 0;

        uint8_t i = _UART_RX_BUFFER_HEAD + 1 == UART_RX_BUFFER_SIZE ? 0 : _UART_RX_BUFFER_HEAD + 1;
        if(i != _UART_RX_BUFFER_TAIL) {
            _UART_RX_BUFFER[_UART_RX_BUFFER_HEAD] = data;
            _UART_RX_BUFFER_HEAD = i;
        }
    }

    bool canRead() {
        return _UART_RX_BUFFER_HEAD != _UART_RX_BUFFER_TAIL;
    }

    uint8_t read() {
        uint8_t c = _UART_RX_BUFFER[_UART_RX_BUFFER_TAIL];
        if(++_UART_RX_BUFFER_TAIL >= UART_RX_BUFFER_SIZE)
            _UART_RX_BUFFER_TAIL = 0;
        return c;
    }

    // ====================== WRITE ===========================

    // buffers are for the weak
    void write(uint8_t data) {
        while(!(UCSR0A & (1 << UDRE0))) { }
        UDR0 = data;
    }
};
