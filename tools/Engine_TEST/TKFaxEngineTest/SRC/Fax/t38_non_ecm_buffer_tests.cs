/*
 * TKFaxEngine - managed C# port of t38_non_ecm_buffer_tests.c.
 *
 * The complete spanDSP non-ECM rate-adapting buffer test sequence is retained,
 * including image EOL recognition, row padding, flow-control fill, RTC and TCF.
 */

#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using TKFaxEngine.Daten.T38;
using static TKFaxEngine.Daten.T38.T38NonEcmBuffer;

namespace TKFaxEngineTest.Fax.Test;

public static class t38_non_ecm_buffer_tests {
    private static readonly byte[] Spreader =
    {
        0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55,
        0x22, 0x10, 0x40, 0x80, 0x80, 0x40, 0x10, 0x02, 0x00, 0x25
    };

    private static int _bitNumber;

    public sealed class T38NonEcmBufferTestsExitException : Exception {
        public T38NonEcmBufferTestsExitException(int exitCode)
            : base($"t38_non_ecm_buffer_tests exited with code {exitCode}.") => ExitCode = exitCode;

        public int ExitCode { get; }
    }

    public static int Main(string[] args) {
        try {
            return tkfx_t38_non_ecm_buffer_tests_body_main(args);
        } catch (T38NonEcmBufferTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_t38_non_ecm_buffer_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        bool logBits = testArguments.Length > 0;
        T38Log logging = new();
        logging.Sink = (level, message) => Console.WriteLine("Buffer: {0}: {1}", level, message);

        Console.WriteLine("T.38 non-ECM rate adapting buffer tests.");
        TestNoMinimum(logBits, logging);
        TestAlignment(logBits, logging, minimumBitsPerRow: 0, expectedEndCount: 337);
        TestAlignment(logBits, logging, minimumBitsPerRow: 400, expectedEndCount: 1273);
        TestImageStartup(logBits, logging, outputAhead: true);
        TestImageStartup(logBits, logging, outputAhead: false);
        TestTcfWithoutLeadingOnes(logBits, logging);
        TestTcfWithLeadingOnes(logBits, logging);

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static void TestNoMinimum(bool logBits, T38Log logging) {
        Console.WriteLine("1 - Impose no minimum for the bits per row");
        T38NonEcmBufferState buffer = t38_non_ecm_buffer_init(null, true, 0);
        _bitNumber = 0;

        Console.WriteLine("    We should get ones here");
        for (int index = 0; index < 17; index++)
            SpreaderStep(buffer, logBits, index, 1);

        Console.WriteLine("    We should change to zeros here");
        SpreaderStep(buffer, logBits, 17, 0);
        for (int index = 0; index < 17; index++)
            SpreaderStep(buffer, logBits, index, 0);

        Console.WriteLine("    We should get the first row here");
        SpreaderStep(buffer, logBits, 17, -1);
        for (int index = 0; index < 17; index++)
            SpreaderStep(buffer, logBits, index, 0);

        Console.WriteLine("    We should get the second row here");
        SpreaderStep(buffer, logBits, 17, -1);
        for (int index = 0; index < 17; index++)
            SpreaderStep(buffer, logBits, index, 0);

        Console.WriteLine("    We should get the third row here");
        SpreaderStep(buffer, logBits, 17, -1);
        Console.WriteLine("    Done");
        ReportAndRelease(buffer, logging);
    }

    private static void TestAlignment(
        bool logBits,
        T38Log logging,
        int minimumBitsPerRow,
        int expectedEndCount) {
        if (minimumBitsPerRow == 0)
            Console.WriteLine("2 - Impose no minimum for the bits per row, different alignment");
        else
            Console.WriteLine("3 - Demand a fairly high minimum for the bits per row");

        T38NonEcmBufferState buffer = t38_non_ecm_buffer_init(null, true, minimumBitsPerRow);
        byte[] data = new byte[1024];
        data.AsSpan(10, 10).Fill(0x55);
        data[25] = minimumBitsPerRow == 0 ? (byte)0x20 : (byte)0x08;
        data.AsSpan(30, 10).Fill(0x55);
        data[45] = minimumBitsPerRow == 0 ? (byte)0x10 : (byte)0x04;
        t38_non_ecm_buffer_inject(buffer, data, 50);
        t38_non_ecm_buffer_push(buffer);

        int count = 0;
        for (; ; )
        {
            int bit = GetBit(buffer, logBits, count);
            count++;
            if (bit == (int)SignalStatus.EndOfData) {
                Require(count == expectedEndCount);
                break;
            }

            bool alternating = minimumBitsPerRow == 0
                ? count is >= 18 and <= 96 or >= 178 and <= 256
                : count is >= 18 and <= 96 or >= 834 and <= 912;
            bool onePosition = minimumBitsPerRow == 0
                ? count is 139 or 300
                : count is 429 or 1238;

            if (alternating)
                Require(bit != (count & 1));
            else if (onePosition)
                Require(bit == 1);
            else
                Require(bit == 0);
        }

        ReportAndRelease(buffer, logging);
    }

    private static void TestImageStartup(bool logBits, T38Log logging, bool outputAhead) {
        Console.WriteLine(outputAhead
            ? "4 - Take some time to get to the first row of the image, output ahead"
            : "5 - Take some time to get to the first row of the image, output behind");

        T38NonEcmBufferState buffer = t38_non_ecm_buffer_init(null, true, 400);
        byte[] data = new byte[1024];
        int displayedBit = 0;

        if (outputAhead) {
            ExpectRepeatedBit(buffer, 1000, 1, logBits, ref displayedBit);
            Console.WriteLine("    Initial ones OK");

            t38_non_ecm_buffer_inject(buffer, data, 20);
            ExpectRepeatedBit(buffer, 1000, 1, logBits, ref displayedBit);
            Console.WriteLine("    Continuing initial ones OK");
        } else {
            data.AsSpan(0, 100).Fill(0xFF);
            t38_non_ecm_buffer_inject(buffer, data, 100);
            Array.Clear(data, 0, data.Length);
            t38_non_ecm_buffer_inject(buffer, data, 100);
            ExpectRepeatedBit(buffer, 1000, 1, logBits, ref displayedBit);
            Console.WriteLine("    Initial ones OK");
        }

        data[0] = 0x01;
        t38_non_ecm_buffer_inject(buffer, data, 1);
        ExpectRepeatedBit(buffer, 1000, 0, logBits, ref displayedBit);
        Console.WriteLine("    First EOL caused zeros to output OK");

        data[0] = 0x00;
        data[4] = 0x01;
        t38_non_ecm_buffer_inject(buffer, data, 5);
        for (int index = 0; index < 1000; index++) {
            int bit = GetBit(buffer, logBits, displayedBit++);
            Require(index == 23 ? bit == 1 : bit == 0, index);
        }
        Console.WriteLine("    Second EOL caused the first row to output OK");

        BuildRtc(data);
        t38_non_ecm_buffer_inject(buffer, data, 50);
        t38_non_ecm_buffer_push(buffer);
        for (int index = 0; index < 1000; index++) {
            int bit = GetBit(buffer, logBits, displayedBit++);
            Require(IsRtcOnePosition(index) ? bit == 1 : bit == 0, index);
        }
        Console.WriteLine("    RTC output OK");

        ReportAndRelease(buffer, logging);
    }

    private static void TestTcfWithoutLeadingOnes(bool logBits, T38Log logging) {
        Console.WriteLine("6 - TCF without leading ones");
        T38NonEcmBufferState buffer = t38_non_ecm_buffer_init(null, false, 400);
        byte[] data = new byte[1024];
        int displayedBit = 0;

        ExpectRepeatedBit(buffer, 1000, 1, logBits, ref displayedBit);
        Console.WriteLine("    Initial ones from an empty TCF buffer OK");

        t38_non_ecm_buffer_inject(buffer, data, 500);
        t38_non_ecm_buffer_push(buffer);
        ExpectRepeatedBit(buffer, 500 * 8, 0, logBits, ref displayedBit);
        Console.WriteLine("    Passthrough of TCF OK");

        int bit = GetBit(buffer, logBits, displayedBit++);
        Require(bit == (int)SignalStatus.EndOfData);
        Console.WriteLine("    End of data seen OK");
        ReportAndRelease(buffer, logging);
    }

    private static void TestTcfWithLeadingOnes(bool logBits, T38Log logging) {
        Console.WriteLine("7 - TCF with leading ones");
        T38NonEcmBufferState buffer = t38_non_ecm_buffer_init(null, false, 400);
        byte[] data = new byte[1024];
        int displayedBit = 0;

        ExpectRepeatedBit(buffer, 1000, 1, logBits, ref displayedBit);
        Console.WriteLine("    Initial ones from an empty TCF buffer OK");

        data.AsSpan(0, 500).Fill(0xFF);
        t38_non_ecm_buffer_inject(buffer, data, 500);
        ExpectRepeatedBit(buffer, 500 * 8, 1, logBits, ref displayedBit);
        Console.WriteLine("    Sustaining ones OK");

        Array.Clear(data, 0, data.Length);
        data.AsSpan(0, 100).Fill(0xFF);
        data[100] = 0xF0;
        t38_non_ecm_buffer_inject(buffer, data, 500);
        t38_non_ecm_buffer_push(buffer);
        for (int index = 0; index < 400 * 8; index++) {
            int bit = GetBit(buffer, logBits, displayedBit++);
            Require(index < 4 ? bit == 1 : bit == 0);
        }
        Console.WriteLine("    Passthrough of TCF OK");

        int finalBit = GetBit(buffer, logBits, displayedBit++);
        Require(finalBit == (int)SignalStatus.EndOfData);
        Console.WriteLine("    End of data seen OK");
        ReportAndRelease(buffer, logging);
    }

    private static int SpreaderStep(
        T38NonEcmBufferState state,
        bool logBits,
        int spreaderIndex,
        int expected) {
        t38_non_ecm_buffer_inject(state, new[] { Spreader[spreaderIndex] }, 1);
        if (expected >= 0) {
            for (int index = 0; index < 128; index++) {
                int bit = GetBit(state, logBits, _bitNumber);
                if (bit != expected)
                    Fail($"Tests failed - {_bitNumber} {bit} {expected}");
                _bitNumber++;
            }
            return 0;
        }

        int sourceBit = -1;
        for (int index = 0; index < 256; index++) {
            int bit = GetBit(state, logBits, _bitNumber);
            if (sourceBit < 0) {
                if (bit == 1)
                    sourceBit = 18 * 8 - 5;
            } else {
                int wanted = (Spreader[sourceBit >> 3] >> (7 - (sourceBit & 7))) & 1;
                if (bit != wanted)
                    Fail($"Tests failed - {_bitNumber} {bit} {wanted}");
                sourceBit++;
                if (sourceBit >= 18 * 8)
                    sourceBit = 0;
            }

            _bitNumber++;
            if (sourceBit == 17 * 8)
                return 0;
        }

        return 0;
    }

    private static void BuildRtc(byte[] data) {
        Array.Clear(data, 0, data.Length);
        for (int index = 10; index < 19; index += 3) {
            data[index] = 0x00;
            data[index + 1] = 0x10;
            data[index + 2] = 0x01;
        }

        data[25] = 0x00;
        data[26] = 0x18;
        data[27] = 0x00;
        data[28] = 0xC0;
        data[29] = 0x06;
        data[30] = 0x00;
        data[31] = 0x30;
        data[32] = 0x01;
        data[33] = 0x80;
        data[34] = 0x0C;
    }

    private static bool IsRtcOnePosition(int index) {
        if (index == 7)
            return true;

        for (int row = 0; row < 6; row++) {
            if (index == 400 + 11 + row * 12)
                return true;
        }

        int secondBase = 400 + 11 + 60 + 400 + 4;
        for (int row = 0; row < 6; row++) {
            int position = secondBase + row * 13;
            if (index == position || index == position + 1)
                return true;
        }

        return false;
    }

    private static void ExpectRepeatedBit(
        T38NonEcmBufferState state,
        int count,
        int expected,
        bool logBits,
        ref int displayedBit) {
        for (int index = 0; index < count; index++) {
            int bit = GetBit(state, logBits, displayedBit++);
            Require(bit == expected);
        }
    }

    private static int GetBit(T38NonEcmBufferState state, bool logBits, int bitNumber) {
        int bit = t38_non_ecm_buffer_get_bit(state);
        if (logBits)
            Console.WriteLine("Rx bit {0} - {1}", bitNumber, bit);
        return bit;
    }

    private static void ReportAndRelease(T38NonEcmBufferState state, T38Log logging) {
        t38_non_ecm_buffer_report_input_status(state, logging);
        t38_non_ecm_buffer_report_output_status(state, logging);
        _ = t38_non_ecm_buffer_release(state);
    }

    private static void Require(bool condition, int? index = null) {
        if (!condition)
            Fail(index.HasValue ? $"Tests failed ({index.Value})" : "Tests failed");
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        Exit(2);
    }

    private static void Exit(int code) => throw new T38NonEcmBufferTestsExitException(code);
}
