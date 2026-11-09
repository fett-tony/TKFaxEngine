/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * v29_tests.cs - Managed C# port of v29_tests.c.
 * Original test written by Steve Underwood <steveu@coppice.org>.
 *
 * The complete active V.29 signal path, BERT sweep, line-model loop,
 * decode-file path, QAM diagnostics and pass/fail limits are retained.
 */

#nullable enable

using System.Globalization;
using System.Reflection;
using System.Text;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V29;
using TKFaxEngineTest.Network.Test;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.Modem.BertApi;
using static TKFaxEngine.Modem.V29.V29TxApi;
using static TKFaxEngineTest.Network.Test.line_model;

namespace TKFaxEngineTest.Modem.Test;

public static class v29_tests {
    private const int BLOCK_LEN = 160;
    private const string OUT_FILE_NAME = "v29.wav";
    private const int MUNGE_CODEC_NONE = 0;

    private static string? decode_test_file;
    private static int symbol_no;
    private static int rx_bits;
    private static BertState bert = null!;
    private static one_way_line_model_state_t? line_model;
    private static BertResults latest_results = new();
    private static float smooth_power;
    private static int update_interval = 100;
    private static readonly Random random = new(1);

    public static int main(string[] testArguments) {
        try { return tkfx_v29_tests_body_main(testArguments); } catch (V29TestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_v29_tests_body_main(string[] testArguments) {
        int channelCodec = MUNGE_CODEC_NONE;
        int rbsPattern = 0;
        int testBps = 9600;
        bool tep = false;
        int lineModelNo = 0;
        decode_test_file = null;
        int noiseLevel = -70;
        int signalLevel = -13;
        int bitsPerTest = 50_000;
        bool logAudio = false;

        ParseArguments(testArguments, ref channelCodec, ref rbsPattern, ref testBps,
            ref tep, ref lineModelNo, ref noiseLevel, ref signalLevel,
            ref bitsPerTest, ref logAudio);

        V29V34Pcm16WaveReader? input = null;
        V29V34Pcm16WaveWriter? output = null;
        V29TxState? tx = null;
        V29Rx? rx = null;

        try {
            if (logAudio)
                output = new V29V34Pcm16WaveWriter(OUT_FILE_NAME, 8000, 1);

            if (decode_test_file is not null) {
                input = new V29V34Pcm16WaveReader(decode_test_file, 8000, 1);
            } else {
                tx = v29_tx_init(null, testBps, tep, v29getbit, null)
                    ?? throw new InvalidOperationException("v29_tx_init failed");
                v29_tx_power(tx, signalLevel);
                v29_tx_set_modem_status_handler(tx, v29_tx_status, tx);
                SetPrivateField(tx, "_carrierPhaseRate", PhaseRate(1710.0f));
                SetPrivateField(tx, "_carrierPhase", 0u);

                bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
                bert_set_report(bert, 10_000, reporter, null);

                line_model = one_way_line_model_init(lineModelNo, noiseLevel, channelCodec, rbsPattern);
                if (line_model is null)
                    Fail("    Failed to create line model");
            }

            rx = new V29Rx(testBps);
            rx.SetSignalCutoff(-45.5f);
            rx.SetPutBitHandler(bit => v29putbit(rx, bit));
            rx.SetModemStatusHandler(status => v29_rx_status(rx, (int)status));
            rx.SetQamReportHandler((received, target, state) => qam_report(rx, received, target, state));
            SetPrivateField(rx, "_carrierPhase", 0x80000000u);

            latest_results = new BertResults();
            var generated = new short[BLOCK_LEN];
            var channel = new short[BLOCK_LEN];

            for (int blockNo = 0; ; blockNo++) {
                int samples;
                if (decode_test_file is not null) {
                    samples = input!.ReadFrames(channel);
                    if (samples == 0)
                        break;
                } else {
                    samples = v29_tx(tx!, generated, BLOCK_LEN);
                    if (samples == 0) {
                        Array.Clear(channel, 0, channel.Length);
                        rx.Process(channel, 0, BLOCK_LEN);

                        var result = new BertResults();
                        bert_result(bert, result);
                        PrintResults("Final result", signalLevel, noiseLevel, result);
                        PrintResults("Last report ", signalLevel, noiseLevel, latest_results);

                        if (result.TotalBits < bitsPerTest
                            || latest_results.TotalBits < bitsPerTest - 100
                            || latest_results.BadBits != 0) {
                            break;
                        }

                        latest_results = new BertResults();
                        signalLevel--;
                        v29_tx_restart(tx!, testBps, tep);
                        v29_tx_power(tx!, signalLevel);
                        rx.Restart(testBps, false);
                        SetPrivateField(rx, "_equalizerPutStep", random.Next(48 * 10 / 3));

                        bert_release(bert);
                        bert = bert_init(bert, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
                        bert_set_report(bert, 10_000, reporter, null);

                        one_way_line_model_free(line_model!);
                        line_model = one_way_line_model_init(lineModelNo, noiseLevel, channelCodec, 0);
                        if (line_model is null)
                            Fail("    Failed to create line model");
                        continue;
                    }

                    if (logAudio)
                        output!.WriteFrames(generated.AsSpan(0, samples));

                    one_way_line_model(line_model!, channel, generated, samples);
                }

                rx.Process(channel, 0, samples);
            }

            if (decode_test_file is null) {
                var result = new BertResults();
                bert_result(bert, result);
                Console.Error.WriteLine("At completion:");
                PrintResults("Final result", signalLevel, noiseLevel, result);
                PrintResults("Last report ", signalLevel, noiseLevel, latest_results);

                one_way_line_model_free(line_model!);
                line_model = null;

                if (signalLevel > -43)
                    Fail("Tests failed.");

                Console.WriteLine("Tests passed.");
            }

            return 0;
        } finally {
            rx?.Release();
            if (tx is not null)
                v29_tx_free(tx);
            if (bert is not null)
                bert_release(bert);
            if (line_model is not null) {
                one_way_line_model_free(line_model);
                line_model = null;
            }
            input?.Dispose();
            output?.Dispose();
        }
    }

    private static void ParseArguments(string[] testArguments,
        ref int codec, ref int rbs, ref int bps, ref bool tep,
        ref int model, ref int noise, ref int signal, ref int bits,
        ref bool logAudio) {
        for (int i = 0; i < testArguments.Length; i++) {
            string arg = testArguments[i];
            switch (arg) {
                case "-b": bps = ParseInt(Next(testArguments, ref i)); break;
                case "-B": bits = ParseInt(Next(testArguments, ref i)); break;
                case "-c": codec = ParseInt(Next(testArguments, ref i)); break;
                case "-d": decode_test_file = Next(testArguments, ref i); break;
                case "-g": Console.Error.WriteLine("Graphical monitoring not available"); Exit(2); break;
                case "-l": logAudio = true; break;
                case "-m": model = ParseInt(Next(testArguments, ref i)); break;
                case "-n": noise = ParseInt(Next(testArguments, ref i)); break;
                case "-r": rbs = ParseInt(Next(testArguments, ref i)); break;
                case "-s": signal = ParseInt(Next(testArguments, ref i)); break;
                case "-t": tep = true; break;
                default: Exit(2); break;
            }
        }

        if (bps is not (9600 or 7200 or 4800)) {
            Console.Error.WriteLine("Invalid bit rate specified");
            Exit(2);
        }
    }

    private static void reporter(object? userData, int reason, BertResults results) {
        _ = userData;
        if (reason == BERT_REPORT_REGULAR) {
            Console.Error.WriteLine($"BERT report regular - {results.TotalBits} bits, {results.BadBits} bad bits, {results.Resyncs} resyncs");
            latest_results = results.Clone();
        } else {
            Console.Error.WriteLine($"BERT report {bert_event_to_str(reason)}");
        }
    }

    private static void v29_rx_status(V29Rx rx, int status) {
        Console.WriteLine($"V.29 rx status is {signal_status_to_str(status)} ({status})");
        if (status == (int)V29Rx.V29RxStatus.TrainingSucceeded) {
            Console.WriteLine("Training succeeded");
            DumpEqualizer(rx, "Equalizer:");
        }
    }

    private static void v29putbit(V29Rx rx, int bit) {
        if (bit < 0) {
            v29_rx_status(rx, bit);
            return;
        }

        if (decode_test_file is not null)
            Console.WriteLine($"Rx bit {rx_bits++} - {bit}");
        else
            bert_put_bit(bert, bit);
    }

    private static void v29_tx_status(object? userData, int status) {
        _ = userData;
        Console.WriteLine($"V.29 tx status is {signal_status_to_str(status)} ({status})");
    }

    private static int v29getbit(object? userData) {
        _ = userData;
        return bert_get_bit(bert);
    }

    private static void qam_report(V29Rx rx, V29Rx.ComplexSample constellation, V29Rx.ComplexSample target, int symbol) {
        float errorReal = constellation.Real - target.Real;
        float errorImag = constellation.Imaginary - target.Imaginary;
        float power = errorReal * errorReal + errorImag * errorImag;
        smooth_power = 0.95f * smooth_power + 0.05f * power;

        Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0,8} [{1,8:F4}, {2,8:F4}] [{3,8:F4}, {4,8:F4}] {5,2:X} {6,8:F4} {7,8:F4} {8,9:F4} {9,7:F3} {10,7:F4}",
            symbol_no++, constellation.Real, constellation.Imaginary,
            target.Real, target.Imaginary, symbol, power, smooth_power,
            rx.CarrierFrequency, rx.SignalPower, rx.SymbolTimingCorrection));

        if (--update_interval <= 0) {
            DumpEqualizer(rx, "Equalizer A:");
            update_interval = 100;
        }
    }

    private static void DumpEqualizer(V29Rx rx, string title) {
        V29Rx.ComplexSample[] coefficients = rx.GetEqualizerState();
        if (coefficients.Length == 0)
            return;
        Console.WriteLine(title);
        for (int i = 0; i < coefficients.Length; i++) {
            float power = coefficients[i].Real * coefficients[i].Real
                        + coefficients[i].Imaginary * coefficients[i].Imaginary;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0,3} ({1,15:F5}, {2,15:F5}) -> {3,15:F5}",
                i, coefficients[i].Real, coefficients[i].Imaginary, power));
        }
    }

    private static void PrintResults(string caption, int signal, int noise, BertResults result) =>
        Console.Error.WriteLine($"{caption} {signal}dBm0/{noise}dBm0, {result.TotalBits} bits, {result.BadBits} bad bits, {result.Resyncs} resyncs");

    private static int PhaseRate(float frequency) =>
        unchecked((int)Math.Round(frequency * 4294967296.0 / 8000.0, MidpointRounding.AwayFromZero));

    private static void SetPrivateField(object target, string name, object value) {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(target.GetType().FullName, name);
        field.SetValue(target, value);
    }

    private static string Next(string[] testArguments, ref int i) {
        if (++i >= testArguments.Length) Exit(2);
        return testArguments[i];
    }

    private static int ParseInt(string value) {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) Exit(2);
        return result;
    }

    internal static void Exit(int code) => throw new V29TestsExitException(code);
    private static void Fail(string message) { Console.Error.WriteLine(message); Exit(2); }

    public sealed class V29TestsExitException : Exception {
        public V29TestsExitException(int exitCode) : base($"v29_tests exited with code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }
}

internal sealed class V29V34Pcm16WaveReader : IDisposable {
    private readonly BinaryReader reader;
    private readonly int channels;
    private long remaining;

    public V29V34Pcm16WaveReader(string path, int expectedRate, int expectedChannels) {
        reader = new BinaryReader(File.OpenRead(path), Encoding.ASCII, leaveOpen: false);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("Not RIFF");
        _ = reader.ReadUInt32();
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("Not WAVE");
        int format = 0, rate = 0, bits = 0, foundChannels = 0;
        while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length) {
            string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int size = reader.ReadInt32();
            long next = reader.BaseStream.Position + size + (size & 1);
            if (id == "fmt ") {
                format = reader.ReadUInt16(); foundChannels = reader.ReadUInt16(); rate = reader.ReadInt32();
                _ = reader.ReadInt32(); _ = reader.ReadUInt16(); bits = reader.ReadUInt16();
            } else if (id == "data") { remaining = size; break; }
            reader.BaseStream.Position = next;
        }
        if (format != 1 || bits != 16 || rate != expectedRate || foundChannels != expectedChannels)
            throw new InvalidDataException($"Expected PCM16 {expectedRate}Hz/{expectedChannels}ch");
        channels = foundChannels;
    }

    public int ReadFrames(Span<short> destination) {
        int frames = 0;
        while (frames < destination.Length && remaining >= channels * 2L) {
            destination[frames++] = reader.ReadInt16();
            for (int c = 1; c < channels; c++) _ = reader.ReadInt16();
            remaining -= channels * 2L;
        }
        return frames;
    }

    public void Dispose() => reader.Dispose();
}

internal sealed class V29V34Pcm16WaveWriter : IDisposable {
    private readonly BinaryWriter writer;
    private readonly int sampleRate;
    private readonly short channels;
    private long samples;
    private bool disposed;

    public V29V34Pcm16WaveWriter(string path, int sampleRate, int channels) {
        this.sampleRate = sampleRate;
        this.channels = checked((short)channels);
        writer = new BinaryWriter(File.Create(path), Encoding.ASCII, leaveOpen: false);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write(this.channels); writer.Write(sampleRate);
        writer.Write(sampleRate * this.channels * 2); writer.Write((short)(this.channels * 2)); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(0);
    }

    public void WriteFrames(ReadOnlySpan<short> interleaved) {
        foreach (short sample in interleaved) writer.Write(sample);
        samples += interleaved.Length;
    }

    public void Dispose() {
        if (disposed) return;
        disposed = true;
        long end = writer.BaseStream.Position;
        writer.BaseStream.Position = 4; writer.Write(checked((int)(end - 8)));
        writer.BaseStream.Position = 40; writer.Write(checked((int)(samples * 2)));
        writer.BaseStream.Position = end;
        writer.Dispose();
    }
}
