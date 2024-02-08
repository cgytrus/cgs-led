#include <stdlib.h>
#include <stdio.h>
#include <array>

#include "pico/stdlib.h"
#include "pico/bootrom.h"
#include "pico/stdio/driver.h"
#include "hardware/pio.h"
#include "hardware/dma.h"
#include "hardware/clocks.h"
#include "ws2812.pio.h"

// --- SETTINGS ---

struct led_data {
    uint8_t m_pin;
    size_t m_size;
    PIO m_pio;
    uint32_t m_sm;
    uint32_t m_dma;

    led_data(uint8_t pin, size_t count, PIO ppio, uint32_t sm) :
        m_pin(pin), m_size(count * 3), m_pio(ppio), m_sm(sm) { }

    void put(uint32_t x) const {
        pio_sm_put_blocking(m_pio, m_sm, x);
    }
};

constexpr size_t stripCount = 3;
static std::array<led_data, stripCount> strips = {
    led_data(11, 177, pio0, 0),
    led_data(12, 82, pio1, 0),
    led_data(13, 30, pio1, 1)
};

// ----------------

constexpr size_t totalDataCount = 177 * 3 + 82 * 3 + 30 * 3;

enum class DataType : uint8_t {
    Power,
    Data,
    Ping
};

uint8_t data[totalDataCount];
bool pendingShow = false;

// freddor
bool freddy = false;
bool freddyShown = true;
const uint8_t freddyBrightness = 63;
bool wasPowered = false;

void usbWrite(const uint8_t x) {
    stdio_usb.out_chars(reinterpret_cast<const char*>(&x), 1);
}
bool usbTryRead(uint8_t& x) {
    auto res = stdio_usb.in_chars(reinterpret_cast<char*>(&x), 1);
    return res != PICO_ERROR_NO_DATA;
}

void showAll() {
    //size_t di = 0;
    //for (const auto& strip : strips) {
    //    for (size_t i = 0; i < strip.m_size; i += 3) {
    //        strip.put(data[di + 1] << 24 | data[di] << 16 | data[di + 2] << 8);
    //        di += 3;
    //    }
    //}
    size_t currStart = 0;
    for (const auto& strip : strips) {
        dma_channel_set_read_addr(strip.m_dma, &data[currStart], true);
        currStart += strip.m_size;
    }
    //for (const auto& strip : strips) {
    //    dma_channel_wait_for_finish_blocking(strip.m_dma);
    //}
    // can't wait for everything cuz too slow and can't not wait cuz
    // it artifacts a little at the end of the longest strip
    dma_channel_wait_for_finish_blocking(strips[2].m_dma);
}

void showFreddy() {
    size_t ledIndex = strips[0].m_size + strips[1].m_size / 3 / 2 * 3 - 10 * 3;
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
    showAll();
    freddyShown = true;
}
void hideFreddy() {
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = 0u;
    showAll();
    freddyShown = false;
}

uint8_t readNext() {
    uint8_t b;
    while (!usbTryRead(b)) { }
    return b;
}

void readPower() {
    auto value = readNext();
    // TODO digitalWrite(relayPin, value == 0 ? LOW : HIGH);
    if (value == 0) {
        for(size_t i = 0; i < totalDataCount; i++)
            data[i] = 0u;
        showAll();
    }
    wasPowered = value != 0;
    freddy = value == 2;
    if(freddy) {
        sleep_us(65535u);
        sleep_us(65535u);
        showFreddy();
    }
}

void readData() {
    for(size_t i = 0; i < totalDataCount; i++) {
        if (i % 3 == 0)
            data[i + 1] = readNext();
        else if (i % 3 == 1)
            data[i - 1] = readNext();
        else
            data[i] = readNext();
    }
    pendingShow = true;
}

absolute_time_t lastPing;
void readPing() {
    if(pendingShow)
        showAll();
    pendingShow = false;
    usbWrite(0); // pong hehe
    lastPing = get_absolute_time();
}

int main() {
    stdio_init_all();

    for (auto& strip : strips) {
        uint offset = pio_add_program(strip.m_pio, &ws2812_program);
        // got 440000 from trial and error..
        // TODO: actually calculate this properly
        ws2812_program_init(strip.m_pio, strip.m_sm, offset, strip.m_pin, 440000.f);
        strip.m_dma = dma_claim_unused_channel(true);
        dma_channel_config c = dma_channel_get_default_config(strip.m_dma);
        channel_config_set_transfer_data_size(&c, DMA_SIZE_8);
        channel_config_set_read_increment(&c, true);
        channel_config_set_write_increment(&c, false);
        channel_config_set_dreq(&c, pio_get_dreq(strip.m_pio, strip.m_sm, true));
        dma_channel_configure(strip.m_dma, &c,
            &strip.m_pio->txf[strip.m_sm],
            nullptr,
            strip.m_size,
            false
        );
    }

    // reset leds
    for(size_t i = 0; i < totalDataCount; i++)
        data[i] = 0u;
    showAll();
    //size_t t = 0;
    //while (true) {
    //    for(size_t i = 0; i < totalDataCount; i++)
    //        data[i] = (i / 3 + t) % 64;
    //    showAll();
    //    t++;
    //}
    //usbWrite(1);

    while (true) {
        // freddy fazbear mode har har har har har
        if(freddy) {
            if(freddyShown)
                hideFreddy();
            else
                showFreddy();
            int waitTime = freddyShown ? rand() % 65 : rand() % 17;
            for (int i = 0; i < waitTime; i++)
                sleep_us(rand());
            if (rand() % 100 == 0) {
                freddy = false;
                hideFreddy();
                // TODO digitalWrite(relayPin, wasPowered ? HIGH : LOW);
            }
        }

        uint8_t x;
        if(!usbTryRead(x)) {
            // no data for more than 5 seconds
            if (to_ms_since_boot(get_absolute_time()) - to_ms_since_boot(lastPing) < 5000)
                continue;
            lastPing = to_ms_since_boot(get_absolute_time()) + 999999;
            for(size_t i = 0; i < totalDataCount; i++)
                data[i] = 0u;
            showAll();
            continue;
        }
        switch(static_cast<DataType>(x)) {
            case DataType::Power: readPower();
                break;
            case DataType::Data: readData();
                break;
            case DataType::Ping: readPing();
                break;
        }
    }
}
