#include <stdlib.h>
#include <stdio.h>
#include <array>

#include "pico/stdlib.h"
#include "pico/bootrom.h"
#include "pico/stdio/driver.h"
#include "hardware/pio.h"
#include "hardware/dma.h"
#include "hardware/clocks.h"
#include "hardware/pwm.h"
#include "ws2812.pio.h"

#include "audio/musicbox.h"
#include "audio.hpp"

#define STB_VORBIS_MAX_CHANNELS 1
#include "stb_vorbis.c"

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
constexpr uint8_t relayPin = 21;
bool powered = false;

// ----------------

constexpr size_t totalDataCount = 177 * 3 + 82 * 3 + 30 * 3;

enum class DataType : uint8_t {
    Power,
    Data,
    Ping
};

std::array<uint8_t, totalDataCount> data;

// freddor
bool freddy = false;
bool freddyShown = true;
stb_vorbis* freddyVorbis = nullptr;
constexpr uint8_t freddyBrightness = 63;
constexpr uint8_t speakerPowerPin = 22;
constexpr uint8_t speakerDataPin = 20;

void usbWrite(const uint8_t x) {
    stdio_usb.out_chars(reinterpret_cast<const char*>(&x), 1);
}
bool usbTryRead(uint8_t& x) {
    auto res = stdio_usb.in_chars(reinterpret_cast<char*>(&x), 1);
    return res != PICO_ERROR_NO_DATA;
}

void showAll() {
    for (const auto& strip : strips) {
        dma_channel_wait_for_finish_blocking(strip.m_dma);
    }
    size_t currStart = 0;
    for (const auto& strip : strips) {
        dma_channel_set_read_addr(strip.m_dma, &data[currStart], true);
        currStart += strip.m_size;
    }
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

void setPower(uint8_t value) {
    powered = value > 0;
    gpio_put(relayPin, powered);
    if (!powered) {
        for(size_t i = 0; i < totalDataCount; i++)
            data[i] = 0u;
        showAll();
    }
    audio::stop();
    freddy = value == 2;
    gpio_put(speakerPowerPin, freddy);
    if (freddy) {
        sleep_us(65535u);
        sleep_us(65535u);
        if (freddyVorbis) {
            stb_vorbis_close(freddyVorbis);
            freddyVorbis = nullptr;
        }
        int err;
        freddyVorbis = stb_vorbis_open_memory(musicbox, sizeof(musicbox), &err, nullptr);
        showFreddy();
    }
}

uint8_t readNext() {
    uint8_t b;
    while (!usbTryRead(b)) { }
    return b;
}

void readPower() {
    setPower(readNext());
}

void readData() {
    size_t currStart = 0;
    for (const auto& strip : strips) {
        dma_channel_wait_for_finish_blocking(strip.m_dma);
    }
    for (const auto& strip : strips) {
        auto* current = &data[currStart];
        int remaining = strip.m_size;
        int res;
        do {
            res = stdio_usb.in_chars(reinterpret_cast<char*>(current), remaining);
            if (res < 0)
                continue;
            remaining -= res;
            current += res;
        } while(remaining > 0);
        dma_channel_set_read_addr(strip.m_dma, &data[currStart], true);
        currStart += strip.m_size;
    }
}

absolute_time_t lastPing;
void readPing() {
    usbWrite(0); // pong hehe
    lastPing = get_absolute_time();
}

int main() {
    stdio_init_all();

    // relay
    gpio_init(relayPin);
    gpio_set_dir(relayPin, GPIO_OUT);

    for (auto& strip : strips) {
        uint offset = pio_add_program(strip.m_pio, &ws2812_program);
        ws2812_program_init(strip.m_pio, strip.m_sm, offset, strip.m_pin);
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
    setPower(0);

    //size_t t = 0;
    //while (true) {
    //    for(size_t i = 0; i < totalDataCount; i++)
    //        data[i] = (i / 3 + t) % 64;
    //    showAll();
    //    t++;
    //}

    //usbWrite(1);

    // freddy speaker
    gpio_init(speakerPowerPin);
    gpio_set_dir(speakerPowerPin, GPIO_OUT);
    audio::init(speakerDataPin, 22050);

    bool played = true;
    while (true) {
        // TODO: fix freddys audio :<
        // freddy fazbear mode har har har har har
        if (freddy) {
            if (played && freddyVorbis) {
                float pcm[AUDIO_BUFFER_SIZE];
                int n;
                n = stb_vorbis_get_samples_float_interleaved(freddyVorbis, 1, pcm, AUDIO_BUFFER_SIZE);
                if (n == 0) {
                    stb_vorbis_close(freddyVorbis);
                    freddyVorbis = nullptr;
                }
                else {
                    uint8_t samples[AUDIO_BUFFER_SIZE];
                    for (int i = 0; i < n; i++) {
                        float s = (pcm[i] + 1.f) * 0.5f * 255.f;
                        if (s > 255.f)
                            s = 255.f;
                        if (s < 0.f)
                            s = 0.f;
                        samples[i] = static_cast<uint8_t>(s);
                    }
                    audio::addSamples(samples, AUDIO_BUFFER_SIZE);
                }
            }
            played = audio::step();

            if (freddyShown)
                hideFreddy();
            else
                showFreddy();
            int waitTime = freddyShown ? rand() % 65 : rand() % 17;
            for (int i = 0; i < waitTime; i++)
                sleep_us(rand());
            //if (rand() % 100 == 0) {
            //    freddy = false;
            //    hideFreddy();
            //    audio::stop();
            //    gpio_put(speakerPowerPin, false);
            //    if (freddyVorbis) {
            //        stb_vorbis_close(freddyVorbis);
            //        freddyVorbis = nullptr;
            //    }
            //    gpio_put(relayPin, powered);
            //}
        }
        //else if (!powered) {
        //    if (rand() % 100 == 0) {
        //        freddy = true;
        //        gpio_put(relayPin, true);
        //        int err;
        //        freddyVorbis = stb_vorbis_open_memory(musicbox, sizeof(musicbox), &err, nullptr);
        //        gpio_put(speakerPowerPin, true);
        //    }
        //}

        uint8_t x;
        if (!usbTryRead(x)) {
            if (freddy)
                continue;
            // no data for more than 5 seconds
            if (absolute_time_diff_us(get_absolute_time(), lastPing) < 5000000)
                continue;
            lastPing = at_the_end_of_time;
            setPower(0);
            continue;
        }
        switch (static_cast<DataType>(x)) {
            case DataType::Power: readPower();
                break;
            case DataType::Data: readData();
                break;
            case DataType::Ping: readPing();
                break;
        }
    }
}
