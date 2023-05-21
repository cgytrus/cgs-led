﻿using CgsLedController;

using CgsLedService.Helpers;

namespace CgsLedService.Modes.Vu;

public sealed class VuMode : LedMode<VuMode.Configuration> {
    public record Configuration(
        MusicColors colors,
        int sampleCount = 16,
        float falloffSpeed = 1f);

    private readonly AudioCapture _capture;

    private readonly float[] _samples = new float[2];
    private readonly int[] _sampleCounts = new int[2];
    private readonly float[] _display = new float[2];
    private readonly TimeSpan[] _lastDisplayTimes = new TimeSpan[2];

    public VuMode(AudioCapture capture, Configuration config) : base(config) => _capture = capture;

    protected override void Main() => _capture.AddListener(AddSample);
    public override void StopMode() => _capture.RemoveListener(AddSample);

    private void AddSample(float sample, int channel, TimeSpan time) {
        if(_samples[channel] >= config.sampleCount) {
            float display = MoreMath.SampleToUnitDb(MathF.Sqrt(_samples[channel] / _sampleCounts[channel]));
            if(GetDisplay(channel) <= display) {
                _display[channel] = display;
                _lastDisplayTimes[channel] = VuMode.time;
            }

            _samples[channel] = 0f;
            _sampleCounts[channel] = 0;
        }
        _samples[channel] += sample * sample;
        _sampleCounts[channel]++;
    }

    private float GetDisplay(int channel) =>
        _display[channel] - config.falloffSpeed * (float)(time - _lastDisplayTimes[channel]).TotalSeconds;

    public override void Update() { }
    public override void Draw(int strip) {
        int ledCount = writer.ledCounts[strip];
        int rightCount = writer.halfLedCounts[strip];
        int leftCount = ledCount - rightCount;

        Span<float> hues = stackalloc float[ledCount];
        Span<float> values = stackalloc float[ledCount];

        for(int i = 0; i < leftCount; i++) {
            hues[i] = (float)i / leftCount;
            values[i] = Math.Clamp(GetDisplay(0) * leftCount - i, 0f, 1f);
        }
        for(int i = 0; i < rightCount; i++) {
            hues[ledCount - i - 1] = (float)i / rightCount;
            values[ledCount - i - 1] = Math.Clamp(GetDisplay(1) * rightCount - i, 0f, 1f);
        }

        config.colors.Write(writer, strip, (float)time.TotalSeconds, hues, values);
    }
}
