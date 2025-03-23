#include <array>
#include <queue>
#include <string.h>

#include "pico/stdlib.h"
#include "hardware/dma.h"
#include "hardware/irq.h"
#include "hardware/pwm.h"
#include "hardware/sync.h"
#include "hardware/clocks.h"

#include "audio.hpp"

#define REPETITION_RATE 4

uint32_t sample = 0;
uint32_t* samplePtr = &sample;
int pwmDma;
int triggerDma;
int sampleDma;

uint8_t buffers[2][AUDIO_BUFFER_SIZE];
volatile int currBuf = 0;
volatile int lastBuf = 0;

std::queue<uint8_t> audioSamples;

static void __isr __time_critical_func(dma_handler)() {
    currBuf = 1 - currBuf;
    dma_hw->ch[sampleDma].al1_read_addr = (intptr_t)&buffers[currBuf][0];
    dma_hw->ch[triggerDma].al3_read_addr_trig = (intptr_t)&samplePtr;

    dma_hw->ints1 = 1u << triggerDma;
}

void audio::init(int pin, int frequency) {
    gpio_set_function(pin, GPIO_FUNC_PWM);

    int pinSlice = pwm_gpio_to_slice_num(pin);
    int pinChannel = pwm_gpio_to_channel(pin);

    float div = frequency_count_khz(CLOCKS_FC0_SRC_VALUE_CLK_SYS) * 1000.f / 254.f / frequency / REPETITION_RATE;

    pwm_config config = pwm_get_default_config();
    pwm_config_set_clkdiv(&config, div);
    pwm_config_set_wrap(&config, 254);
    pwm_init(pinSlice, &config, true);

    pwmDma = dma_claim_unused_channel(true);
    triggerDma = dma_claim_unused_channel(true);
    sampleDma = dma_claim_unused_channel(true);

    // setup PWM DMA channel
    dma_channel_config pwmDmaConfig = dma_channel_get_default_config(pwmDma);
    channel_config_set_transfer_data_size(&pwmDmaConfig, DMA_SIZE_32); // transfer 32 bits at a time
    channel_config_set_read_increment(&pwmDmaConfig, false); // always read from the same address
    channel_config_set_write_increment(&pwmDmaConfig, false); // always write to the same address
    channel_config_set_chain_to(&pwmDmaConfig, sampleDma); // trigger sample DMA channel when done
    channel_config_set_dreq(&pwmDmaConfig, DREQ_PWM_WRAP0 + pinSlice); // transfer on PWM cycle end
    dma_channel_configure(pwmDma, &pwmDmaConfig,
        &pwm_hw->slice[pinSlice].cc, // write to PWM slice CC register
        &sample, // read from sample
        REPETITION_RATE, // transfer once per desired sample repetition
        false // don't start yet
    );

    // setup trigger DMA channel
    dma_channel_config triggerDmaConfig = dma_channel_get_default_config(triggerDma);
    channel_config_set_transfer_data_size(&triggerDmaConfig, DMA_SIZE_32); // transfer 32-bits at a time
    channel_config_set_read_increment(&triggerDmaConfig, false); // always read from the same address
    channel_config_set_write_increment(&triggerDmaConfig, false); // always write to the same address
    channel_config_set_dreq(&triggerDmaConfig, DREQ_PWM_WRAP0 + pinSlice); // transfer on PWM cycle end
    dma_channel_configure(triggerDma, &triggerDmaConfig,
        &dma_hw->ch[pwmDma].al3_read_addr_trig, // write to PWM DMA channel read address trigger
        &samplePtr, // read from location containing the address of sample
        REPETITION_RATE * AUDIO_BUFFER_SIZE, // trigger once per audio sample per repetition rate
        false // don't start yet
    );
    dma_channel_set_irq1_enabled(triggerDma, true); // fire interrupt when trigger DMA channel is done
    irq_set_exclusive_handler(DMA_IRQ_1, dma_handler);
    irq_set_enabled(DMA_IRQ_1, true);

    // setup sample DMA channel
    dma_channel_config sampleDmaConfig = dma_channel_get_default_config(sampleDma);
    channel_config_set_transfer_data_size(&sampleDmaConfig, DMA_SIZE_8);  // transfer 8-bits at a time
    channel_config_set_read_increment(&sampleDmaConfig, true); // increment read address to go through audio buffer
    channel_config_set_write_increment(&sampleDmaConfig, false); // always write to the same address
    dma_channel_configure(sampleDma, &sampleDmaConfig,
        (char*)&sample + 2 * pinChannel, // write to sample
        &buffers[0][0], // read from audio buffer
        1, // only do one transfer (once per PWM DMA completion due to chaining)
        false // don't start yet
    );


    // clear audio buffers
    memset(buffers[0], 128, AUDIO_BUFFER_SIZE);
    memset(buffers[1], 128, AUDIO_BUFFER_SIZE);

    // kick things off with the trigger DMA channel
    dma_channel_start(triggerDma);
}

uint8_t* audio::getBuffer() {
    if (lastBuf == currBuf)
        return nullptr;
    auto buf = buffers[lastBuf];
    lastBuf = currBuf;
    return buf;
}

void audio::addSamples(const uint8_t* samples, size_t count) {
    for (size_t i = 0; i < count; i++)
        audioSamples.push(samples[i]);
}

void audio::stop() {
    while (!audioSamples.empty())
        audioSamples.pop();
    for (int i = 0; i < AUDIO_BUFFER_SIZE; i++) {
        buffers[0][i] = 0;
        buffers[1][i] = 0;
    }
}

bool audio::step() {
    uint8_t* buffer = getBuffer();
    if (!buffer)
        return false;

    for (int i = 0; i < AUDIO_BUFFER_SIZE; i++) {
        if (audioSamples.empty()) {
            buffer[i] = 0;
            continue;
        }
        buffer[i] = audioSamples.front();
        audioSamples.pop();
    }

    return true;
}
