/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * agc_float_tests.cs - tests for floating point AGC for modems.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2024 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License version 2, as
 * published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * Managed C# port: all target/input sweeps, five-block settling intervals,
 * signal-detection hysteresis and the 0.3 dB validation are retained.
 */

#nullable enable

using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.AgcFloatApi;
using static TKFaxEngine.Audio.Dds;
using static TKFaxEngine.Audio.PowerMeterApi;

namespace TKFaxEngineTest.Audio.Test;

public static class agc_float_tests {
    private const string OUT_FILE_NAME = "agc_float.wav";
    private const int BLOCK_LENGTH = 160;

    public static int main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        try {
            return Run();
        } catch (AgcFloatTestFailureException) {
            return 2;
        }
    }

    public static int Run() {
        var pre_meter = power_meter_init(null, 5) ?? throw new InvalidOperationException("power_meter_init failed.");
        var post_meter = power_meter_init(null, 5) ?? throw new InvalidOperationException("power_meter_init failed.");
        int phase_rate = dds_phase_rate(768.0f);
        uint phase_acc = 0;
        float[] fbuf = new float[BLOCK_LENGTH];
        short[] buf = new short[BLOCK_LENGTH];
        short[] audio = new short[2 * BLOCK_LENGTH];

        using var outhandle = new Pcm16WaveWriter(OUT_FILE_NAME, 2, 8000);

        float target_level;
        float signal_level;
        bool signal_present = false;

        for (target_level = -50.0f; target_level < 3.0f; target_level += 1.0f) {
            for (signal_level = -50.0f; signal_level < 3.0f; signal_level += 1.0f) {
                pre_meter = power_meter_init(pre_meter, 5)!;
                post_meter = power_meter_init(post_meter, 5)!;
                using AgcFloatDescriptor desc = agcf_make_descriptor(null, target_level, -45.0f, -48.0f, 5, 5)
                    ?? throw new InvalidOperationException("agcf_make_descriptor failed.");
                using AgcFloatState agc = agcf_init(null, desc);
                short scale = dds_scaling_dbm0(signal_level);

                for (int j = 0; j < 5; j++) {
                    for (int i = 0; i < BLOCK_LENGTH; i++) {
                        buf[i] = dds_mod(ref phase_acc, phase_rate, scale, 0);
                        audio[2 * i] = buf[i];
                        _ = power_meter_update(pre_meter, buf[i]);
                    }

                    signal_present = agcf_from_int16_rx(agc, fbuf, buf, BLOCK_LENGTH);
                    CheckSignalPresence(j, signal_level, signal_present);

                    for (int i = 0; i < BLOCK_LENGTH; i++) {
                        audio[2 * i + 1] = FloatToInt16(fbuf[i]);
                        _ = power_meter_update(post_meter, audio[2 * i + 1]);
                    }
                    outhandle.WriteFrames(audio);
                }

                ValidateLevel(signal_present, pre_meter, post_meter, target_level, agc);
            }
        }

        pre_meter = power_meter_init(pre_meter, 5)!;
        post_meter = power_meter_init(post_meter, 5)!;
        using (AgcFloatDescriptor desc = agcf_make_descriptor(null, target_level, -45.0f, -48.0f, 5, 5)
            ?? throw new InvalidOperationException("agcf_make_descriptor failed."))
        using (AgcFloatState agc = agcf_init(null, desc)) {
            for (signal_level = -55.0f; signal_level < -30.0f; signal_level += 1.0f) {
                short scale = dds_scaling_dbm0(signal_level);
                for (int j = 0; j < 5; j++) {
                    FillIntegerTone(buf, audio, pre_meter, ref phase_acc, phase_rate, scale);
                    signal_present = agcf_from_int16_rx(agc, fbuf, buf, BLOCK_LENGTH);
                    if (j > 0) {
                        if (signal_level > -45.0f && !signal_present)
                            Fail($"At {signal_level}dBm0 signal is not present");
                        if (signal_level <= -45.0f && signal_present)
                            Fail($"At {signal_level}dBm0 signal is present");
                    }
                    StoreFloatOutput(fbuf, audio, post_meter);
                    outhandle.WriteFrames(audio);
                }
            }

            for (signal_level = -30.0f; signal_level > -55.0f; signal_level -= 1.0f) {
                short scale = dds_scaling_dbm0(signal_level);
                for (int j = 0; j < 5; j++) {
                    FillIntegerTone(buf, audio, pre_meter, ref phase_acc, phase_rate, scale);
                    signal_present = agcf_from_int16_rx(agc, fbuf, buf, BLOCK_LENGTH);
                    if (j > 0) {
                        if (signal_level < -48.0f && signal_present)
                            Fail($"At {signal_level}dBm0 signal is present");
                        if (signal_level >= -48.0f && !signal_present)
                            Fail($"At {signal_level}dBm0 signal is not present");
                    }
                    StoreFloatOutput(fbuf, audio, post_meter);
                    outhandle.WriteFrames(audio);
                }
            }
        }

        for (target_level = -50.0f; target_level < 3.0f; target_level += 1.0f) {
            for (signal_level = -50.0f; signal_level < 3.0f; signal_level += 1.0f) {
                pre_meter = power_meter_init(pre_meter, 5)!;
                post_meter = power_meter_init(post_meter, 5)!;
                using AgcFloatDescriptor desc = agcf_make_descriptor(null, target_level, -45.0f, -48.0f, 5, 5)
                    ?? throw new InvalidOperationException("agcf_make_descriptor failed.");
                using AgcFloatState agc = agcf_init(null, desc);
                float scale = dds_scaling_dbm0(signal_level);

                for (int j = 0; j < 5; j++) {
                    for (int i = 0; i < BLOCK_LENGTH; i++) {
                        fbuf[i] = dds_modf(ref phase_acc, phase_rate, scale, 0);
                        audio[2 * i] = FloatToInt16(fbuf[i]);
                        _ = power_meter_update(pre_meter, audio[2 * i]);
                    }

                    signal_present = agcf_rx(agc, fbuf, fbuf, BLOCK_LENGTH);
                    CheckSignalPresence(j, signal_level, signal_present);
                    StoreFloatOutput(fbuf, audio, post_meter);
                    outhandle.WriteFrames(audio);
                }

                ValidateLevel(signal_present, pre_meter, post_meter, target_level, agc);
            }
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void FillIntegerTone(short[] buf, short[] audio, PowerMeterState meter, ref uint phase, int phaseRate, short scale) {
        for (int i = 0; i < BLOCK_LENGTH; i++) {
            buf[i] = dds_mod(ref phase, phaseRate, scale, 0);
            audio[2 * i] = buf[i];
            _ = power_meter_update(meter, buf[i]);
        }
    }

    private static void StoreFloatOutput(float[] fbuf, short[] audio, PowerMeterState meter) {
        for (int i = 0; i < BLOCK_LENGTH; i++) {
            audio[2 * i + 1] = FloatToInt16(fbuf[i]);
            _ = power_meter_update(meter, audio[2 * i + 1]);
        }
    }

    private static void CheckSignalPresence(int block, float signalLevel, bool present) {
        if (block <= 0)
            return;
        if (signalLevel > -45.0f && !present)
            Fail($"Signal not present at {signalLevel}dBm0");
        if (signalLevel <= -45.0f && present)
            Fail($"Signal present at {signalLevel}dBm0");
    }

    private static void ValidateLevel(bool present, PowerMeterState pre, PowerMeterState post, float target, AgcFloatState agc) {
        if (!present || MathF.Abs(power_meter_current_dbm0(post) - target) <= 0.3f)
            return;

        Fail(
            $"Pre {power_meter_current_dbm0(pre)}dBm0, post {power_meter_current_dbm0(post)}dBm0, " +
            $"target {target}dBm0, current {agcf_current_power_dbm0(agc)}dBm0, gain {agcf_get_scaling(agc)}");
    }

    private static short FloatToInt16(float value) {
        if (value >= short.MaxValue)
            return short.MaxValue;
        if (value <= short.MinValue)
            return short.MinValue;
        return (short)value;
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new AgcFloatTestFailureException(message);
    }

    private sealed class AgcFloatTestFailureException : Exception {
        public AgcFloatTestFailureException(string message) : base(message) { }
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long samplesWritten;
        private bool disposed;

        public Pcm16WaveWriter(string path, short channels, int sampleRate) {
            this.channels = channels;
            this.sampleRate = sampleRate;
            stream = File.Create(path);
            writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> interleavedSamples) {
            foreach (short sample in interleavedSamples)
                writer.Write(sample);
            samplesWritten += interleavedSamples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;
            uint dataBytes = checked((uint)(samplesWritten * sizeof(short)));
            stream.Position = 0;
            WriteHeader(dataBytes);
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(uint dataBytes) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36u + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write((uint)(sampleRate * channels * sizeof(short)));
            writer.Write((ushort)(channels * sizeof(short)));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
        }
    }
}
