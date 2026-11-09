/*
 * TKFaxEngine - managed C# port of modem_connect_tones_tests.c.
 *
 * The complete native test flow is retained: generation, frequency and level
 * sweeps, V.21 preamble detection, phase-reversal interval tests, ANSam pitch
 * and depth tests, Bellcore/Mitel talk-off tests, and arbitrary WAV decoding.
 */

#nullable enable

using System.Reflection;
using TKFaxEngine.Audio;
using TKFaxEngine.Modem;

namespace TKFaxEngineTest.Modem.Test;

public static class modem_connect_tones_tests {
    private const int SamplesPerChunk = 160;
    private const int SampleRate = 8000;
    private const string OutputFileName = "modem_connect_tones.wav";

    private const int LevelMax = -5;
    private const int LevelMin = -48;
    private const int LevelMinAccept = -43;
    private const int LevelMinReject = -44;

    private const int CedFrequencyTolerance = 46;
    private const int CedFrequencyBlackout = 80;
    private const int CngFrequencyTolerance = 23;
    private const int CngFrequencyBlackout = 80;
    private const int AmFrequencyTolerance = 1;
    private const int BellAnsFrequencyTolerance = 23;
    private const int BellAnsFrequencyBlackout = 80;
    private const int CallingToneFrequencyTolerance = 23;
    private const int CallingToneFrequencyBlackout = 80;

    private const int PerformTest1A = 1 << 1;
    private const int PerformTest1B = 1 << 2;
    private const int PerformTest1C = 1 << 3;
    private const int PerformTest1D = 1 << 4;
    private const int PerformTest1E = 1 << 5;
    private const int PerformTest1F = 1 << 6;
    private const int PerformTest1G = 1 << 7;
    private const int PerformTest2A = 1 << 8;
    private const int PerformTest2B = 1 << 9;
    private const int PerformTest2C = 1 << 10;
    private const int PerformTest2D = 1 << 11;
    private const int PerformTest2E = 1 << 12;
    private const int PerformTest2F = 1 << 13;
    private const int PerformTest2G = 1 << 14;
    private const int PerformTest3A = 1 << 15;
    private const int PerformTest3B = 1 << 16;
    private const int PerformTest3C = 1 << 17;
    private const int PerformTest3D = 1 << 18;
    private const int PerformTest3E = 1 << 19;
    private const int PerformTest3F = 1 << 20;
    private const int PerformTest3G = 1 << 21;
    private const int PerformTest4 = 1 << 22;
    private const int PerformTest5A = 1 << 23;
    private const int PerformTest5B = 1 << 24;
    private const int PerformTest6A = 1 << 25;
    private const int PerformTest6B = 1 << 26;
    private const int PerformTest7A = 1 << 27;
    private const int PerformTest7B = 1 << 28;
    private const int PerformTest8 = 1 << 29;

    private static readonly string[] BellcoreFiles =
    {
        "Test-Files/mitel/mitel-cm7291-talkoff.wav",
        "Test-Files/bellcore/tr-tsy-00763-1.wav",
        "Test-Files/bellcore/tr-tsy-00763-2.wav",
        "Test-Files/bellcore/tr-tsy-00763-3.wav",
        "Test-Files/bellcore/tr-tsy-00763-4.wav",
        "Test-Files/bellcore/tr-tsy-00763-5.wav",
        "Test-Files/bellcore/tr-tsy-00763-6.wav"
    };

    private static readonly Dictionary<string, int> TestFlags =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {
            ["1a"] = PerformTest1A,
            ["1b"] = PerformTest1B,
            ["1c"] = PerformTest1C,
            ["1d"] = PerformTest1D,
            ["1e"] = PerformTest1E,
            ["1f"] = PerformTest1F,
            ["1g"] = PerformTest1G,
            ["2a"] = PerformTest2A,
            ["2b"] = PerformTest2B,
            ["2c"] = PerformTest2C,
            ["2d"] = PerformTest2D,
            ["2e"] = PerformTest2E,
            ["2f"] = PerformTest2F,
            ["2g"] = PerformTest2G,
            ["3a"] = PerformTest3A,
            ["3b"] = PerformTest3B,
            ["3c"] = PerformTest3C,
            ["3d"] = PerformTest3D,
            ["3e"] = PerformTest3E,
            ["3f"] = PerformTest3F,
            ["3g"] = PerformTest3G,
            ["4"] = PerformTest4,
            ["5a"] = PerformTest5A,
            ["5b"] = PerformTest5B,
            ["6a"] = PerformTest6A,
            ["6b"] = PerformTest6B,
            ["7a"] = PerformTest7A,
            ["7b"] = PerformTest7B,
            ["8"] = PerformTest8
        };

    private static int _preambleCount;
    private static int _preambleBitNumber;
    private static int _preambleOnAt = -1;
    private static int _preambleOffAt = -1;
    private static int _hits;
    private static int _when;
    private static readonly Random RandomBits = new(1);

    public static int main(string[] testArguments) {
        try { return tkfx_modem_connect_tones_tests_body_main(testArguments); } catch (ModemConnectTonesTestsExitException ex) { return ex.ExitCode; }
    }

    public static int tkfx_modem_connect_tones_tests_body_main(string[] testArguments) {
        ParseArguments(testArguments, out int testList, out string? decodeTestFile);
        if (decodeTestFile is null && testList == 0) testList = unchecked((int)0xFFFFFFFF);

        var amplitude = new short[SampleRate];
        using (Pcm16WaveWriter output = CreateOutputWriter()) {
            RunGenerationTests(testList, amplitude, output);
        }

        if ((testList & PerformTest2A) != 0)
            RunFrequencyTest("Test 2a: CNG detection with frequency", ModemConnectTone.FaxCng,
                ModemConnectTone.FaxCng, ModemConnectTone.FaxCng, 1100, 600, 1600, true,
                CedFrequencyTolerance, CedFrequencyBlackout, -50.0, 10, true, amplitude);
        if ((testList & PerformTest2B) != 0)
            RunFrequencyTest("Test 2b: CED/ANS detection with frequency", ModemConnectTone.Ans,
                ModemConnectTone.FaxCedOrPreamble, ModemConnectTone.FaxCed, 2100, 1600, 2600, false,
                CngFrequencyTolerance, CngFrequencyBlackout, -50.0, 10, false, amplitude);
        if ((testList & PerformTest2C) != 0)
            RunFrequencyTest("Test 2c: ANSam detection with frequency", ModemConnectTone.Ansam,
                ModemConnectTone.Ansam, ModemConnectTone.Ansam, 2100, 2000, 2200, true,
                CngFrequencyTolerance, CngFrequencyBlackout, -50.0, 10, false, amplitude);
        if ((testList & PerformTest2D) != 0)
            RunFrequencyTest("Test 2d: ANS/ (EC-disable) detection with frequency", ModemConnectTone.AnsWithPhaseReversals,
                ModemConnectTone.AnsWithPhaseReversals, ModemConnectTone.AnsWithPhaseReversals, 2100, 2000, 2200, true,
                CngFrequencyTolerance, CngFrequencyBlackout, -50.0, 10, false, amplitude);
        if ((testList & PerformTest2E) != 0)
            RunFrequencyTest("Test 2e: ANSam/ (Modulated EC-disable) detection with frequency", ModemConnectTone.AnsamWithPhaseReversals,
                ModemConnectTone.AnsamWithPhaseReversals, ModemConnectTone.AnsamWithPhaseReversals, 2100, 2000, 2200, true,
                CngFrequencyTolerance, CngFrequencyBlackout, -50.0, 10, false, amplitude);
        if ((testList & PerformTest2F) != 0)
            RunFrequencyTest("Test 2f: Bell answer tone detection with frequency", ModemConnectTone.BellAns,
                ModemConnectTone.BellAns, ModemConnectTone.BellAns, 2225, 1725, 2725, true,
                BellAnsFrequencyTolerance, BellAnsFrequencyBlackout, -50.0, 1, true, amplitude);
        if ((testList & PerformTest2G) != 0)
            RunFrequencyTest("Test 2g: Calling tone detection with frequency", ModemConnectTone.CallingTone,
                ModemConnectTone.CallingTone, ModemConnectTone.CallingTone, 1300, 800, 1800, true,
                CallingToneFrequencyTolerance, CallingToneFrequencyBlackout, -50.0, 10, true, amplitude);

        if ((testList & PerformTest3A) != 0)
            RunLevelTest("Test 3a: CNG detection with level", ModemConnectTone.FaxCng,
                ModemConnectTone.FaxCng, ModemConnectTone.FaxCng, 1100, CedFrequencyTolerance, amplitude);
        if ((testList & PerformTest3B) != 0)
            RunLevelTest("Test 3b: CED/ANS detection with level", ModemConnectTone.Ans,
                ModemConnectTone.FaxCedOrPreamble, ModemConnectTone.FaxCed, 2100, CngFrequencyTolerance, amplitude);
        if ((testList & PerformTest3C) != 0)
            RunLevelTest("Test 3c: ANSam detection with level", ModemConnectTone.Ansam,
                ModemConnectTone.Ansam, ModemConnectTone.Ansam, 2100, CngFrequencyTolerance, amplitude);
        if ((testList & PerformTest3D) != 0)
            RunLevelTest("Test 3d: ANS/ (EC-disable) detection with level", ModemConnectTone.AnsWithPhaseReversals,
                ModemConnectTone.AnsWithPhaseReversals, ModemConnectTone.AnsWithPhaseReversals, 2100, CngFrequencyTolerance, amplitude);
        if ((testList & PerformTest3E) != 0)
            RunLevelTest("Test 3e: ANSam/ (Modulated EC-disable) detection with level", ModemConnectTone.AnsamWithPhaseReversals,
                ModemConnectTone.AnsamWithPhaseReversals, ModemConnectTone.AnsamWithPhaseReversals, 2100, CngFrequencyTolerance, amplitude);
        if ((testList & PerformTest3F) != 0)
            RunLevelTest("Test 3f: Bell answer tone detection with level", ModemConnectTone.BellAns,
                ModemConnectTone.BellAns, ModemConnectTone.BellAns, 2225, BellAnsFrequencyTolerance, amplitude);
        if ((testList & PerformTest3G) != 0)
            RunLevelTest("Test 3g: Calling tone detection with level", ModemConnectTone.CallingTone,
                ModemConnectTone.CallingTone, ModemConnectTone.CallingTone, 1300, CallingToneFrequencyTolerance, amplitude);

        if ((testList & PerformTest4) != 0) RunPreambleTest(amplitude);
        if ((testList & PerformTest5A) != 0) RunReversalIntervalTest(amplitude);
        if ((testList & PerformTest5B) != 0) RunMixedReversalIntervalTest(amplitude);
        if ((testList & PerformTest6A) != 0) RunAmPitchTest("Test 6a: ANSam detection with AM pitch", ModemConnectTone.Ansam, amplitude);
        if ((testList & PerformTest6B) != 0) RunAmPitchTest("Test 6b: ANSam/ (Modulated EC-disable) detection with AM pitch", ModemConnectTone.AnsamWithPhaseReversals, amplitude);
        if ((testList & PerformTest7A) != 0) RunAmDepthTest("Test 7a: ANSam detection with AM depth", ModemConnectTone.Ansam, amplitude);
        if ((testList & PerformTest7B) != 0) RunAmDepthTest("Test 7b: ANSam/ (Modulated EC-disable) detection with AM depth", ModemConnectTone.AnsamWithPhaseReversals, amplitude);
        if ((testList & PerformTest8) != 0) RunTalkOffTest(amplitude);
        if (decodeTestFile is not null) DecodeFile(decodeTestFile, amplitude);

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void ParseArguments(string[] testArguments, out int testList, out string? decodeFile) {
        testList = 0;
        decodeFile = null;
        int count = testArguments.Length;
        int index = 0;
        while (index < count) {
            string arg = testArguments[index++];
            if (arg == "-d") {
                if (index >= count) Exit(2);
                decodeFile = testArguments[index++];
                continue;
            }
            if (arg.StartsWith("-d", StringComparison.Ordinal) && arg.Length > 2) {
                decodeFile = arg[2..];
                continue;
            }
            if (!TestFlags.TryGetValue(arg, out int flag)) {
                Console.Error.WriteLine($"Unknown test '{arg}' specified");
                Exit(2);
            }
            testList |= flag;
        }
    }

    private static Pcm16WaveWriter CreateOutputWriter() {
        try { return new Pcm16WaveWriter(OutputFileName, SampleRate, 1); } catch {
            Console.Error.WriteLine($"    Cannot create audio file '{OutputFileName}'");
            Exit(2);
            throw;
        }
    }

    private static void RunGenerationTests(int testList, short[] amplitude, Pcm16WaveWriter output) {
        GenerateIfSelected(testList, PerformTest1A, "Test 1a: CNG generation to a file", ModemConnectTone.FaxCng, amplitude, output);
        GenerateIfSelected(testList, PerformTest1B, "Test 1b: CED/ANS generation to a file", ModemConnectTone.Ans, amplitude, output);
        GenerateIfSelected(testList, PerformTest1C, "Test 1c: ANSam (Modulated ANS) generation to a file", ModemConnectTone.Ansam, amplitude, output);
        GenerateIfSelected(testList, PerformTest1D, "Test 1d: ANS/ (EC-disable) generation to a file", ModemConnectTone.AnsWithPhaseReversals, amplitude, output);
        GenerateIfSelected(testList, PerformTest1E, "Test 1e: ANSam/ (Modulated EC-disable) generation to a file", ModemConnectTone.AnsamWithPhaseReversals, amplitude, output);
        GenerateIfSelected(testList, PerformTest1F, "Test 1f: Bell answer tone generation to a file", ModemConnectTone.BellAns, amplitude, output);
        GenerateIfSelected(testList, PerformTest1G, "Test 1g: Calling tone generation to a file", ModemConnectTone.CallingTone, amplitude, output);
    }

    private static void GenerateIfSelected(int testList, int flag, string label, ModemConnectTone tone, short[] amplitude, Pcm16WaveWriter output) {
        if ((testList & flag) == 0) return;
        Console.WriteLine(label);
        using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(tone);
        for (int i = 0; i < 20 * SampleRate; i += SamplesPerChunk) {
            int samples = ModemConnectTones.Transmit(tx, amplitude.AsSpan(0, SamplesPerChunk));
            if (output.WriteFrames(amplitude, samples) != samples) {
                Console.Error.WriteLine("    Error writing audio file");
                Exit(2);
            }
        }
    }

    private static void RunFrequencyTest(
        string label,
        ModemConnectTone transmitTone,
        ModemConnectTone receiveTone,
        ModemConnectTone expectedTone,
        int centerFrequency,
        int firstPitch,
        int endPitch,
        bool inclusiveEnd,
        int tolerance,
        int blackout,
        double noiseLevel,
        int seconds,
        bool trackLevel,
        short[] amplitude) {
        Console.WriteLine(label);
        var noise = new Awgn(7162534, noiseLevel);
        bool falseHit = false;
        bool falseMiss = false;
        int finalPitch = inclusiveEnd ? endPitch + 1 : endPitch;
        for (int pitch = firstPitch; pitch < finalPitch; pitch++) {
            using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(transmitTone);
            SetInternalProperty(tx, nameof(ModemConnectTonesTxState.TonePhaseRate), Dds.PhaseRate(pitch));
            using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(receiveTone, null, null);
            int level2 = 0;
            int maxLevel2 = 0;
            for (int i = 0; i < seconds * SampleRate; i += SamplesPerChunk) {
                int samples = ModemConnectTones.Transmit(tx, amplitude.AsSpan(0, SamplesPerChunk));
                AddNoise(amplitude, samples, noise, trackLevel, ref level2, ref maxLevel2);
                ModemConnectTones.Receive(rx, amplitude.AsSpan(0, samples));
            }
            ModemConnectTone hit = ModemConnectTones.ReceiveGet(rx);
            if (pitch < centerFrequency - blackout || pitch > centerFrequency + blackout) {
                if (hit != ModemConnectTone.None) falseHit = true;
            } else if (pitch > centerFrequency - tolerance && pitch < centerFrequency + tolerance) {
                if (hit != expectedTone) falseMiss = true;
            }
            if (hit != ModemConnectTone.None)
                PrintDetectedFrequency(pitch, rx, hit);
        }
        PassOrExit(falseHit, falseMiss);
    }

    private static void RunLevelTest(
        string label,
        ModemConnectTone transmitTone,
        ModemConnectTone receiveTone,
        ModemConnectTone expectedTone,
        int centerFrequency,
        int tolerance,
        short[] amplitude) {
        Console.WriteLine(label);
        var noise = new Awgn(7162534, -60.0);
        bool falseHit = false;
        bool falseMiss = false;
        for (int pitch = centerFrequency - tolerance; pitch <= centerFrequency + tolerance; pitch += 2 * tolerance) {
            for (int level = LevelMax; level >= LevelMin; level--) {
                using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(transmitTone);
                SetInternalProperty(tx, nameof(ModemConnectTonesTxState.TonePhaseRate), Dds.PhaseRate(pitch));
                SetInternalProperty(tx, nameof(ModemConnectTonesTxState.Level), Dds.ScalingDbm0(level));
                if (transmitTone is ModemConnectTone.Ansam or ModemConnectTone.AnsamWithPhaseReversals) {
                    SetInternalProperty(
                        tx,
                        nameof(ModemConnectTonesTxState.ModulationLevel),
                        unchecked((short)(tx.Level * 20 / 100)));
                }
                using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(receiveTone, null, null);
                for (int i = 0; i < 10 * SampleRate; i += SamplesPerChunk) {
                    int samples = ModemConnectTones.Transmit(tx, amplitude.AsSpan(0, SamplesPerChunk));
                    int unusedLevel = 0;
                    int unusedMax = 0;
                    AddNoise(amplitude, samples, noise, false, ref unusedLevel, ref unusedMax);
                    ModemConnectTones.Receive(rx, amplitude.AsSpan(0, samples));
                }
                ModemConnectTone hit = ModemConnectTones.ReceiveGet(rx);
                if (level < LevelMinReject) {
                    if (hit != ModemConnectTone.None) falseHit = true;
                } else if (level > LevelMinAccept) {
                    if (hit != expectedTone) falseMiss = true;
                }
                if (hit != ModemConnectTone.None)
                    Console.WriteLine($"Detected at {pitch,5}Hz {level,4}dB {rx.ChannelLevel,12} {rx.NotchLevel,12} {ModemConnectTones.ToneToString(hit)} ({(int)hit})");
            }
        }
        PassOrExit(falseHit, falseMiss);
    }

    private static void RunPreambleTest(short[] amplitude) {
        Console.WriteLine("Test 4: CED detection, when stimulated with V.21 preamble");
        _preambleCount = 0;
        _preambleBitNumber = 0;
        _preambleOnAt = -1;
        _preambleOffAt = -1;
        using FskTxState preambleTx = Fsk.InitializeTransmitter(null, Fsk.GetPreset(FskPreset.V21Channel2), PreambleGetBit, null);
        using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(ModemConnectTone.FaxCedOrPreamble, PreambleDetected, null);
        for (int i = 0; i < 2 * SampleRate; i += SamplesPerChunk) {
            int samples = Fsk.Transmit(preambleTx, amplitude.AsSpan(0, SamplesPerChunk));
            ModemConnectTones.Receive(rx, amplitude.AsSpan(0, samples));
        }
        for (int i = 0; i < SampleRate / 10; i += SamplesPerChunk) {
            Array.Clear(amplitude, 0, SamplesPerChunk);
            ModemConnectTones.Receive(rx, amplitude.AsSpan(0, SamplesPerChunk));
        }
        if (_preambleOnAt < 40 || _preambleOnAt > 50 || _preambleOffAt < 580 || _preambleOffAt > 620) {
            Console.WriteLine("Test failed.");
            Exit(2);
        }
        Console.WriteLine("Test passed.");
    }

    private static void RunReversalIntervalTest(short[] amplitude) {
        Console.WriteLine("Test 5A: ANS and ANS/ detection with reversal interval");
        var noise = new Awgn(7162534, -60.0);
        bool falseHit = false;
        bool falseMiss = false;
        for (int interval = 400; interval < 800; interval++) {
            Console.WriteLine($"Reversal interval = {interval}");
            using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(ModemConnectTone.AnsWithPhaseReversals);
            using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals, AnsPrDetected, null);
            _hits = 0;
            uint phase = tx.TonePhase;
            int hopTimer = tx.HopTimer;
            for (int i = 0; i < 10 * SampleRate; i += SamplesPerChunk) {
                _when = i;
                for (int j = 0; j < SamplesPerChunk; j++) {
                    if (--hopTimer <= 0) {
                        hopTimer = MillisecondsToSamples(interval);
                        phase = unchecked(phase + 0x80000000U);
                    }
                    amplitude[j] = Dds.dds_mod(ref phase, tx.TonePhaseRate, tx.Level, 0);
                }
                int unusedLevel = 0;
                int unusedMax = 0;
                AddNoise(amplitude, SamplesPerChunk, noise, false, ref unusedLevel, ref unusedMax);
                ModemConnectTones.Receive(rx, amplitude.AsSpan(0, SamplesPerChunk));
            }
            if (interval < 425 || interval > 475) {
                if (_hits != 0) falseHit = true;
            } else if (interval > 425 && interval < 475) {
                if (_hits == 0) falseMiss = true;
            }
            if (_hits != 0)
                Console.WriteLine($"Detected at {2100,5}Hz {-11,4}dB {interval}ms {rx.ChannelLevel,12} {rx.NotchLevel,12} {_hits}");
        }
        PassOrExit(falseHit, falseMiss);
    }

    private static void RunMixedReversalIntervalTest(short[] amplitude) {
        Console.WriteLine("Test 5B: ANS and ANS/ detection with mixed reversal intervals");
        var noise = new Awgn(7162534, -60.0);
        int interval = 450;
        Console.WriteLine($"Reversal interval = {interval}");
        using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(ModemConnectTone.AnsWithPhaseReversals);
        using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals, AnsPrDetected, null);
        int cycle = 0;
        _hits = 0;
        uint phase = tx.TonePhase;
        int hopTimer = tx.HopTimer;
        for (int i = 0; i < 60 * SampleRate; i += SamplesPerChunk) {
            _when = i;
            for (int j = 0; j < SamplesPerChunk; j++) {
                if (--hopTimer <= 0) {
                    if (++cycle == 10) interval = 1000;
                    if (cycle == 20) interval = 450;
                    hopTimer = MillisecondsToSamples(interval);
                    phase = unchecked(phase + 0x80000000U);
                }
                amplitude[j] = Dds.dds_mod(ref phase, tx.TonePhaseRate, tx.Level, 0);
            }
            int unusedLevel = 0;
            int unusedMax = 0;
            AddNoise(amplitude, SamplesPerChunk, noise, false, ref unusedLevel, ref unusedMax);
            ModemConnectTones.Receive(rx, amplitude.AsSpan(0, SamplesPerChunk));
        }
        // The native spanDSP test is a stimulus-only test here and performs no result assertion.
        Console.WriteLine("Test passed.");
    }

    private static void RunAmPitchTest(string label, ModemConnectTone tone, short[] amplitude) {
        Console.WriteLine(label);
        var noise = new Awgn(7162534, -50.0);
        bool falseHit = false;
        bool falseMiss = false;
        for (int pitch = 5; pitch < 25; pitch++) {
            using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(tone);
            SetInternalProperty(tx, nameof(ModemConnectTonesTxState.ModulationPhaseRate), Dds.PhaseRate(pitch));
            using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(tone, null, null);
            RunToneThroughNoise(tx, rx, noise, 10, amplitude);
            ModemConnectTone hit = ModemConnectTones.ReceiveGet(rx);
            if (pitch < 5 || pitch > 25) {
                if (hit == tone) falseHit = true;
            } else if (pitch > 15 - AmFrequencyTolerance && pitch < 15 + AmFrequencyTolerance) {
                if (hit != tone) falseMiss = true;
            }
            if (hit != ModemConnectTone.None) PrintDetectedFrequency(pitch, rx, hit);
        }
        PassOrExit(falseHit, falseMiss);
    }

    private static void RunAmDepthTest(string label, ModemConnectTone tone, short[] amplitude) {
        Console.WriteLine(label);
        const int pitch = 2100;
        var noise = new Awgn(7162534, -50.0);
        bool falseHit = false;
        bool falseMiss = false;
        for (int depth = 0; depth < 40; depth++) {
            using ModemConnectTonesTxState tx = ModemConnectTones.TransmitInit(tone);
            SetInternalProperty(tx, nameof(ModemConnectTonesTxState.ModulationLevel), unchecked((short)(tx.Level * depth / 100)));
            using ModemConnectTonesRxState rx = ModemConnectTones.ReceiveInit(tone, null, null);
            RunToneThroughNoise(tx, rx, noise, 10, amplitude);
            ModemConnectTone hit = ModemConnectTones.ReceiveGet(rx);
            if (depth < 10) {
                if (hit == tone) falseHit = true;
            } else if (depth > 15) {
                if (hit != tone) falseMiss = true;
            }
            if (hit != ModemConnectTone.None) PrintDetectedFrequency(pitch, rx, hit);
        }
        PassOrExit(falseHit, falseMiss);
    }

    private static void RunToneThroughNoise(ModemConnectTonesTxState tx, ModemConnectTonesRxState rx, Awgn noise, int seconds, short[] amplitude) {
        for (int i = 0; i < seconds * SampleRate; i += SamplesPerChunk) {
            int samples = ModemConnectTones.Transmit(tx, amplitude.AsSpan(0, SamplesPerChunk));
            int unusedLevel = 0;
            int unusedMax = 0;
            AddNoise(amplitude, samples, noise, false, ref unusedLevel, ref unusedMax);
            ModemConnectTones.Receive(rx, amplitude.AsSpan(0, samples));
        }
    }

    private static void RunTalkOffTest(short[] amplitude) {
        Console.WriteLine("Test 8: Talk-off test");
        ModemConnectTonesRxState cng = ModemConnectTones.ReceiveInit(ModemConnectTone.FaxCng, null, null);
        ModemConnectTonesRxState ced = ModemConnectTones.ReceiveInit(ModemConnectTone.FaxCedOrPreamble, null, null);
        ModemConnectTonesRxState ansPr = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals, null, null);
        ModemConnectTonesRxState bell = ModemConnectTones.ReceiveInit(ModemConnectTone.BellAns, null, null);
        ModemConnectTonesRxState calling = ModemConnectTones.ReceiveInit(ModemConnectTone.CallingTone, null, null);
        try {
            foreach ((string file, int fileNumber) in EnumerateWithIndex(BellcoreFiles)) {
                using Pcm16WaveReader input = OpenInput(file);
                _when = 0;
                _hits = 0;
                int frames;
                while ((frames = input.ReadFrames(amplitude, SampleRate)) != 0) {
                    _when++;
                    ModemConnectTones.Receive(cng, amplitude.AsSpan(0, frames));
                    ModemConnectTones.Receive(ced, amplitude.AsSpan(0, frames));
                    ModemConnectTones.Receive(ansPr, amplitude.AsSpan(0, frames));
                    ModemConnectTones.Receive(bell, amplitude.AsSpan(0, frames));
                    ModemConnectTones.Receive(calling, amplitude.AsSpan(0, frames));
                    ResetOnHit(ref cng, ModemConnectTone.FaxCng, "Hit CNG");
                    ResetOnHit(ref ced, ModemConnectTone.FaxCedOrPreamble, "Hit CED");
                    ResetOnHit(ref ansPr, ModemConnectTone.AnsWithPhaseReversals, "Hit EC disable");
                    ResetOnHit(ref bell, ModemConnectTone.BellAns, "Hit calling tone");
                    ResetOnHit(ref calling, ModemConnectTone.CallingTone, "Hit calling tone");
                }
                Console.WriteLine($"    File {fileNumber + 1} gave {_hits} false hits.");
            }
            // The source checks the final file's hit count, because hits is reset for each file.
            if (_hits > 0) {
                Console.WriteLine("Test failed.");
                Exit(2);
            }
            Console.WriteLine("Test passed.");
        } finally {
            cng.Dispose();
            ced.Dispose();
            ansPr.Dispose();
            bell.Dispose();
            calling.Dispose();
        }
    }

    private static void DecodeFile(string file, short[] amplitude) {
        Console.WriteLine($"Decode file '{file}'");
        using ModemConnectTonesRxState cng = ModemConnectTones.ReceiveInit(ModemConnectTone.FaxCng, CngDetected, null);
        using ModemConnectTonesRxState ced = ModemConnectTones.ReceiveInit(ModemConnectTone.FaxCedOrPreamble, CedDetected, null);
        using ModemConnectTonesRxState ansPr = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals, AnsPrDetected, null);
        using ModemConnectTonesRxState bell = ModemConnectTones.ReceiveInit(ModemConnectTone.BellAns, BellAnsDetected, null);
        using ModemConnectTonesRxState calling = ModemConnectTones.ReceiveInit(ModemConnectTone.CallingTone, CallingToneDetected, null);
        using Pcm16WaveReader input = OpenInput(file);
        _when = 0;
        _hits = 0;
        int frames;
        while ((frames = input.ReadFrames(amplitude, SampleRate)) != 0) {
            _when += frames;
            ModemConnectTones.Receive(cng, amplitude.AsSpan(0, frames));
            ModemConnectTones.Receive(ced, amplitude.AsSpan(0, frames));
            ModemConnectTones.Receive(ansPr, amplitude.AsSpan(0, frames));
            ModemConnectTones.Receive(bell, amplitude.AsSpan(0, frames));
            ModemConnectTones.Receive(calling, amplitude.AsSpan(0, frames));
        }
        Console.WriteLine($"    File gave {_hits} hits.");
    }

    private static Pcm16WaveReader OpenInput(string file) {
        try { return new Pcm16WaveReader(file, SampleRate, 1); } catch {
            Console.Error.WriteLine($"    Cannot open speech file '{file}'");
            Exit(2);
            throw;
        }
    }

    private static void ResetOnHit(ref ModemConnectTonesRxState receiver, ModemConnectTone type, string message) {
        if (ModemConnectTones.ReceiveGet(receiver) == ModemConnectTone.None) return;
        Console.WriteLine($"{message} at {_when}s");
        _hits++;
        receiver.Dispose();
        receiver = ModemConnectTones.ReceiveInit(type, null, null);
    }

    private static IEnumerable<(string Item, int Index)> EnumerateWithIndex(IEnumerable<string> values) {
        int index = 0;
        foreach (string value in values) yield return (value, index++);
    }

    private static void AddNoise(short[] amplitude, int samples, Awgn noise, bool trackLevel, ref int level2, ref int maxLevel2) {
        for (int i = 0; i < samples; i++) {
            amplitude[i] = unchecked((short)(amplitude[i] + noise.NextSample()));
            if (!trackLevel) continue;
            level2 += (Math.Abs((int)amplitude[i]) - level2) >> 5;
            if (level2 > maxLevel2) maxLevel2 = level2;
        }
    }

    private static void PrintDetectedFrequency(int pitch, ModemConnectTonesRxState rx, ModemConnectTone hit) =>
        Console.WriteLine($"Detected at {pitch,5}Hz {rx.ChannelLevel,12} {rx.NotchLevel,12} {ModemConnectTones.ToneToString(hit)} ({(int)hit})");

    private static void PassOrExit(bool falseHit, bool falseMiss) {
        if (falseHit || falseMiss) {
            Console.WriteLine("Test failed.");
            Exit(2);
        }
        Console.WriteLine("Test passed.");
    }

    private static int PreambleGetBit(object? userData) {
        _ = userData;
        int bit;
        if (++_preambleCount < 255) {
            bit = _preambleBitNumber < 2 ? 0 : 1;
            if (++_preambleBitNumber >= 8) _preambleBitNumber = 0;
        } else {
            bit = RandomBits.Next() & 1;
        }
        return bit;
    }

    private static void CngDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        PrintCallback(tone, level, delay);
        if (tone == ModemConnectTone.FaxCng) _hits++;
    }

    private static void PreambleDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        _ = userData;
        _ = delay;
        Console.WriteLine($"{ModemConnectTones.ToneToString(tone)} ({(int)tone}) declared at bit {_preambleCount} ({level}dBm0)");
        if (tone == ModemConnectTone.FaxPreamble) _preambleOnAt = _preambleCount;
        else _preambleOffAt = _preambleCount;
    }

    private static void CedDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        PrintCallback(tone, level, delay);
        if (tone == ModemConnectTone.FaxPreamble || tone == ModemConnectTone.Ans) _hits++;
    }

    private static void AnsPrDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        PrintCallback(tone, level, delay);
        if (tone == ModemConnectTone.AnsWithPhaseReversals) _hits++;
    }

    private static void BellAnsDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        PrintCallback(tone, level, delay);
        if (tone == ModemConnectTone.BellAns) _hits++;
    }

    private static void CallingToneDetected(object? userData, ModemConnectTone tone, int level, int delay) {
        PrintCallback(tone, level, delay);
        if (tone == ModemConnectTone.CallingTone) _hits++;
    }

    private static void PrintCallback(ModemConnectTone tone, int level, int delay) =>
        Console.WriteLine($"{ModemConnectTones.ToneToString(tone)} ({(int)tone}) declared at {(float)_when / SampleRate:F6}s, delay {delay} ({level}dBm0)");

    private static int MillisecondsToSamples(int milliseconds) => milliseconds * SampleRate / 1000;

    private static void SetInternalProperty<T>(ModemConnectTonesTxState state, string propertyName, T value) {
        PropertyInfo? property = typeof(ModemConnectTonesTxState).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property is null || property.SetMethod is null)
            throw new MissingMemberException(typeof(ModemConnectTonesTxState).FullName, propertyName);
        property.SetValue(state, value);
    }

    private static void Exit(int code) => throw new ModemConnectTonesTestsExitException(code);

    public sealed class ModemConnectTonesTestsExitException : Exception {
        public ModemConnectTonesTestsExitException(int exitCode)
            : base($"modem_connect_tones_tests requested exit code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly BinaryReader _reader;
        private readonly int _channels;
        private long _remainingBytes;

        internal Pcm16WaveReader(string path, int sampleRate, int channels) {
            _reader = new BinaryReader(File.OpenRead(path));
            if (ReadFourCc(_reader) != "RIFF") throw new InvalidDataException("Missing RIFF header.");
            _reader.ReadInt32();
            if (ReadFourCc(_reader) != "WAVE") throw new InvalidDataException("Missing WAVE header.");
            ushort format = 0, foundChannels = 0, bits = 0;
            int foundRate = 0;
            while (_reader.BaseStream.Position + 8 <= _reader.BaseStream.Length) {
                string id = ReadFourCc(_reader);
                int size = _reader.ReadInt32();
                long next = _reader.BaseStream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = _reader.ReadUInt16();
                    foundChannels = _reader.ReadUInt16();
                    foundRate = _reader.ReadInt32();
                    _reader.ReadInt32();
                    _reader.ReadUInt16();
                    bits = _reader.ReadUInt16();
                } else if (id == "data") {
                    _remainingBytes = size;
                    break;
                }
                _reader.BaseStream.Position = next;
            }
            if (format != 1 || bits != 16 || foundRate != sampleRate || foundChannels != channels)
                throw new InvalidDataException("Unexpected WAV format.");
            _channels = channels;
        }

        internal int ReadFrames(short[] destination, int maximumFrames) {
            int frames = 0;
            while (frames < maximumFrames && _remainingBytes >= 2L * _channels) {
                destination[frames++] = _reader.ReadInt16();
                for (int channel = 1; channel < _channels; channel++) _reader.ReadInt16();
                _remainingBytes -= 2L * _channels;
            }
            return frames;
        }

        public void Dispose() => _reader.Dispose();
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly BinaryWriter _writer;
        private long _samples;
        private bool _disposed;

        internal Pcm16WaveWriter(string path, int sampleRate, int channels) {
            _writer = new BinaryWriter(File.Create(path));
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("RIFF"));
            _writer.Write(0);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            _writer.Write(16);
            _writer.Write((short)1);
            _writer.Write((short)channels);
            _writer.Write(sampleRate);
            _writer.Write(sampleRate * channels * 2);
            _writer.Write((short)(channels * 2));
            _writer.Write((short)16);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("data"));
            _writer.Write(0);
        }

        internal int WriteFrames(short[] mono, int frames) {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(frames, mono.Length);
            for (int i = 0; i < frames; i++) _writer.Write(mono[i]);
            _samples += frames;
            return frames;
        }

        public void Dispose() {
            if (_disposed) return;
            long dataBytes = _samples * 2;
            _writer.BaseStream.Position = 4;
            _writer.Write(checked((int)(36 + dataBytes)));
            _writer.BaseStream.Position = 40;
            _writer.Write(checked((int)dataBytes));
            _writer.Dispose();
            _disposed = true;
        }
    }

    private static string ReadFourCc(BinaryReader reader) => new(reader.ReadChars(4));
}
