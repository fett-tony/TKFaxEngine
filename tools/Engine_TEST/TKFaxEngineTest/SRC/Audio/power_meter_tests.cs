/*
 * TKFaxEngine - managed C# port of the spanDSP test
 *
 * power_meter_tests.c
 *
 * Direct port of power_meter_tests.c. Original implementation written by
 * Steve Underwood <steveu@coppice.org>, Copyright (C) 2003 Steve Underwood.
 *
 * This test file preserves the GNU General Public License version 2 terms
 * of the original test source.
 */

#nullable enable

using libsndfile.NET8;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Dds;
using static TKFaxEngine.Audio.PowerMeterApi;
using static TKFaxEngine.Audio.tone_generate;

namespace TKFaxEngineTest.Audio.Test;

public static class PowerMeterTests
{
    private const string InputFileName = "Test-Files/local/short_nb_voice.wav";
    private const string OutputFileName = "power_meter_tests.wav";
    private const int SampleRate = 8000;

    public static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (TestExitException ex)
        {
            return ex.ExitCode;
        }
    }

    public static int Run(string[] args)
    {
        bool basicTests = true;
        bool decode = false;
        string inputFile = InputFileName;

        ParseArguments(args, ref basicTests, ref decode, ref inputFile);

        if (basicTests)
        {
            _ = RunPowerMeterTests();
            _ = RunPowerSurgeDetectorTests();
        }

        if (decode)
        {
            _ = RunPowerSurgeDetectorFileTest(inputFile);
        }

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static int RunPowerSurgeDetectorTests()
    {
        SndFile outputHandle = SfOpenTelephonyWrite(OutputFileName, 2);
        PowerSurgeDetectorState detector = power_surge_detector_init(null, -50.0f, 5.0f)
            ?? throw new InvalidOperationException("Cannot initialize power-surge detector.");

        bool previousSignalPresent = false;
        int phaseRate = dds_phase_rate(450.0f);
        uint phaseAccumulator = 0;
        short phaseScale = dds_scaling_dbm0(-33.0f);
        Awgn noise = new(1234567, -45.0f);

        int[] extremes = { 8001, -1, 8001, -1 };
        short[] amplitudes = new short[8000];
        short[] output = new short[2 * 8000];

        for (int sample = 0; sample < 800_000; sample += 8000)
        {
            int ok = 0;
            for (int index = 0; index < 8000; index++)
            {
                int value = noise.NextSample();
                if (index < 4000)
                {
                    value += dds_mod(ref phaseAccumulator, phaseRate, phaseScale, 0);
                }

                amplitudes[index] = unchecked((short)value);
                int signalLevel = power_surge_detector(detector, amplitudes[index]);
                bool signalPresent = signalLevel != 0;

                if (previousSignalPresent != signalPresent)
                {
                    float signalPower = power_surge_detector_current_dbm0(detector);
                    if (signalPresent)
                    {
                        if (ok == 0 && index >= 0 && index < 25)
                        {
                            ok = 1;
                        }

                        if (extremes[0] > index)
                        {
                            extremes[0] = index;
                        }

                        if (extremes[1] < index)
                        {
                            extremes[1] = index;
                        }

                        Console.WriteLine(
                            $"On at {(sample + index) / 8000.0:F6} ({signalPower:F6}dBm0)");
                    }
                    else
                    {
                        if (ok == 1 && index >= 4000 && index < 4035)
                        {
                            ok = 2;
                        }

                        if (extremes[2] > index)
                        {
                            extremes[2] = index;
                        }

                        if (extremes[3] < index)
                        {
                            extremes[3] = index;
                        }

                        Console.WriteLine(
                            $"Off at {(sample + index) / 8000.0:F6} ({signalPower:F6}dBm0)");
                    }

                    previousSignalPresent = signalPresent;
                }

                output[2 * index] = amplitudes[index];
                output[2 * index + 1] = signalPresent ? (short)5000 : (short)0;
            }

            _ = outputHandle.WriteFrames(output, 8000);

            if (ok != 2
                || extremes[0] < 1
                || extremes[1] > 30
                || extremes[2] < 4001
                || extremes[3] > 4030)
            {
                Console.WriteLine($"    Surge not detected correctly ({ok})");
                Exit(2);
            }
        }

        if (SfCloseTelephony(outputHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{OutputFileName}'");
            Exit(2);
        }

        Console.WriteLine(
            $"Min on {extremes[0]}, max on {extremes[1]}, " +
            $"min off {extremes[2]}, max off {extremes[3]}");

        _ = power_surge_detector_free(detector);
        return 0;
    }

    private static int RunPowerSurgeDetectorFileTest(string file)
    {
        SndFile inputHandle = SfOpenTelephonyRead(file, 1);
        SndFile outputHandle = SfOpenTelephonyWrite(OutputFileName, 1);
        PowerSurgeDetectorState detector = power_surge_detector_init(null, -50.0f, 6.0f)
            ?? throw new InvalidOperationException("Cannot initialize power-surge detector.");

        bool previousSignalPresent = false;
        int sample = 0;
        short[] amplitudes = new short[8000];
        short[] output = new short[2 * 8000];

        int inputFrames;
        while ((inputFrames = checked((int)inputHandle.ReadFrames(amplitudes, 8000))) != 0)
        {
            for (int index = 0; index < inputFrames; index++)
            {
                int signalLevel = power_surge_detector(detector, amplitudes[index]);
                bool signalPresent = signalLevel != 0;

                if (previousSignalPresent != signalPresent)
                {
                    float signalPower = power_surge_detector_current_dbm0(detector);
                    Console.WriteLine(
                        $"{(signalPresent ? "On" : "Off")} at " +
                        $"{(sample + index) / 8000.0:F6} ({signalPower:F6}dBm0)");
                    previousSignalPresent = signalPresent;
                }

                output[2 * index] = amplitudes[index];
                output[2 * index + 1] = signalPresent ? (short)5000 : (short)0;
            }

            _ = outputHandle.WriteFrames(output, inputFrames);
            sample += inputFrames;
        }

        if (SfCloseTelephony(inputHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{file}'");
            Exit(2);
        }

        if (SfCloseTelephony(outputHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{OutputFileName}'");
            Exit(2);
        }

        _ = power_surge_detector_free(detector);
        return 0;
    }

    private static int RunPowerMeterTests()
    {
        PowerMeterState meter = power_meter_init(null, 7)
            ?? throw new InvalidOperationException("Cannot initialize power meter.");
        short[] amplitudes = new short[1000];

        Console.WriteLine("Testing with zero in the power register.");
        Console.WriteLine(
            $"Power: expected {-90.169f:F6}dBm0/{-96.329f:F6}dBOv, got " +
            $"{power_meter_current_dbm0(meter):F6}dBm0/" +
            $"{power_meter_current_dbov(meter):F6}dBOv");

        if (MathF.Abs(-90.169f - power_meter_current_dbm0(meter)) > 0.05f
            || MathF.Abs(-96.329f - power_meter_current_dbov(meter)) > 0.05f)
        {
            Console.WriteLine("Test failed (level)");
            Exit(2);
        }

        Console.WriteLine("Testing with a square wave 10dB from full scale.");
        int level = 0;
        for (int index = 0; index < 1000; index++)
        {
            amplitudes[index] = (index & 1) != 0 ? (short)10362 : (short)-10362;
            level = power_meter_update(meter, amplitudes[index]);
        }

        PrintExpectedAndMeasured(level, meter);

        if (level < power_meter_level_dbov(-10.0f) * 0.99f
            || level > power_meter_level_dbov(-10.0f) * 1.01f)
        {
            Console.WriteLine(
                $"Test failed (level) - {level} {power_meter_level_dbov(-10.0f)}");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbm0(meter)
            - (-10.0f + PowerMeter.Dbm0MaximumPower)) > 0.1f)
        {
            Console.WriteLine("Test failed (dBm0)");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbov(meter)
            - (-10.0f + PowerMeter.DbovMaximumPower)) > 0.1f)
        {
            Console.WriteLine("Test failed (dBOv)");
            Exit(2);
        }

        Console.WriteLine("Testing with a sine wave tone 10dB from maximum");
        tone_gen_descriptor_t descriptor = tone_gen_descriptor_init(
            null,
            1000,
            -4,
            0,
            1,
            1,
            0,
            0,
            0,
            1) ?? throw new InvalidOperationException("Cannot initialize tone descriptor.");
        tone_gen_state_t generator = tone_gen_init(null, descriptor)
            ?? throw new InvalidOperationException("Cannot initialize tone generator.");

        int length = tone_gen(generator, amplitudes, 1000);
        for (int index = 0; index < length; index++)
        {
            level = power_meter_update(meter, amplitudes[index]);
        }

        PrintExpectedAndMeasured(level, meter);

        if (level < power_meter_level_dbov(-10.0f) * 0.95f
            || level > power_meter_level_dbov(-10.0f) * 1.05f)
        {
            Console.WriteLine(
                $"Test failed (level) - {level} {power_meter_level_dbov(-10.0f)}");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbm0(meter)
            - (-10.0f + PowerMeter.Dbm0MaximumPower)) > 0.2f)
        {
            Console.WriteLine("Test failed (dBm0)");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbov(meter)
            - (-10.0f + PowerMeter.DbovMaximumPower)) > 0.2f)
        {
            Console.WriteLine("Test failed (dBOv)");
            Exit(2);
        }

        Console.WriteLine("Testing with AWGN 10dB from maximum");
        Awgn noise = Awgn.FromDbov(1234567, -10.0f);
        noise.Fill(amplitudes);

        _ = power_meter_rx(meter, amplitudes, 1000);
        level = power_meter_current(meter);
        PrintExpectedAndMeasured(level, meter);

        if (level < power_meter_level_dbov(-10.0f) * 0.95f
            || level > power_meter_level_dbov(-10.0f) * 1.05f)
        {
            Console.WriteLine(
                $"Test failed (level) - {level} {power_meter_level_dbov(-10.0f)}");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbm0(meter)
            - (-10.0f + PowerMeter.Dbm0MaximumPower)) > 0.2f)
        {
            Console.WriteLine("Test failed (dBm0)");
            Exit(2);
        }

        if (MathF.Abs(power_meter_current_dbov(meter)
            - (-10.0f + PowerMeter.DbovMaximumPower)) > 0.2f)
        {
            Console.WriteLine("Test failed (dBOv)");
            Exit(2);
        }

        _ = tone_gen_free(generator);
        tone_gen_descriptor_free(descriptor);
        _ = power_meter_free(meter);
        return 0;
    }

    private static void PrintExpectedAndMeasured(int level, PowerMeterState meter)
    {
        int expectedDbov = power_meter_level_dbov(
            -10.0f + PowerMeter.DbovMaximumPower);
        int expectedDbm0 = power_meter_level_dbm0(
            -10.0f + PowerMeter.Dbm0MaximumPower);

        Console.WriteLine(
            $"Level: expected {expectedDbov}/{expectedDbm0}, got {level}");
        Console.WriteLine(
            $"Power: expected " +
            $"{-10.0f + PowerMeter.Dbm0MaximumPower:F6}dBm0/" +
            $"{-10.0f + PowerMeter.DbovMaximumPower:F6}dBOv, got " +
            $"{power_meter_current_dbm0(meter):F6}dBm0/" +
            $"{power_meter_current_dbov(meter):F6}dBOv");
    }

    private static void ParseArguments(
        string[] args,
        ref bool basicTests,
        ref bool decode,
        ref string inputFile)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "-d" || argument.StartsWith("-d", StringComparison.Ordinal))
            {
                inputFile = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
                basicTests = false;
                decode = true;
            }
            else
            {
                Exit(2);
            }
        }
    }

    private static string ReadOptionValue(string[] args, ref int index)
    {
        index++;
        if (index >= args.Length)
        {
            Exit(2);
        }

        return args[index];
    }

    private static SndFile SfOpenTelephonyRead(string name, int channels)
    {
        SndFile? handle = SndFile.OpenRead(name);
        if (handle is null)
        {
            Console.Error.WriteLine($"    Cannot open audio file '{name}'");
            Exit(2);
        }

        if (handle.Format.SampleRate != SampleRate || handle.Format.Channels != channels)
        {
            handle.Dispose();
            Console.Error.WriteLine($"    Audio file '{name}' is not {SampleRate} Hz/{channels} channel(s)");
            Exit(2);
        }

        return handle;
    }

    private static SndFile SfOpenTelephonyWrite(string name, int channels)
    {
        SfFormat format = new(
            SfFormatMajor.WAV,
            SfFormatSubtype.PCM_16,
            SfFormatEndian.FILE,
            channels,
            SampleRate);
        SndFile? handle = SndFile.OpenWrite(name, format);
        if (handle is null)
        {
            Console.Error.WriteLine($"    Cannot create audio file '{name}'");
            Exit(2);
        }

        return handle;
    }

    private static int SfCloseTelephony(SndFile handle)
    {
        try
        {
            handle.Dispose();
            return 0;
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }

    private static void Exit(int code) => throw new TestExitException(code);

    private sealed class TestExitException : Exception
    {
        public TestExitException(int exitCode)
        {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
