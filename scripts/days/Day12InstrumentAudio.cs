using Godot;
using System;

namespace NewGameProject.Days;

// Short synthesized play sounds; no external recordings or music dependencies.
public static class Day12InstrumentAudio
{
    public static AudioStreamWav Create(int instrument)
    {
        const int sampleRate = 22050;
        const double duration = 0.55;
        int count = (int)(sampleRate * duration);
        byte[] pcm = new byte[count * 2];
        double[] frequencies = [90, 659.25, 523.25, 392, 329.63];
        Random noise = new(3200 + instrument);
        for (int index = 0; index < count; index++)
        {
            double t = (double)index / sampleRate;
            double phase = 2 * Math.PI * frequencies[instrument] * t;
            double attack = Math.Min(1, t / 0.012);
            double signal = instrument switch
            {
                0 => Math.Sin(2 * Math.PI * (100 * t - 35 * t * t)) * Math.Exp(-t * 10)
                    + (noise.NextDouble() * 2 - 1) * Math.Exp(-t * 35) * 0.2,
                1 => (Math.Sin(phase) + 0.24 * Math.Sin(phase * 3.98)) * Math.Exp(-t * 7),
                2 => (Math.Sin(phase) + 0.28 * Math.Sin(phase * 2) + 0.12 * Math.Sin(phase * 3)) * Math.Exp(-t * 5),
                3 => (Math.Sin(phase) + 0.32 * Math.Sin(phase * 2) + 0.2 * Math.Sin(phase * 3)) * Math.Sin(Math.PI * t / duration),
                _ => (Math.Sin(phase) + 0.35 * Math.Sin(phase * 2) + 0.18 * Math.Sin(phase * 4)) * Math.Exp(-t * 8)
            };
            double fade = Math.Min(1, (duration - t) / 0.04);
            short sample = (short)(Math.Clamp(signal * attack * fade * 0.3, -1, 1) * short.MaxValue);
            pcm[index * 2] = (byte)(sample & 255);
            pcm[index * 2 + 1] = (byte)((sample >> 8) & 255);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = sampleRate, Stereo = false, Data = pcm };
    }
}

