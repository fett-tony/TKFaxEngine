/*
 * TKFaxEngine - managed C# port of v42_tests.c.
 *
 * The spanDSP V.42 LAP-M back-to-back test is retained, including support
 * negotiation, information frame sequencing, local/far busy signalling,
 * optional variable frame lengths, optional injected bit errors and the full
 * one-million-bit test load.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;
using TKFaxEngine;

using System;
using System.Runtime.CompilerServices;
using TKFaxEngine.Modem.V42;
using static TKFaxEngine.LoggingApi;
using static TKFaxEngine.Modem.V42.v42;

namespace TKFaxEngineTest.Modem.Test;

public static class v42_tests
{
    private static readonly v42_state_t CallerStorage = new();
    private static readonly v42_state_t AnswererStorage = new();
    private static readonly int[] RxNext = new int[3];
    private static readonly int[] TxNext = new int[3];
    private static readonly CCompatibleRandom Random = new(1u);

    private static v42_state_t? _caller;
    private static v42_state_t? _answerer;
    private static bool _variableLength;
    private static int _receivedFrameCount;
    private static int _callerBusyWasSet;

    public sealed class V42TestsExitException : Exception
    {
        public V42TestsExitException(int exitCode)
            : base($"v42_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int Main(string[] args)
    {
        try
        {
            return tkfx_v42_tests_body_main(args);
        }
        catch (V42TestsExitException ex)
        {
            return ex.ExitCode;
        }
    }

    public static int tkfx_v42_tests_body_main(string[] testArguments)
    {
        ArgumentNullException.ThrowIfNull(testArguments);

        int insertCallerBitErrors = 0;
        int insertAnswererBitErrors = 0;
        _variableLength = false;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++)
        {
            string argument = testArguments[argumentIndex];
            if (argument.Length < 2 || argument[0] != '-')
                Exit(2);

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++)
            {
                switch (argument[optionIndex])
                {
                    case 'b':
                        insertCallerBitErrors = 11000;
                        insertAnswererBitErrors = 10000;
                        break;
                    case 'v':
                        _variableLength = true;
                        break;
                    default:
                        Exit(2);
                        break;
                }
            }
        }

        _caller = v42_init(
            CallerStorage,
            true,
            true,
            V42GetFrames,
            V42PutFrames,
            CallerStorage)!;

        _answerer = v42_init(
            AnswererStorage,
            false,
            true,
            V42GetFrames,
            V42PutFrames,
            AnswererStorage)!;

        v42_set_status_callback(_caller, V42Status, _caller);
        v42_set_status_callback(_answerer, V42Status, _answerer);
        v42_restart(_caller);
        v42_restart(_answerer);

        int logLevel =
            SPAN_LOG_SHOW_SEVERITY |
            SPAN_LOG_SHOW_PROTOCOL |
            SPAN_LOG_SHOW_TAG |
            SPAN_LOG_DEBUG;

        span_log_set_level(v42_get_logging_state(_caller), logLevel);
        span_log_set_tag(v42_get_logging_state(_caller), "caller");
        span_log_set_level(v42_get_logging_state(_answerer), logLevel);
        span_log_set_tag(v42_get_logging_state(_answerer), "answerer");

        for (int i = 0; i < 1_000_000; i++)
        {
            int bit = v42_tx_bit(_caller);
            if (insertCallerBitErrors != 0 && i % insertCallerBitErrors == 0)
                bit ^= 1;

            v42_rx_bit(_answerer, bit);

            bit = v42_tx_bit(_answerer);
            if (insertAnswererBitErrors != 0 && i % insertAnswererBitErrors == 0)
                bit ^= 1;

            v42_rx_bit(_caller, bit);
        }

        return 0;
    }

    private static void V42Status(object? userData, int status)
    {
        if (userData is not v42_state_t state)
            throw new ArgumentException("V.42 status user data must be a v42_state_t.", nameof(userData));

        string statusText = status < 0
            ? SignalStatusToString(status)
            : lapm_status_to_str(status);

        Console.WriteLine(
            "0x{0:X8}: Status is '{1}' ({2})",
            RuntimeHelpers.GetHashCode(state),
            statusText,
            status);
    }

    private static int V42GetFrames(object? userData, byte[] message, int len)
    {
        if (userData is not v42_state_t state)
            throw new ArgumentException("V.42 frame source user data must be a v42_state_t.", nameof(userData));

        if (len < 0)
        {
            V42Status(userData, len);
            return 0;
        }

        int x = ReferenceEquals(state, _caller) ? 1 : 2;
        int length;

        if (_variableLength)
        {
            int mask = MakeMask32(len);
            do
            {
                length = mask & Random.Next();
            }
            while (length > len);
        }
        else
        {
            length = len;
        }

        for (int i = 0; i < length; i++)
            message[i] = unchecked((byte)TxNext[x]++);

        return length;
    }

    private static int V42PutFrames(
        object? userData,
        byte[]? message,
        int lengthOrStatus)
    {
        if (userData is not v42_state_t state)
            throw new ArgumentException("V.42 frame sink user data must be a v42_state_t.", nameof(userData));

        if (lengthOrStatus < 0)
        {
            V42Status(userData, lengthOrStatus);
            return 0;
        }

        byte[] frame = message!;
        int x = ReferenceEquals(state, _caller) ? 1 : 2;
        int length = lengthOrStatus;

        for (int i = 0; i < length; i++)
        {
            int expected = RxNext[x] & 0xFF;
            if (frame[i] != expected)
            {
                Console.WriteLine(
                    "0x{0:X8}: Mismatch 0x{1:X2} 0x{2:X2}",
                    RuntimeHelpers.GetHashCode(state),
                    frame[i],
                    expected);
                Exit(2);
            }

            RxNext[x]++;
        }

        Console.WriteLine(
            "0x{0:X8}: Got frame len {1}",
            RuntimeHelpers.GetHashCode(state),
            length);

        Console.WriteLine(
            "0x{0:X8}: {1} Far end busy status {2}",
            RuntimeHelpers.GetHashCode(state),
            _receivedFrameCount,
            v42_get_far_busy_status(state) ? 1 : 0);

        if (ReferenceEquals(state, _caller))
        {
            if (++_receivedFrameCount == 5)
            {
                v42_set_local_busy_status(state, true);
                _callerBusyWasSet = 1;
            }
        }
        else if (_callerBusyWasSet != 0 && ++_receivedFrameCount == 45)
        {
            v42_set_local_busy_status(_caller!, false);
        }
        return 0;
    }

    private static int MakeMask32(int value)
    {
        if (value <= 0)
            return 0;

        uint mask = unchecked((uint)value);
        mask |= mask >> 1;
        mask |= mask >> 2;
        mask |= mask >> 4;
        mask |= mask >> 8;
        mask |= mask >> 16;
        return unchecked((int)mask);
    }

    private static string SignalStatusToString(int status) => status switch
    {
        (int)SignalStatus.CarrierDown => "Carrier down",
        (int)SignalStatus.CarrierUp => "Carrier up",
        (int)SignalStatus.TrainingInProgress => "Training in progress",
        (int)SignalStatus.TrainingSucceeded => "Training succeeded",
        (int)SignalStatus.TrainingFailed => "Training failed",
        (int)SignalStatus.FramingOk => "Framing OK",
        (int)SignalStatus.EndOfData => "End of data",
        (int)SignalStatus.Abort => "Abort",
        (int)SignalStatus.Break => "Break",
        (int)SignalStatus.ShutdownComplete => "Shutdown complete",
        (int)SignalStatus.OctetReport => "Octet report",
        (int)SignalStatus.PoorSignalQuality => "Poor signal quality",
        (int)SignalStatus.ModemRetrainOccurred => "Modem retrain occurred",
        (int)SignalStatus.LinkConnected => "Link connected",
        (int)SignalStatus.LinkDisconnected => "Link disconnected",
        (int)SignalStatus.LinkError => "Link error",
        (int)SignalStatus.LinkIdle => "Link idle",
        _ => "Unknown signal status"
    };

    private static void Exit(int exitCode) => throw new V42TestsExitException(exitCode);

    private sealed class CCompatibleRandom
    {
        private uint _state;

        public CCompatibleRandom(uint seed) => _state = seed;

        public int Next()
        {
            _state = unchecked(_state * 1_103_515_245u + 12_345u);
            return unchecked((int)((_state >> 16) & 0x7FFFu));
        }
    }
}
