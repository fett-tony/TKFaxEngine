/*
 * TKFaxEngine - managed C# port
 *
 * fax_decode.cs - a simple fax audio decoder.
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2005 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 *
 * Fresh port from fax_decode.c. The V.21 control channel, V.17/V.29/V.27ter
 * image receivers, HDLC, ECM buffering and T.4 receive path remain streaming.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using System.Globalization;
using System.Text;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.FaxImage;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V17;
using TKFaxEngine.Modem.V27;
using TKFaxEngine.Modem.V29;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.BitOperationsApi;
using static TKFaxEngine.Daten.T30.T30Logging;
using static TKFaxEngine.Daten.T30.T35;
using static TKFaxEngine.FaxImage.t4_rx;
using static TKFaxEngine.Modem.FskApi;
using static TKFaxEngine.Modem.HdlcApi;
using static TKFaxEngine.Modem.V17.V17RxApi;
using static TKFaxEngine.Modem.V27.V27TerRxApi;
using ProtocolT30ModemType = TKFaxEngine.Daten.T30.T30ModemType;
using ProtocolT30SupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;

namespace TKFaxEngineTest.Fax.Test;

public static class fax_decode {
    private const int SamplesPerChunk = 160;
    private const byte DisBit1 = 0x01;
    private const byte DisBit2 = 0x02;
    private const byte DisBit3 = 0x04;
    private const byte DisBit4 = 0x08;
    private const byte DisBit5 = 0x10;
    private const byte DisBit6 = 0x20;
    private const byte DisBit7 = 0x40;
    private const byte DisBit8 = 0x80;

    private enum FastReceiver {
        None,
        V27Ter,
        V29,
        V17
    }

    private readonly record struct FallbackEntry(
        int BitRate,
        ProtocolT30ModemType ModemType,
        ProtocolT30SupportedModems Support,
        byte DcsCode);

    private static readonly FallbackEntry[] FallbackSequence =
    [
        new(14_400, ProtocolT30ModemType.V17, ProtocolT30SupportedModems.V17, DisBit6),
        new(12_000, ProtocolT30ModemType.V17, ProtocolT30SupportedModems.V17, DisBit6 | DisBit4),
        new(9_600, ProtocolT30ModemType.V17, ProtocolT30SupportedModems.V17, DisBit6 | DisBit3),
        new(9_600, ProtocolT30ModemType.V29, ProtocolT30SupportedModems.V29, DisBit3),
        new(7_200, ProtocolT30ModemType.V17, ProtocolT30SupportedModems.V17, DisBit6 | DisBit4 | DisBit3),
        new(7_200, ProtocolT30ModemType.V29, ProtocolT30SupportedModems.V29, DisBit4 | DisBit3),
        new(4_800, ProtocolT30ModemType.V27Ter, ProtocolT30SupportedModems.V27Ter, DisBit4),
        new(2_400, ProtocolT30ModemType.V27Ter, ProtocolT30SupportedModems.V27Ter, 0)
    ];

    private static readonly byte[][] EcmData = CreateEcmData();
    private static readonly short[] EcmLength = new short[256];

    private static readonly T30Log T30Log = new() {
        Sink = static (level, text) => Console.Error.WriteLine($"T.30 {level}: {text}")
    };

    private static t4_rx_state_t? t4RxState;
    private static bool t4Up;
    private static HdlcReceiver? hdlcRx;
    private static FastReceiver fastTrained;

    private static int lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T4_1D;
    private static int xResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
    private static int yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD;
    private static int imageWidth = 1728;
    private static int octetsPerEcmFrame = 256;
    private static bool errorCorrectingMode;
    private static int currentFallback;
    private static bool endOfPageDetected;

    private static FskRxState? fsk;
    private static V17RxState? v17_14400;
    private static V17RxState? v17_12000;
    private static V17RxState? v17_9600;
    private static V17RxState? v17_7200;
    private static V29Rx? v29_9600;
    private static V29Rx? v29_7200;
    private static V27TerRxState? v27ter_4800;
    private static V27TerRxState? v27ter_2400;

    private static int v17_14400_zeros;
    private static int v17_12000_zeros;
    private static int v17_9600_zeros;
    private static int v17_7200_zeros;
    private static int v29_9600_zeros;
    private static int v29_7200_zeros;
    private static int v27ter_4800_zeros;
    private static int v27ter_2400_zeros;

    private static int v17_14400_max_zeros;
    private static int v17_12000_max_zeros;
    private static int v17_9600_max_zeros;
    private static int v17_7200_max_zeros;
    private static int v29_9600_max_zeros;
    private static int v29_7200_max_zeros;
    private static int v27ter_4800_max_zeros;
    private static int v27ter_2400_max_zeros;

    public static int main(string[] testArguments) {
        try {
            return tkfx_fax_decode_body_main(testArguments);
        } catch (FaxDecodeExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_fax_decode_body_main(string[] testArguments) {
        ResetState();

        string fileName = "Test-Files/local/fax_samp.wav";
        bool decodeReversed = false;
        bool decodeT30 = false;

        int argumentCount = testArguments.Length;
        for (int index = 0; index < argumentCount; index++) {
            string argument = testArguments[index];
            if (!argument.StartsWith("-", StringComparison.Ordinal) || argument == "-") {
                fileName = argument;
                continue;
            }

            switch (argument) {
                case "-e":
                    errorCorrectingMode = true;
                    break;
                case "-r":
                    decodeReversed = true;
                    break;
                case "-t":
                    decodeT30 = true;
                    break;
                case "-w":
                    imageWidth = 1728;
                    break;
                case "-c":
                    SetCompression(RequireValue(testArguments, ref index, argumentCount, "-c"));
                    break;
                case "-x":
                    SetXResolution(RequireValue(testArguments, ref index, argumentCount, "-x"));
                    break;
                case "-y":
                    SetYResolution(RequireValue(testArguments, ref index, argumentCount, "-y"));
                    break;
                default:
                    if (argument.StartsWith("-c", StringComparison.Ordinal) && argument.Length > 2)
                        SetCompression(argument[2..]);
                    else if (argument.StartsWith("-x", StringComparison.Ordinal) && argument.Length > 2)
                        SetXResolution(argument[2..]);
                    else if (argument.StartsWith("-y", StringComparison.Ordinal) && argument.Length > 2)
                        SetYResolution(argument[2..]);
                    else
                        Fail($"Unknown option '{argument}'.");
                    break;
            }
        }

        if (decodeT30) {
            DecodeT30Messages(decodeReversed);
            return 0;
        }

        using var input = Pcm16WaveReader.Open(fileName);
        if (input.SampleRate != fax_utils.TelephonySampleRate)
            Fail($"    Unexpected sample rate in audio file '{fileName}'");
        if (input.Channels != 1)
            Fail($"    Unexpected number of channels in audio file '{fileName}'");

        InitializeReceivers();

        short[] samples = new short[SamplesPerChunk];
        while (input.ReadFrames(samples, SamplesPerChunk) == SamplesPerChunk) {
            ReadOnlySpan<short> block = samples;
            _ = fsk_rx(fsk!, block);
            _ = v17_rx(v17_14400!, block);
            _ = v17_rx(v17_12000!, block);
            _ = v17_rx(v17_9600!, block);
            _ = v17_rx(v17_7200!, block);
            _ = v29_9600!.Process(block);
            _ = v29_7200!.Process(block);
            _ = v27ter_rx(v27ter_4800!, block);
            _ = v27ter_rx(v27ter_2400!, block);
        }

        T4End();
        ReleaseReceivers();
        return 0;
    }

    private static void Decode20DigitMessage(ReadOnlySpan<byte> packet) {
        if (packet.Length > T30State.MaxIdentLength + 3) {
            Console.Error.WriteLine($"XXX {packet.Length} {T30State.MaxIdentLength + 1}");
            return;
        }

        ReadOnlySpan<byte> body = packet[2..];
        int end = body.Length;
        while (end > 1 && body[end - 1] == (byte)' ')
            end--;

        var text = new StringBuilder(Math.Max(0, end - 1));
        while (end > 1)
            text.Append((char)body[--end]);

        Console.Error.WriteLine($"{t30_frametype(body[0])} is: \"{text}\"");
    }

    private static void PrintFrame(string direction, ReadOnlySpan<byte> frame) {
        if (frame.IsEmpty)
            return;

        byte fcf = frame.Length > 2 ? frame[2] : (byte)0;
        Console.Error.Write($"{direction} {t30_frametype(fcf)}:");
        for (int i = Math.Min(2, frame.Length); i < frame.Length; i++)
            Console.Error.Write($" {frame[i]:x2}");
        Console.Error.WriteLine();

        byte type = (byte)(fcf & 0xFE);
        if (type is T30Frame.Dis or T30Frame.Dtc or T30Frame.Dcs)
            t30_log_dis_dtc_dcs(T30Log, frame);

        if (type is T30Frame.Csi or T30Frame.Tsi or T30Frame.Pwd or
            T30Frame.Sep or T30Frame.Sub or T30Frame.Sid) {
            Decode20DigitMessage(frame);
        }

        if (type is T30Frame.Nsf or T30Frame.Nss or T30Frame.Nsc) {
            if (frame.Length > 3 && t35_decode(frame[3..], out string? country, out string? vendor, out string? model)) {
                if (country is not null)
                    Console.Error.WriteLine($"The remote was made in '{country}'");
                if (vendor is not null)
                    Console.Error.WriteLine($"The remote was made by '{vendor}'");
                if (model is not null)
                    Console.Error.WriteLine($"The remote is a '{model}'");
            }

            if (type is T30Frame.Nss or T30Frame.Nsc) {
                Console.Error.WriteLine(
                    "WARNING: The FAX machines may be switching into a proprietary mode, " +
                    "which this software cannot decode");
            }
        }
    }

    private static int FindFallbackEntry(int dcsCode) {
        for (int i = 0; i < FallbackSequence.Length; i++) {
            if (FallbackSequence[i].DcsCode == dcsCode)
                return i;
        }
        return -1;
    }

    private static int CheckRxDcs(ReadOnlySpan<byte> message) {
        int[,] widths =
        {
            { 864, 1024, 1216, -1 },
            { 1728, 2048, 2432, -1 },
            { 3456, 4096, 4864, -1 }
        };

        if (message.Length < 6) {
            Console.WriteLine("Short DCS frame");
            return -1;
        }

        byte[] dcs = new byte[19];
        message[..Math.Min(message.Length, dcs.Length)].CopyTo(dcs);

        octetsPerEcmFrame = (dcs[6] & DisBit4) != 0 ? 256 : 64;

        if ((dcs[8] & DisBit1) != 0)
            yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE;
        else if ((dcs[4] & DisBit7) != 0)
            yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE;
        else
            yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD;

        if ((dcs[8] & DisBit3) != 0) {
            xResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R16;
            yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE;
        } else {
            xResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
        }

        int widthGroup = (dcs[8] & DisBit3) != 0 ? 2 : 1;
        int widthCode = dcs[5] & (DisBit2 | DisBit1);
        imageWidth = widths[widthGroup, widthCode];

        if ((dcs[12] & DisBit7) != 0)
            lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T85_L0;
        else if ((dcs[12] & DisBit6) != 0)
            lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T85;
        else if ((dcs[6] & DisBit7) != 0)
            lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T6;
        else if ((dcs[4] & DisBit8) != 0)
            lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T4_2D;
        else
            lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T4_1D;

        Console.Error.WriteLine($"Selected compression {lineEncoding}");

        currentFallback = FindFallbackEntry(dcs[4] & (DisBit6 | DisBit5 | DisBit4 | DisBit3));
        if (currentFallback < 0)
            Console.WriteLine("Remote asked for a modem standard we do not support");

        errorCorrectingMode = (dcs[6] & DisBit3) != 0;
        return 0;
    }

    private static void HdlcAccept(
        object? userData,
        ReadOnlyMemory<byte>? packet,
        int lengthOrStatus,
        bool ok) {
        _ = userData;
        if (lengthOrStatus < 0) {
            Console.Error.WriteLine($"HDLC status is {signal_status_to_str(lengthOrStatus)} ({lengthOrStatus})");
            return;
        }

        ReadOnlySpan<byte> message;
        if (packet.HasValue) {
            ReadOnlyMemory<byte> memory = packet.Value;
            message = memory.Span[..Math.Min(lengthOrStatus, memory.Length)];
        } else {
            message = ReadOnlySpan<byte>.Empty;
        }

        if (message.Length < 3)
            return;

        if (!ok) {
            Console.Error.Write("Bad HDLC frame");
            foreach (byte value in message)
                Console.Error.Write($" {value:x2}");
            Console.Error.WriteLine();
            return;
        }

        if (message[0] != 0xFF || (message[1] != 0x03 && message[1] != 0x13)) {
            Console.Error.WriteLine($"Bad frame header - {message[0]:x2} {message[1]:x2}");
            return;
        }

        PrintFrame("HDLC:", message);
        byte type = (byte)(message[2] & 0xFE);
        switch (type) {
            case T30Frame.Fcd:
                if (message.Length <= 4 + 256) {
                    int frameNumber = message[3];
                    int dataLength = message.Length - 4;
                    message[4..].CopyTo(EcmData[frameNumber]);
                    EcmLength[frameNumber] = checked((short)dataLength);
                }
                break;

            case T30Frame.Dcs:
                _ = CheckRxDcs(message);
                Console.Error.WriteLine("Return to long training");
                _ = v17_rx_restart(v17_14400!, 14_400, 0);
                _ = v17_rx_restart(v17_12000!, 12_000, 0);
                _ = v17_rx_restart(v17_9600!, 9_600, 0);
                _ = v17_rx_restart(v17_7200!, 7_200, 0);
                break;
        }
    }

    private static void T4Begin() {
        t4_rx_state_t state = t4RxState ?? throw new InvalidOperationException("T.4 receiver is not initialized.");
        _ = t4_rx_set_rx_encoding(state, lineEncoding);
        t4_rx_set_x_resolution(state, xResolution);
        t4_rx_set_y_resolution(state, yResolution);
        t4_rx_set_image_width(state, imageWidth);
        _ = t4_rx_start_page(state);
        t4Up = true;
        endOfPageDetected = false;
        Array.Fill(EcmLength, (short)-1);
    }

    private static void T4End() {
        if (!t4Up || t4RxState is null)
            return;

        if (errorCorrectingMode) {
            for (int i = 0; i < EcmLength.Length; i++) {
                if (EcmLength[i] > 0)
                    _ = t4_rx_put(t4RxState, EcmData[i], EcmLength[i]);
                Console.Error.Write(EcmLength[i] <= 0 ? '0' : '1');
            }
            Console.Error.WriteLine();
        }

        _ = t4_rx_end_page(t4RxState);
        var statistics = new t4_stats_t();
        t4_rx_get_transfer_statistics(t4RxState, statistics);
        Console.Error.WriteLine($"Pages = {statistics.pages_transferred}");
        Console.Error.WriteLine($"Image size = {statistics.width}x{statistics.length}");
        Console.Error.WriteLine($"Image resolution = {statistics.x_resolution}x{statistics.y_resolution}");
        Console.Error.WriteLine($"Bad rows = {statistics.bad_rows}");
        Console.Error.WriteLine($"Longest bad row run = {statistics.longest_bad_row_run}");
        t4Up = false;
    }

    private static void V21PutBit(object? userData, int bit) {
        _ = userData;
        if (bit < 0) {
            Console.Error.WriteLine($"V.21 rx status is {signal_status_to_str(bit)} ({bit})");
            return;
        }

        if (fastTrained == FastReceiver.None)
            hdlc_rx_put_bit(hdlcRx!, bit);
    }

    private static void V17_14400PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V17, "V.17", 14_400, bit, ref v17_14400_zeros, ref v17_14400_max_zeros);

    private static void V17_12000PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V17, "V.17", 12_000, bit, ref v17_12000_zeros, ref v17_12000_max_zeros);

    private static void V17_9600PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V17, "V.17", 9_600, bit, ref v17_9600_zeros, ref v17_9600_max_zeros);

    private static void V17_7200PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V17, "V.17", 7_200, bit, ref v17_7200_zeros, ref v17_7200_max_zeros);

    private static void V29_9600PutBit(int bit) =>
        HandleFastBit(FastReceiver.V29, "V.29", 9_600, bit, ref v29_9600_zeros, ref v29_9600_max_zeros);

    private static void V29_7200PutBit(int bit) =>
        HandleFastBit(FastReceiver.V29, "V.29", 7_200, bit, ref v29_7200_zeros, ref v29_7200_max_zeros);

    private static void V27Ter_4800PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V27Ter, "V.27ter", 4_800, bit, ref v27ter_4800_zeros, ref v27ter_4800_max_zeros);

    private static void V27Ter_2400PutBit(object? userData, int bit) =>
        HandleFastBit(FastReceiver.V27Ter, "V.27ter", 2_400, bit, ref v27ter_2400_zeros, ref v27ter_2400_max_zeros);

    private static void HandleFastBit(
        FastReceiver receiver,
        string modem,
        int bitRate,
        int bit,
        ref int zeros,
        ref int maximumZeros) {
        if (bit < 0) {
            Console.Error.WriteLine($"{modem} rx status is {signal_status_to_str(bit)} ({bit})");
            switch ((SignalStatus)bit) {
                case SignalStatus.TrainingSucceeded:
                    fastTrained = receiver;
                    maximumZeros = 0;
                    zeros = 0;
                    T4Begin();
                    break;

                case SignalStatus.CarrierDown:
                    T4End();
                    if (fastTrained == receiver)
                        fastTrained = FastReceiver.None;
                    Console.Error.WriteLine($"Max {modem} {bitRate} max zeros = {maximumZeros}");
                    maximumZeros = 0;
                    zeros = 0;
                    break;
            }
            return;
        }

        if (bit == 0) {
            zeros++;
        } else {
            if (zeros > maximumZeros)
                maximumZeros = zeros;
            zeros = 0;
        }

        if (errorCorrectingMode) {
            hdlc_rx_put_bit(hdlcRx!, bit);
        } else if (t4_rx_put_bit(t4RxState!, bit) != 0) {
            T4End();
            if (!endOfPageDetected)
                Console.Error.WriteLine("End of page detected");
            endOfPageDetected = true;
        }
    }

    private static void DecodeT30Messages(bool reversed) {
        string? line;
        while ((line = Console.ReadLine()) is not null) {
            string[] fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            byte[] message = new byte[Math.Min(fields.Length, 256)];
            int length = 0;

            foreach (string field in fields) {
                if (length >= message.Length || !byte.TryParse(field, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                    break;
                message[length++] = reversed ? bit_reverse8(value) : value;
            }

            PrintFrame(string.Empty, message.AsSpan(0, length));
        }
    }

    private static void InitializeReceivers() {
        hdlcRx = hdlc_rx_init(null, crc32: false, reportBadFrames: true, framingOkThreshold: 5, HdlcAccept, null);
        fsk = fsk_rx_init(null, preset_fsk_specs[FSK_V21CH2], FSK_FRAME_MODE_SYNC, V21PutBit, null);

        v17_14400 = v17_rx_init(null, 14_400, V17_14400PutBit, null) ?? throw new InvalidOperationException("V.17/14400 init failed.");
        v17_12000 = v17_rx_init(null, 12_000, V17_12000PutBit, null) ?? throw new InvalidOperationException("V.17/12000 init failed.");
        v17_9600 = v17_rx_init(null, 9_600, V17_9600PutBit, null) ?? throw new InvalidOperationException("V.17/9600 init failed.");
        v17_7200 = v17_rx_init(null, 7_200, V17_7200PutBit, null) ?? throw new InvalidOperationException("V.17/7200 init failed.");

        v29_9600 = new V29Rx(9_600, V29_9600PutBit);
        v29_7200 = new V29Rx(7_200, V29_7200PutBit);

        v27ter_4800 = v27ter_rx_init(null, 4_800, V27Ter_4800PutBit, null) ?? throw new InvalidOperationException("V.27ter/4800 init failed.");
        v27ter_2400 = v27ter_rx_init(null, 2_400, V27Ter_2400PutBit, null) ?? throw new InvalidOperationException("V.27ter/2400 init failed.");

        fsk_rx_set_signal_cutoff(fsk, -45.5f);
        v17_rx_set_signal_cutoff(v17_14400, -45.5f);
        v17_rx_set_signal_cutoff(v17_12000, -45.5f);
        v17_rx_set_signal_cutoff(v17_9600, -45.5f);
        v17_rx_set_signal_cutoff(v17_7200, -45.5f);
        v29_9600.SetSignalCutoff(-45.5f);
        v29_7200.SetSignalCutoff(-45.5f);
        v27ter_rx_set_signal_cutoff(v27ter_4800, -40.0f);
        v27ter_rx_set_signal_cutoff(v27ter_2400, -40.0f);

        ConfigureLog(v17_rx_get_logging_state(v17_14400), "V.17-14400");
        ConfigureLog(v17_rx_get_logging_state(v17_12000), "V.17-12000");
        ConfigureLog(v17_rx_get_logging_state(v17_9600), "V.17-9600");
        ConfigureLog(v17_rx_get_logging_state(v17_7200), "V.17-7200");
        v29_9600.Log = text => Console.Error.WriteLine($"V.29-9600: {text}");
        v29_7200.Log = text => Console.Error.WriteLine($"V.29-7200: {text}");
        ConfigureLog(v27ter_rx_get_logging_state(v27ter_4800), "V.27ter-4800");
        ConfigureLog(v27ter_rx_get_logging_state(v27ter_2400), "V.27ter-2400");

        t4RxState = t4_rx_init(null, "fax_decode.tif", (int)t4_image_compression_t.T4_COMPRESSION_T4_2D);
        if (t4RxState is null)
            Fail("Failed to init");
    }

    private static void ReleaseReceivers() {
        if (t4RxState is not null)
            _ = t4_rx_release(t4RxState);
        if (fsk is not null)
            _ = fsk_rx_release(fsk);
        if (hdlcRx is not null)
            _ = hdlc_rx_release(hdlcRx);
        if (v17_14400 is not null)
            _ = v17_rx_release(v17_14400);
        if (v17_12000 is not null)
            _ = v17_rx_release(v17_12000);
        if (v17_9600 is not null)
            _ = v17_rx_release(v17_9600);
        if (v17_7200 is not null)
            _ = v17_rx_release(v17_7200);
        if (v29_9600 is not null)
            _ = v29_9600.Release();
        if (v29_7200 is not null)
            _ = v29_7200.Release();
        if (v27ter_4800 is not null)
            _ = v27ter_rx_release(v27ter_4800);
        if (v27ter_2400 is not null)
            _ = v27ter_rx_release(v27ter_2400);
    }

    private static void ConfigureLog(V17RxLogger logger, string protocol) {
        logger.Protocol = protocol;
        logger.FlowSink = text => Console.Error.WriteLine($"{protocol}: {text}");
        logger.WarningSink = text => Console.Error.WriteLine($"{protocol} WARNING: {text}");
    }

    private static void ConfigureLog(V27TerRxLog logger, string protocol) {
        logger.Protocol = protocol;
        logger.FlowSink = text => Console.Error.WriteLine($"{protocol}: {text}");
        logger.DebugSink = text => Console.Error.WriteLine($"{protocol} DEBUG: {text}");
    }

    private static string RequireValue(string[] testArguments, ref int index, int argumentCount, string option) {
        if (++index >= argumentCount)
            Fail($"Missing value after {option}.");
        return testArguments[index];
    }

    private static void SetCompression(string value) {
        lineEncoding = value switch {
            "T41D" => (int)t4_image_compression_t.T4_COMPRESSION_T4_1D,
            "T42D" => (int)t4_image_compression_t.T4_COMPRESSION_T4_2D,
            "T6" => (int)t4_image_compression_t.T4_COMPRESSION_T6,
            "T85" => (int)t4_image_compression_t.T4_COMPRESSION_T85,
            "T88" => (int)t4_image_compression_t.T4_COMPRESSION_T88,
            "T81" => (int)t4_image_compression_t.T4_COMPRESSION_T42_T81,
            "T43" => (int)t4_image_compression_t.T4_COMPRESSION_T43,
            "T45" => (int)t4_image_compression_t.T4_COMPRESSION_T45,
            _ => throw new FaxDecodeExitException(2, "Unrecognised line compression.")
        };
    }

    private static void SetXResolution(string value) {
        xResolution = value switch {
            "R4" => (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R4,
            "R8" => (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8,
            "R16" => (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R16,
            _ => throw new FaxDecodeExitException(2, "Unrecognised X-resolution.")
        };
    }

    private static void SetYResolution(string value) {
        yResolution = value switch {
            "standard" => (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD,
            "fine" => (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_FINE,
            "superfine" => (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_SUPERFINE,
            _ => throw new FaxDecodeExitException(2, "Unrecognised Y-resolution.")
        };
    }

    private static void ResetState() {
        t4RxState = null;
        t4Up = false;
        hdlcRx = null;
        fastTrained = FastReceiver.None;
        lineEncoding = (int)t4_image_compression_t.T4_COMPRESSION_T4_1D;
        xResolution = (int)t4_image_x_resolution_t.T4_X_RESOLUTION_R8;
        yResolution = (int)t4_image_y_resolution_t.T4_Y_RESOLUTION_STANDARD;
        imageWidth = 1728;
        octetsPerEcmFrame = 256;
        errorCorrectingMode = false;
        currentFallback = 0;
        endOfPageDetected = false;
        Array.Fill(EcmLength, (short)-1);

        v17_14400_zeros = v17_12000_zeros = v17_9600_zeros = v17_7200_zeros = 0;
        v29_9600_zeros = v29_7200_zeros = 0;
        v27ter_4800_zeros = v27ter_2400_zeros = 0;
        v17_14400_max_zeros = v17_12000_max_zeros = v17_9600_max_zeros = v17_7200_max_zeros = 0;
        v29_9600_max_zeros = v29_7200_max_zeros = 0;
        v27ter_4800_max_zeros = v27ter_2400_max_zeros = 0;
    }

    private static byte[][] CreateEcmData() {
        var result = new byte[256][];
        for (int i = 0; i < result.Length; i++)
            result[i] = new byte[260];
        return result;
    }

    private static void Fail(string message) {
        Console.Error.WriteLine(message);
        throw new FaxDecodeExitException(2, message);
    }

    public sealed class FaxDecodeExitException : Exception {
        public FaxDecodeExitException(int exitCode, string? message = null)
            : base(message ?? $"fax_decode requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataEnd;

        private Pcm16WaveReader(
            FileStream stream,
            BinaryReader reader,
            long dataEnd,
            int sampleRate,
            int channels) {
            this.stream = stream;
            this.reader = reader;
            this.dataEnd = dataEnd;
            SampleRate = sampleRate;
            Channels = channels;
        }

        public int SampleRate { get; }
        public int Channels { get; }

        public static Pcm16WaveReader Open(string path) {
            FileStream stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            try {
                if (ReadFourCc(reader) != "RIFF")
                    throw new InvalidDataException($"'{path}' is not RIFF.");
                _ = reader.ReadUInt32();
                if (ReadFourCc(reader) != "WAVE")
                    throw new InvalidDataException($"'{path}' is not WAVE.");

                ushort format = 0;
                int channels = 0;
                int sampleRate = 0;
                int bits = 0;
                long dataStart = -1;
                uint dataLength = 0;

                while (stream.Position + 8 <= stream.Length) {
                    string chunk = ReadFourCc(reader);
                    uint size = reader.ReadUInt32();
                    long next = checked(stream.Position + size + (size & 1));
                    if (chunk == "fmt ") {
                        format = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = reader.ReadInt32();
                        _ = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        bits = reader.ReadUInt16();
                    } else if (chunk == "data") {
                        dataStart = stream.Position;
                        dataLength = size;
                        break;
                    }
                    stream.Position = next;
                }

                if (format != 1 || bits != 16 || channels < 1 || dataStart < 0)
                    throw new InvalidDataException($"'{path}' must be PCM16 WAV.");

                stream.Position = dataStart;
                return new Pcm16WaveReader(stream, reader, dataStart + dataLength, sampleRate, channels);
            } catch {
                reader.Dispose();
                stream.Dispose();
                throw;
            }
        }

        public int ReadFrames(short[] destination, int maximumFrames) {
            ArgumentNullException.ThrowIfNull(destination);
            if ((uint)maximumFrames > (uint)destination.Length)
                throw new ArgumentOutOfRangeException(nameof(maximumFrames));

            int frames = 0;
            while (frames < maximumFrames && stream.Position + Channels * 2L <= dataEnd) {
                short first = reader.ReadInt16();
                for (int channel = 1; channel < Channels; channel++)
                    _ = reader.ReadInt16();
                destination[frames++] = first;
            }
            return frames;
        }

        public void Dispose() {
            reader.Dispose();
            stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }
}
