/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * dds_tests.cs - Direct digital synthesis tests.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * Managed C# port preserving all real and complex DDS frequencies,
 * levels, A-law round trips, meter damping values, tolerances and WAV output.
 */

#nullable enable

using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.PowerMeterApi;
using static TKFaxEngine.Audio.Dds;

namespace TKFaxEngineTest.Audio.Test;

public static class dds_tests {
    private const string OUTPUT_FILE_NAME =
        "dds.wav";

    private const string OUTPUT_FILE_NAME_COMPLEX =
        "complex_dds.wav";

    private const int SAMPLES_PER_CHUNK =
        8000;

    public static int main(string[] testArguments) {
        try {
            return tkfx_dds_tests_body_main(
                testArguments);
        } catch (DdsTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_dds_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        var buffer =
            new short[2 * SAMPLES_PER_CHUNK];

        PowerMeterState meter =
            power_meter_init(null, 10)
            ?? throw new InvalidOperationException(
                "Cannot initialize the power meter.");

        Console.WriteLine(
            "Non-complex DDS tests.");

        using (var output =
               new Pcm16WaveWriter(
                   OUTPUT_FILE_NAME,
                   channels: 1,
                   sampleRate: Dds.SampleRate)) {
            uint phase = 0;

            Console.WriteLine(
                "Test with 123.456789Hz at -10dBm0.");

            int phaseIncrement =
                dds_phase_rate(
                    123.456789f);

            short scale =
                dds_scaling_dbm0(
                    -10.0f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                int scaled =
                    (dds(
                        ref phase,
                        phaseIncrement) *
                     scale) >> 15;

                buffer[i] =
                    alaw_to_linear(
                        linear_to_alaw(
                            scaled));

                power_meter_update(
                    meter,
                    buffer[i]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    SAMPLES_PER_CHUNK));

            PrintLevel(meter);

            if (MathF.Abs(
                    power_meter_current_dbm0(
                        meter) +
                    10.0f) > 0.1f) {
                Fail();
            }

            Console.WriteLine(
                "Test with 12.3456789Hz at full scale.");

            phaseIncrement =
                dds_phase_rate(
                    12.3456789f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                buffer[i] =
                    alaw_to_linear(
                        linear_to_alaw(
                            dds(
                                ref phase,
                                phaseIncrement)));

                power_meter_update(
                    meter,
                    buffer[i]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    SAMPLES_PER_CHUNK));

            PrintLevel(meter);

            if (MathF.Abs(
                    power_meter_current_dbov(
                        meter) +
                    3.02f) > 0.2f) {
                Fail();
            }

            Console.WriteLine(
                "Test with 2345.6789Hz at -10dBov.");

            phaseIncrement =
                dds_phase_rate(
                    2345.6789f);

            scale =
                dds_scaling_dbov(
                    -10.0f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                int scaled =
                    (dds(
                        ref phase,
                        phaseIncrement) *
                     scale) >> 15;

                buffer[i] =
                    alaw_to_linear(
                        linear_to_alaw(
                            scaled));

                power_meter_update(
                    meter,
                    buffer[i]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    SAMPLES_PER_CHUNK));

            PrintLevel(meter);

            if (MathF.Abs(
                    power_meter_current_dbov(
                        meter) +
                    10.0f) > 0.1f) {
                Fail();
            }

            Console.WriteLine(
                "Test with 3456.789Hz at full scale.");

            phaseIncrement =
                dds_phase_rate(
                    3456.789f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                buffer[i] =
                    alaw_to_linear(
                        linear_to_alaw(
                            dds(
                                ref phase,
                                phaseIncrement)));

                power_meter_update(
                    meter,
                    buffer[i]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    SAMPLES_PER_CHUNK));

            PrintLevel(meter);

            if (MathF.Abs(
                    power_meter_current_dbov(
                        meter) +
                    3.02f) > 0.05f) {
                Fail();
            }
        }

        Console.WriteLine(
            "Complex DDS tests,");

        using (var output =
               new Pcm16WaveWriter(
                   OUTPUT_FILE_NAME_COMPLEX,
                   channels: 2,
                   sampleRate: Dds.SampleRate)) {
            Console.WriteLine(
                "Test with 123.456789Hz at -10dBm0.");

            PowerMeterState meterI =
                power_meter_init(null, 7)
                ?? throw new InvalidOperationException(
                    "Cannot initialize the " +
                    "I-channel power meter.");

            PowerMeterState meterQ =
                power_meter_init(null, 7)
                ?? throw new InvalidOperationException(
                    "Cannot initialize the " +
                    "Q-channel power meter.");

            short scale =
                dds_scaling_dbm0(
                    -10.0f);

            uint phase = 0;

            int phaseIncrement =
                dds_phase_ratef(
                    123.456789f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                DdsComplexFloat complex =
                    dds_complexf(
                        ref phase,
                        phaseIncrement);

                buffer[2 * i] =
                    ToInt16(
                        complex.Real *
                        scale);

                buffer[2 * i + 1] =
                    ToInt16(
                        complex.Imaginary *
                        scale);

                power_meter_update(
                    meterI,
                    buffer[2 * i]);

                power_meter_update(
                    meterQ,
                    buffer[2 * i + 1]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    2 * SAMPLES_PER_CHUNK));

            PrintComplexLevels(
                meterI,
                meterQ);

            if (MathF.Abs(
                    power_meter_current_dbm0(
                        meterI) +
                    10.0f) > 0.2f ||
                MathF.Abs(
                    power_meter_current_dbm0(
                        meterQ) +
                    10.0f) > 0.2f) {
                Fail();
            }

            Console.WriteLine(
                "Test with 123.456789Hz at full scale.");

            meterI =
                power_meter_init(
                    meterI,
                    7)
                ?? throw new InvalidOperationException(
                    "Cannot reset the " +
                    "I-channel power meter.");

            meterQ =
                power_meter_init(
                    meterQ,
                    7)
                ?? throw new InvalidOperationException(
                    "Cannot reset the " +
                    "Q-channel power meter.");

            phase = 0;

            phaseIncrement =
                dds_phase_ratef(
                    123.456789f);

            for (int i = 0;
                 i < SAMPLES_PER_CHUNK;
                 i++) {
                DdsComplexFloat complex =
                    dds_complexf(
                        ref phase,
                        phaseIncrement);

                buffer[2 * i] =
                    ToInt16(
                        complex.Real *
                        32768.0f);

                buffer[2 * i + 1] =
                    ToInt16(
                        complex.Imaginary *
                        32768.0f);

                power_meter_update(
                    meterI,
                    buffer[2 * i]);

                power_meter_update(
                    meterQ,
                    buffer[2 * i + 1]);
            }

            output.WriteSamples(
                buffer.AsSpan(
                    0,
                    2 * SAMPLES_PER_CHUNK));

            PrintComplexLevels(
                meterI,
                meterQ);

            if (MathF.Abs(
                    power_meter_current_dbov(
                        meterI) -
                    Dds.DbovMaximumSinePower) >
                0.2f ||
                MathF.Abs(
                    power_meter_current_dbov(
                        meterQ) -
                    Dds.DbovMaximumSinePower) >
                0.2f) {
                Fail();
            }
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static short ToInt16(float value) {
        return unchecked(
            (short)(int)value);
    }

    private static void PrintLevel(
        PowerMeterState meter) {
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "Level is {0:F6}dBOv/{1:F6}dBm0",
                power_meter_current_dbov(
                    meter),
                power_meter_current_dbm0(
                    meter)));
    }

    private static void PrintComplexLevels(
        PowerMeterState meterI,
        PowerMeterState meterQ) {
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "Level is {0:F6}dBOv/{1:F6}dBm0, " +
                "{2:F6}dBOv/{3:F6}dBm0",
                power_meter_current_dbov(
                    meterI),
                power_meter_current_dbm0(
                    meterI),
                power_meter_current_dbov(
                    meterQ),
                power_meter_current_dbm0(
                    meterQ)));
    }

    private static void Fail() {
        Console.WriteLine("Test failed.");

        throw new DdsTestsExitException(
            2);
    }

    public sealed class DdsTestsExitException :
        Exception {
        public DdsTestsExitException(
            int exitCode)
            : base(
                "dds_tests requested exit code " +
                $"{exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveWriter :
        IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long sampleCount;
        private bool disposed;

        public Pcm16WaveWriter(
            string path,
            short channels,
            int sampleRate) {
            if (channels <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(channels));
            }

            if (sampleRate <= 0) {
                throw new ArgumentOutOfRangeException(
                    nameof(sampleRate));
            }

            this.channels = channels;
            this.sampleRate = sampleRate;

            stream = File.Create(path);

            writer = new BinaryWriter(
                stream,
                Encoding.ASCII,
                leaveOpen: true);

            WriteHeader(0);
        }

        public void WriteSamples(
            ReadOnlySpan<short> samples) {
            ObjectDisposedException.ThrowIf(
                disposed,
                this);

            foreach (short sample in samples)
                writer.Write(sample);

            sampleCount += samples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;

            disposed = true;

            long dataLength =
                checked(
                    sampleCount *
                    sizeof(short));

            writer.Flush();
            stream.Position = 0;
            WriteHeader(dataLength);

            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(
            long dataLength) {
            if (dataLength > uint.MaxValue) {
                throw new InvalidOperationException(
                    "The WAV data exceeds the " +
                    "RIFF size limit.");
            }

            ushort blockAlign =
                checked(
                    (ushort)(
                        channels *
                        sizeof(short)));

            uint byteRate =
                checked(
                    (uint)(
                        sampleRate *
                        blockAlign));

            uint riffLength =
                checked(
                    (uint)(
                        36 +
                        dataLength));

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "RIFF"));

            writer.Write(riffLength);

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "WAVEfmt "));

            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((ushort)16);

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "data"));

            writer.Write((uint)dataLength);
        }
    }
}
