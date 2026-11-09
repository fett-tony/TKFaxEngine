/*
 * TKFaxEngine - managed C# port of the spanDSP test
 *
 * plc_tests_adapter.c
 *
 * Direct port of plc_tests.c. Original implementation written by
 * Steve Underwood <steveu@coppice.org>, Copyright (C) 2004 Steve Underwood.
 *
 * This test file preserves the GNU General Public License version 2 terms
 * of the original test source.
 */

#nullable enable

using libsndfile.NET8;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Dds;
using static TKFaxEngine.Audio.PlcApi;

namespace TKFaxEngineTest.Audio.Test;

public static class PlcTestsAdapter
{
    private const string InputFileName = "Test-Files/local/short_nb_voice.wav";
    private const string OutputFileName = "post_plc.wav";
    private const int SampleRate = 8000;
    private const int RandMax = 32767;

    private static uint randomState = 1;

    public static int tkfx_plc_tests_main(string[] testArguments)
    {
        ArgumentNullException.ThrowIfNull(testArguments);
        string[] args = testArguments;

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
        int lossRate = 25;
        int blockLength = 160;
        bool blockReal = false;
        bool blockSynthetic = false;
        int tone = -1;

        ParseArguments(
            args,
            ref blockLength,
            ref lossRate,
            ref blockReal,
            ref blockSynthetic,
            ref tone);

        if (blockLength <= 0)
        {
            Console.Error.WriteLine("    Block length must be positive");
            Exit(2);
        }

        SndFile? inputHandle = null;
        int phaseRate = 0;
        uint phaseAccumulator = 0;

        if (tone < 0)
        {
            inputHandle = SfOpenTelephonyRead(InputFileName, 1);
        }
        else
        {
            phaseRate = dds_phase_ratef(tone);
        }

        SndFile outputHandle = SfOpenTelephonyWrite(OutputFileName, 1);
        PlcState plc = plc_init(null)
            ?? throw new InvalidOperationException("Cannot initialize PLC state.");

        short[] amplitudes = new short[Math.Max(1024, blockLength)];
        int lostBlocks = 0;
        int blockNumber;

        for (blockNumber = 0; ; blockNumber++)
        {
            int inputFrames;
            if (tone < 0)
            {
                inputFrames = checked((int)inputHandle!.ReadFrames(amplitudes, blockLength));
                if (inputFrames != blockLength)
                {
                    break;
                }
            }
            else
            {
                if (blockNumber > 10_000)
                {
                    break;
                }

                for (int index = 0; index < blockLength; index++)
                {
                    amplitudes[index] = unchecked((short)dds_modf(
                        ref phaseAccumulator,
                        phaseRate,
                        10000.0f,
                        0));
                }

                inputFrames = blockLength;
            }

            int dropIt = NextRand() / (RandMax / 100);
            if (dropIt > lossRate)
            {
                _ = plc_rx(plc, amplitudes, inputFrames);
                if (blockReal)
                {
                    Array.Clear(amplitudes, 0, inputFrames);
                }
            }
            else
            {
                lostBlocks++;
                _ = plc_fillin(plc, amplitudes, inputFrames);
                if (blockSynthetic)
                {
                    Array.Clear(amplitudes, 0, inputFrames);
                }
            }

            int outputFrames = checked((int)outputHandle.WriteFrames(amplitudes, inputFrames));
            if (outputFrames != inputFrames)
            {
                Console.Error.WriteLine("    Error writing out sound");
                Exit(2);
            }
        }

        Console.WriteLine($"Dropped {lostBlocks} of {blockNumber} blocks");

        if (tone < 0 && inputHandle is not null && SfCloseTelephony(inputHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{InputFileName}'");
            Exit(2);
        }

        if (SfCloseTelephony(outputHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{OutputFileName}'");
            Exit(2);
        }

        _ = plc_free(plc);
        return 0;
    }

    private static void ParseArguments(
        string[] args,
        ref int blockLength,
        ref int lossRate,
        ref bool blockReal,
        ref bool blockSynthetic,
        ref int tone)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "-r")
            {
                blockReal = true;
            }
            else if (argument == "-s")
            {
                blockSynthetic = true;
            }
            else if (argument == "-b" || argument.StartsWith("-b", StringComparison.Ordinal))
            {
                string value = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
                if (!int.TryParse(value, out blockLength))
                {
                    Exit(2);
                }
            }
            else if (argument == "-l" || argument.StartsWith("-l", StringComparison.Ordinal))
            {
                string value = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
                if (!int.TryParse(value, out lossRate))
                {
                    Exit(2);
                }
            }
            else if (argument == "-t" || argument.StartsWith("-t", StringComparison.Ordinal))
            {
                string value = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
                if (!int.TryParse(value, out tone))
                {
                    Exit(2);
                }
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

    private static int NextRand()
    {
        randomState = unchecked(randomState * 1103515245U + 12345U);
        return (int)((randomState >> 16) & RandMax);
    }

    private static SndFile SfOpenTelephonyRead(string name, int channels)
    {
        SndFile? handle = SndFile.OpenRead(name);
        if (handle is null)
        {
            Console.Error.WriteLine($"    Failed to open audio file '{name}'");
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
            Console.Error.WriteLine($"    Failed to open audio file '{name}'");
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
