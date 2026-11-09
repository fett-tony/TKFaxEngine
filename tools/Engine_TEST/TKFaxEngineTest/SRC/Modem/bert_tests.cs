/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * bert_tests.cs - Tests for the bit-error-rate tester.
 *
 * Original C test written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004 Steve Underwood.
 *
 * Managed C# port. All fixed patterns, complete ITU PRBS cycles,
 * QBF and the 15,000,000-bit error-rate test are retained.
 */

#nullable enable

using TKFaxEngine.Modem;

namespace TKFaxEngineTest.Modem.Test;

public static class bert_tests {
    private static readonly sbyte[] test = new sbyte[0x800000];
    private static uint randomState = 1_234_567u;

    public static int main(string[] testArguments) {
        try {
            return tkfx_bert_tests_body_main(testArguments);
        } catch (BertTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_bert_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        RunFixedPattern("Zeros:    ", BertPattern.Zeros);
        RunFixedPattern("Ones:     ", BertPattern.Ones);
        RunFixedPattern("1 to 7:   ", BertPattern.OneToSeven);
        RunFixedPattern("1 to 3:   ", BertPattern.OneToThree);
        RunFixedPattern("1 to 1:   ", BertPattern.OneToOne);
        RunFixedPattern("3 to 1:   ", BertPattern.ThreeToOne);
        RunFixedPattern("7 to 1:   ", BertPattern.SevenToOne);

        RunPrbsPattern(
            "O.153(9): ",
            BertPattern.ItuO1539,
            registerCount: 0x200,
            period: 511,
            expectedTotalBits: 986,
            initialRegister: 0x1FFu,
            shift: 4,
            shift2: 8,
            invert: 0,
            maximumZeros: 0,
            failOnCycleMismatch: true);

        RunPrbsPattern(
            "O.152(11):",
            BertPattern.ItuO15211,
            registerCount: 0x800,
            period: 2047,
            expectedTotalBits: 4052,
            initialRegister: 0x7FFu,
            shift: 2,
            shift2: 10,
            invert: 0,
            maximumZeros: 0,
            failOnCycleMismatch: true);

        RunPrbsPattern(
            "O.151(15):",
            BertPattern.ItuO15115,
            registerCount: 0x8000,
            period: 32767,
            expectedTotalBits: 65480,
            initialRegister: 0x7FFFu,
            shift: 1,
            shift2: 14,
            invert: 1,
            maximumZeros: 0,
            failOnCycleMismatch: true);

        RunPrbsPattern(
            "O.151(20):",
            BertPattern.ItuO15120,
            registerCount: 0x100000,
            period: 1048575,
            expectedTotalBits: 2097066,
            initialRegister: 0xFFFFFu,
            shift: 3,
            shift2: 19,
            invert: 1,
            maximumZeros: 14,
            failOnCycleMismatch: false);

        RunPrbsPattern(
            "O.151(23):",
            BertPattern.ItuO15123,
            registerCount: 0x800000,
            period: 8388607,
            expectedTotalBits: 16777136,
            initialRegister: 0x7FFFFFu,
            shift: 5,
            shift2: 22,
            invert: 1,
            maximumZeros: 0,
            failOnCycleMismatch: false);

        RunQuickBrownFox();
        RunRateClassificationTest();

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void RunFixedPattern(string label, BertPattern pattern) {
        BertState txBert = Bert.Initialize(null, 0, pattern, 300, 20);
        BertState rxBert = Bert.Initialize(null, 0, pattern, 300, 20);

        for (int i = 0; i < 511 * 2; i++) {
            int bit = Bert.GetBit(txBert);
            Bert.PutBit(rxBert, bit);
        }

        var results = new BertResults();
        Bert.Result(rxBert, results);
        Console.WriteLine($"{label} Bad bits {results.BadBits}/{results.TotalBits}");

        if (results.BadBits != 0 || results.TotalBits != 950)
            Fail("Test failed.");

        Bert.Free(txBert);
        Bert.Free(rxBert);
    }

    private static void RunPrbsPattern(
        string label,
        BertPattern pattern,
        int registerCount,
        int period,
        int expectedTotalBits,
        uint initialRegister,
        int shift,
        int shift2,
        int invert,
        int maximumZeros,
        bool failOnCycleMismatch) {
        BertState txBert = Bert.Initialize(null, 0, pattern, 300, 20);
        BertState rxBert = Bert.Initialize(null, 0, pattern, 300, 20);

        Array.Clear(test, 0, registerCount);

        uint expectedRegister = initialRegister;
        int expectedGeneratorZeros = 0;
        int zeros = 0;
        int maxZeros = 0;

        for (int i = 0; i < period * 2; i++) {
            int bit = Bert.GetBit(txBert);
            int expectedBit = NextPrbsBit(
                ref expectedRegister,
                ref expectedGeneratorZeros,
                shift,
                shift2,
                invert,
                maximumZeros);

            if (bit != expectedBit) {
                Console.WriteLine(
                    $"PRBS generator mismatch at bit {i}: received {bit}, expected {expectedBit}");
                Fail("Test failed.");
            }

            if (bit != 0) {
                if (zeros > maxZeros)
                    maxZeros = zeros;

                zeros = 0;
            } else {
                zeros++;
            }

            Bert.PutBit(rxBert, bit);
            test[(int)expectedRegister]++;
        }

        bool failed = false;
        if (test[0] != 0) {
            Console.WriteLine($"XXX 0 {test[0]}");
            failed = true;
        }

        for (int i = 1; i < registerCount; i++) {
            if (test[i] == 2)
                continue;

            Console.WriteLine($"XXX {i} {test[i]}");
            if (failOnCycleMismatch)
                failed = true;
        }

        var results = new BertResults();
        Bert.Result(rxBert, results);
        Console.WriteLine(
            $"{label} Bad bits {results.BadBits}/{results.TotalBits}, max zeros {maxZeros}");

        if (results.BadBits != 0 ||
            results.TotalBits != expectedTotalBits ||
            failed) {
            Fail("Test failed.");
        }

        Bert.Free(txBert);
        Bert.Free(rxBert);
    }

    private static int NextPrbsBit(
        ref uint register,
        ref int generatorZeros,
        int shift,
        int shift2,
        int invert,
        int maximumZeros) {
        int bit = unchecked((int)(register & 1u));
        register = unchecked(
            (register >> 1) |
            (((register ^ (register >> shift)) & 1u) << shift2));

        if (maximumZeros != 0) {
            if (bit != 0) {
                if (++generatorZeros > maximumZeros) {
                    generatorZeros = 0;
                    bit ^= 1;
                }
            } else {
                generatorZeros = 0;
            }
        }

        return bit ^ invert;
    }

    private static void RunQuickBrownFox() {
        BertState txBert = Bert.Initialize(null, 0, BertPattern.QuickBrownFox, 300, 20);
        BertState rxBert = Bert.Initialize(null, 0, BertPattern.QuickBrownFox, 300, 20);

        for (int i = 0; i < 100_000; i++) {
            int bit = Bert.GetBit(txBert);
            Bert.PutBit(rxBert, bit);
        }

        var results = new BertResults();
        Bert.Result(rxBert, results);
        Console.WriteLine($"QBF:       Bad bits {results.BadBits}/{results.TotalBits}");

        if (results.BadBits != 0 || results.TotalBits != 100_000)
            Fail("Test failed.");

        Bert.Free(txBert);
        Bert.Free(rxBert);
    }

    private static void RunRateClassificationTest() {
        BertState bert = Bert.Initialize(
            null,
            15_000_000,
            BertPattern.ItuO15211,
            300,
            20);

        Bert.SetReport(bert, 100_000, reporter, 0);

        for (; ; )
        {
            int bit = Bert.GetBit(bert);
            if (bit == Bert.SignalStatusEndOfData) {
                var results = new BertResults();
                Bert.Result(bert, results);
                Console.WriteLine(
                    $"Rate test: {results.TotalBits} bits, {results.BadBits} bad bits, " +
                    $"{results.Resyncs} resyncs");

                if (results.TotalBits != 15_000_000 - 42 ||
                    results.BadBits != 58 ||
                    results.Resyncs != 0) {
                    Fail("Tests failed");
                }

                break;
            }

            if ((my_rand() & 0x3FFFF) == 0)
                bit ^= 1;

            Bert.PutBit(bert, bit);
        }

        Bert.Free(bert);
    }

    private static int my_rand() {
        randomState = unchecked(1_664_525u * randomState + 1_013_904_223u);
        return unchecked((int)randomState);
    }

    private static void reporter(object? userData, int reason, BertResults results) {
        int channel = userData is null ? 0 : Convert.ToInt32(userData);
        Console.Write($"{channel}: BERT report '{Bert.EventToString(reason)}' ");

        if (reason == (int)BertReportEvent.Regular) {
            Console.Write(
                $"{results.TotalBits} bits, {results.BadBits} bad bits, " +
                $"{results.Resyncs} resyncs");
        }

        Console.Write('\r');
    }

    private static void Fail(string message) {
        Console.WriteLine(message);
        throw new BertTestsExitException(2);
    }

    public sealed class BertTestsExitException : Exception {
        public BertTestsExitException(int exitCode)
            : base($"bert_tests requested exit code {exitCode}.") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
