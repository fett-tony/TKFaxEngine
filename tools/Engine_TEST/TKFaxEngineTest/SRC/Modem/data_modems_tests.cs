/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * data_modems_tests.cs - Tests for managed data modems.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2011 Steve Underwood.
 *
 * Managed C# port preserving the finite bidirectional V.22bis data-path
 * test, callback flow, byte warm-up, checked-byte count and sample limit.
 */

#nullable enable
using AtModemControlOperation = TKFaxEngine.AtModemControlOperation;
using AsyncParity = TKFaxEngine.AsyncParity;

using System.Text;
using TKFaxEngine.Modem;
using static TKFaxEngine.Modem.data_modems;

namespace TKFaxEngineTest.Modem.Test;

public static class data_modems_tests {
    private const string OUTPUT_WAVE_FILE_NAME = "data_modems.wav";
    private const int SAMPLES_PER_CHUNK = 160;
    private const int DATA_MODEMS_TEST_WARMUP_BYTES = 128;
    private const int DATA_MODEMS_TEST_CHECK_BYTES = 4096;
    private const int DATA_MODEMS_TEST_MAX_SAMPLES = 120 * 8000;

    private static string? decode_test_file;
    private static bool done;
    private static bool sequence_terminated;

    private static readonly data_modems_state_t?[] data_modems_state =
        new data_modems_state_t?[2];

    private static readonly DataModemsTestEndpoint[] data_modems_endpoint =
    {
        new(),
        new()
    };

    public static int main(string[] testArguments) {
        try {
            return tkfx_data_modems_tests_body_main(testArguments);
        } catch (DataModemsTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_data_modems_tests_body_main(string[] testArguments) {
        bool logAudio = false;
        bool testSending = false;
        bool useGui = false;
        decode_test_file = null;

        for (int i = 0; i < testArguments.Length; i++) {
            string argument = testArguments[i];

            switch (argument) {
                case "-d":
                    if (++i >= testArguments.Length)
                        Fail("Missing filename for -d.");

                    decode_test_file = testArguments[i];
                    break;

                case "-g":
                    Console.Error.WriteLine(
                        "Graphical monitoring not available");
                    Fail(null);
                    break;

                case "-l":
                    logAudio = true;
                    break;

                case "-r":
                    testSending = false;
                    break;

                case "-s":
                    testSending = true;
                    break;

                default:
                    if (argument.StartsWith("-", StringComparison.Ordinal))
                        Fail($"Unknown option '{argument}'.");
                    break;
            }
        }

        _ = (Action<object?, BertReportEvent, BertResults>)reporter;

        _ = modem_tests(
            useGui,
            logAudio,
            testSending);

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static void reporter(
        object? userData,
        BertReportEvent reason,
        BertResults results) {
        int channel =
            userData is int value
                ? value
                : 0;

        switch (reason) {
            case BertReportEvent.Synced:
                Console.Error.WriteLine(
                    $"{channel}: BERT report synced");
                break;

            case BertReportEvent.Unsynced:
                Console.Error.WriteLine(
                    $"{channel}: BERT report unsync'ed");
                break;

            case BertReportEvent.Regular:
                Console.Error.WriteLine(
                    $"{channel}: BERT report regular - " +
                    $"{results.TotalBits} bits, " +
                    $"{results.BadBits} bad bits, " +
                    $"{results.Resyncs} resyncs");
                break;

            case BertReportEvent.ErrorRateGreaterThan1In10To2:
                Console.Error.WriteLine(
                    $"{channel}: BERT report > 1 in 10^2");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To2:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^2");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To3:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^3");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To4:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^4");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To5:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^5");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To6:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^6");
                break;

            case BertReportEvent.ErrorRateLessThan1In10To7:
                Console.Error.WriteLine(
                    $"{channel}: BERT report < 1 in 10^7");
                break;

            default:
                Console.Error.WriteLine(
                    $"{channel}: BERT report reason {(int)reason}");
                break;
        }
    }

    private static int data_modems_at_tx_handler(
        object? userData,
        ReadOnlySpan<byte> buffer) {
        _ = userData;
        _ = buffer;
        return 0;
    }

    private static int data_modems_control_handler(
        data_modems_state_t state,
        object? userData,
        int operation,
        string? number) {
        _ = state;
        _ = number;

        if (userData is DataModemsTestEndpoint endpoint &&
            ((AtModemControlOperation)operation ==
                AtModemControlOperation.Hangup ||
             (AtModemControlOperation)operation ==
                AtModemControlOperation.OnHook)) {
            endpoint.StatusEvents++;
        }

        return 0;
    }

    private static int get_msg(
        object? userData,
        byte[] message,
        int length) {
        if (userData is not DataModemsTestEndpoint endpoint ||
            length <= 0) {
            return 0;
        }

        int count = Math.Min(length, message.Length);
        message.AsSpan(0, count).Fill(endpoint.TxByte);
        return count;
    }

    private static int put_msg(
        object? userData,
        byte[]? message,
        int lengthOrStatus) {
        if (userData is not DataModemsTestEndpoint endpoint)
            return 0;

        if (lengthOrStatus < 0) {
            endpoint.StatusEvents++;
            return 0;
        }

        if (message is null)
            return 0;

        int length = Math.Min(lengthOrStatus, message.Length);

        for (int i = 0; i < length; i++) {
            endpoint.ReceivedBytes++;

            if (endpoint.ReceivedBytes <=
                DATA_MODEMS_TEST_WARMUP_BYTES) {
                continue;
            }

            if (endpoint.CheckedBytes >=
                DATA_MODEMS_TEST_CHECK_BYTES) {
                continue;
            }

            endpoint.CheckedBytes++;

            if (message[i] != endpoint.ExpectedRxByte) {
                endpoint.BadBytes++;

                if (endpoint.BadBytes <= 16) {
                    Console.Error.WriteLine(
                        $"ERROR data_modems channel={endpoint.Channel} " +
                        $"byte={endpoint.CheckedBytes} " +
                        $"expected=0x{endpoint.ExpectedRxByte:X2} " +
                        $"actual=0x{message[i]:X2}");
                }
            }
        }

        if (data_modems_endpoint[0].CheckedBytes >=
                DATA_MODEMS_TEST_CHECK_BYTES &&
            data_modems_endpoint[1].CheckedBytes >=
                DATA_MODEMS_TEST_CHECK_BYTES) {
            done = true;
            sequence_terminated = true;
        }

        return 0;
    }

    private static int modem_tests(
        bool useGui,
        bool logAudio,
        bool testSending) {
        _ = useGui;
        _ = testSending;

        var modemSamples =
            new short[SAMPLES_PER_CHUNK];

        var stereoSamples =
            new short[2 * SAMPLES_PER_CHUNK];

        Pcm16WaveWriter? waveWriter = null;
        FileStream? inputStream = null;

        try {
            if (logAudio) {
                waveWriter = new Pcm16WaveWriter(
                    OUTPUT_WAVE_FILE_NAME,
                    channels: 2,
                    sampleRate: 8000);
            }

            if (decode_test_file is not null) {
                try {
                    inputStream = File.Open(
                        decode_test_file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read);
                } catch (Exception ex)
                      when (ex is IOException ||
                            ex is UnauthorizedAccessException) {
                    Console.Error.WriteLine(
                        $"    Cannot open audio file '{decode_test_file}'");
                    return 2;
                }
            }

            ResetEndpoint(
                data_modems_endpoint[0],
                channel: 0,
                txByte: 0x5A,
                expectedRxByte: 0xA5);

            ResetEndpoint(
                data_modems_endpoint[1],
                channel: 1,
                txByte: 0xA5,
                expectedRxByte: 0x5A);

            done = false;
            sequence_terminated = false;

            bool callingParty = true;

            for (int i = 0; i < 2; i++) {
                DataModemsTestEndpoint endpoint =
                    data_modems_endpoint[i];

                data_modems_state_t? state =
                    data_modems_init(
                        null,
                        callingParty,
                        data_modems_at_tx_handler,
                        endpoint,
                        data_modems_control_handler,
                        endpoint,
                        put_msg,
                        get_msg,
                        endpoint);

                if (state is null) {
                    Console.Error.WriteLine(
                        $"    Cannot start data modem channel {i}");
                    return 2;
                }

                data_modems_state[i] = state;

                data_modems_set_async_mode(
                    state,
                    8,
                    (int)AsyncParity.None,
                    1);

                data_modems_set_modem_type(
                    state,
                    DATA_MODEM_V22BIS,
                    600,
                    2400);

                TKFaxEngine.SpanLogState logging =
                    data_modems_get_logging_state(state);

                TKFaxEngine.LoggingApi.span_log_set_protocol(
                    logging,
                    i == 0 ? "Modem-A" : "Modem-B");
                TKFaxEngine.LoggingApi.span_log_set_level(
                    logging,
                    TKFaxEngine.LoggingApi.SPAN_LOG_ERROR);
                TKFaxEngine.LoggingApi.span_log_set_message_handler(
                    logging,
                    static (_, _, message) => Console.Error.WriteLine(message),
                    null);

                callingParty = false;
            }

            Console.WriteLine(
                "Data modem V.22bis test: " +
                $"warmup={DATA_MODEMS_TEST_WARMUP_BYTES} bytes, " +
                $"checked={DATA_MODEMS_TEST_CHECK_BYTES} " +
                "bytes per direction");

            int elapsedSamples = 0;

            while (!done &&
                   elapsedSamples <
                       DATA_MODEMS_TEST_MAX_SAMPLES) {
                for (int i = 0; i < 2; i++) {
                    data_modems_state_t transmitter =
                        data_modems_state[i]
                        ?? throw new InvalidOperationException(
                            $"Data modem channel {i} " +
                            "is not initialized.");

                    int modemLength =
                        data_modems_tx(
                            transmitter,
                            modemSamples,
                            SAMPLES_PER_CHUNK);

                    if (modemLength < 0 ||
                        modemLength > SAMPLES_PER_CHUNK) {
                        Console.Error.WriteLine(
                            "Invalid transmitted sample count " +
                            $"{modemLength}.");
                        return 2;
                    }

                    if (modemLength <
                        SAMPLES_PER_CHUNK) {
                        modemSamples
                            .AsSpan(
                                modemLength,
                                SAMPLES_PER_CHUNK -
                                modemLength)
                            .Clear();

                        modemLength =
                            SAMPLES_PER_CHUNK;
                    }

                    if (logAudio) {
                        for (int sample = 0;
                             sample < modemLength;
                             sample++) {
                            stereoSamples[
                                2 * sample + i] =
                                modemSamples[sample];
                        }
                    }

                    data_modems_state_t receiver =
                        data_modems_state[i ^ 1]
                        ?? throw new InvalidOperationException(
                            $"Data modem channel {i ^ 1} " +
                            "is not initialized.");

                    data_modems_rx(
                        receiver,
                        modemSamples,
                        modemLength);
                }

                if (logAudio)
                    waveWriter!.WriteSamples(stereoSamples);

                elapsedSamples +=
                    SAMPLES_PER_CHUNK;
            }

            Console.WriteLine(
                "Data modem result: " +
                $"A rx={data_modems_endpoint[0].ReceivedBytes} " +
                $"checked={data_modems_endpoint[0].CheckedBytes} " +
                $"bad={data_modems_endpoint[0].BadBytes}, " +
                $"B rx={data_modems_endpoint[1].ReceivedBytes} " +
                $"checked={data_modems_endpoint[1].CheckedBytes} " +
                $"bad={data_modems_endpoint[1].BadBytes}, " +
                $"simulated_ms={elapsedSamples / 8}");

            if (!done ||
                !sequence_terminated ||
                data_modems_endpoint[0].BadBytes != 0 ||
                data_modems_endpoint[1].BadBytes != 0) {
                Console.Error.WriteLine(
                    "ERROR data_modems incomplete_or_bad " +
                    $"done={(done ? 1 : 0)} " +
                    "sequence_terminated=" +
                    $"{(sequence_terminated ? 1 : 0)} " +
                    "A_checked=" +
                    $"{data_modems_endpoint[0].CheckedBytes} " +
                    "A_bad=" +
                    $"{data_modems_endpoint[0].BadBytes} " +
                    "B_checked=" +
                    $"{data_modems_endpoint[1].CheckedBytes} " +
                    "B_bad=" +
                    $"{data_modems_endpoint[1].BadBytes}");

                Console.WriteLine("Tests failed");
                return 2;
            }

            return 0;
        } finally {
            inputStream?.Dispose();
            waveWriter?.Dispose();

            for (int i = 0;
                 i < data_modems_state.Length;
                 i++) {
                data_modems_free(
                    data_modems_state[i]);

                data_modems_state[i] = null;
            }
        }
    }

    private static void ResetEndpoint(
        DataModemsTestEndpoint endpoint,
        int channel,
        byte txByte,
        byte expectedRxByte) {
        endpoint.Channel = channel;
        endpoint.TxByte = txByte;
        endpoint.ExpectedRxByte = expectedRxByte;
        endpoint.ReceivedBytes = 0;
        endpoint.CheckedBytes = 0;
        endpoint.BadBytes = 0;
        endpoint.StatusEvents = 0;
    }

    private static void Fail(string? message) {
        if (!string.IsNullOrEmpty(message))
            Console.Error.WriteLine(message);

        throw new DataModemsTestsExitException(2);
    }

    public sealed class DataModemsTestsExitException :
        Exception {
        public DataModemsTestsExitException(
            int exitCode)
            : base(
                "data_modems_tests requested exit code " +
                $"{exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class DataModemsTestEndpoint {
        public int Channel { get; set; }
        public byte TxByte { get; set; }
        public byte ExpectedRxByte { get; set; }
        public int ReceivedBytes { get; set; }
        public int CheckedBytes { get; set; }
        public int BadBytes { get; set; }
        public int StatusEvents { get; set; }
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
