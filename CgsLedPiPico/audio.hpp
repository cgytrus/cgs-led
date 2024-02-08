#pragma once

#define AUDIO_BUFFER_SIZE 1024

#include <stdint.h>

namespace audio {
    void init(int pin, int frequency);
    uint8_t* getBuffer();
    void addSamples(const uint8_t* samples, size_t count);
    void stop();
    bool step();
}
