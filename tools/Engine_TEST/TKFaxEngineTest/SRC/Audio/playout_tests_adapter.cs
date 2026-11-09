/*
 * TKFaxEngine - managed C# port of the spanDSP test
 *
 * playout_tests_adapter.c
 *
 * Direct port of playout_tests.c. Original implementation written by
 * Steve Underwood <steveu@coppice.org>, Copyright (C) 2005 Steve Underwood.
 *
 * This test file preserves the GNU General Public License version 2 terms
 * of the original test source.
 */

#nullable enable

using libsndfile.NET8;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Playout;
using static TKFaxEngine.Audio.PlcApi;
using static TKFaxEngine.Audio.TimeScaleApi;

namespace TKFaxEngineTest.Audio.Test;

public static class PlayoutTestsAdapter
{
    private const string InputFileName = "Test-Files/local/short_nb_voice.wav";
    private const string OutputFileName = "playout_out.wav";
    private const int BlockLength = 160;
    private const int SampleRate = 8000;
    private const int RandMax = 32767;

    private static uint randomState = 1;

    public static int tkfx_playout_tests_main(string[] testArguments)
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
        _ = args;

        Console.WriteLine("Dynamic buffering tests");
        DynamicBufferTests();

        Console.WriteLine("Static buffering tests");
        StaticBufferTests();

        return 0;
    }

    private static void DynamicBufferTests()
    {
        SndFile inputHandle = SfOpenTelephonyRead(InputFileName, 1);
        SndFile outputHandle = SfOpenTelephonyWrite(OutputFileName, 1);

        int nearFarTimeOffset = 54321;
        int timeStamp = 12345;
        int nextActualReceive = unchecked(timeStamp + nearFarTimeOffset);
        int nextScheduledReceive = 0;

        short[] fill = new short[BlockLength];
        Array.Fill(fill, short.MaxValue);

        PlayoutState state = playout_init(2 * BlockLength, 15 * BlockLength);
        PlcState plc = plc_init(null)
            ?? throw new InvalidOperationException("Cannot initialize PLC state.");
        TimeScaleState timeScale = time_scale_init(null, SampleRate, 1.0f)
            ?? throw new InvalidOperationException("Cannot initialize time-scale state.");

        PlayoutFrame frame = new();
        short[] buffer = new short[20 * BlockLength];
        short[] scaled = new short[10 * BlockLength];

        for (int clock = 0; clock < 1_000_000; clock++)
        {
            if (clock >= nextActualReceive)
            {
                short[] amplitudes = new short[BlockLength];
                int inputFrames = checked((int)inputHandle.ReadFrames(amplitudes, BlockLength));
                if (inputFrames < BlockLength)
                {
                    break;
                }

                _ = playout_put(
                    state,
                    amplitudes,
                    PLAYOUT_TYPE_SPEECH,
                    inputFrames,
                    timeStamp,
                    nextActualReceive);

                int jitter = NextRand() & 0xFF;
                if (clock < 100_000)
                {
                    jitter = (jitter * jitter) >> 7;
                }
                else if (clock < 200_000)
                {
                    jitter = (jitter * jitter) >> 6;
                }
                else if (clock < 300_000)
                {
                    jitter = (jitter * jitter) >> 5;
                }
                else if (clock < 400_000)
                {
                    jitter = (jitter * jitter) >> 7;
                }

                timeStamp = unchecked(timeStamp + BlockLength);
                nextActualReceive = unchecked(timeStamp + nearFarTimeOffset + jitter);
            }

            if (clock < nextScheduledReceive)
            {
                continue;
            }

            int result;
            do
            {
                result = playout_get(state, frame, nextScheduledReceive);
                if (result == PLAYOUT_DROP)
                {
                    Console.WriteLine($">> Drop {nextScheduledReceive}");
                }
            }
            while (result == PLAYOUT_DROP);

            switch (result)
            {
            case PLAYOUT_OK:
            {
                Console.WriteLine($">> Play {nextScheduledReceive}");
                short[] frameData = RequireShortFrame(frame);
                _ = plc_rx(plc, frameData, frame.SenderLength);
                int length = time_scale(timeScale, scaled, frameData, frame.SenderLength);
                if (length < 0)
                {
                    Console.Error.WriteLine("    Error time-scaling sound");
                    Exit(2);
                }

                Console.WriteLine($"len = {length}");
                FillDiagnosticOutput(buffer, scaled, length, state);
                WriteFrames(outputHandle, buffer, length);

                frame.Data = null;
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            }

            case PLAYOUT_FILLIN:
            {
                Console.WriteLine($">> Fill {nextScheduledReceive}");
                _ = plc_fillin(plc, fill, BlockLength);
                _ = time_scale_rate(timeScale, 0.5f);
                int length = time_scale(timeScale, scaled, fill, BlockLength);
                _ = time_scale_rate(timeScale, 1.0f);
                if (length < 0)
                {
                    Console.Error.WriteLine("    Error time-scaling fill-in sound");
                    Exit(2);
                }

                Console.WriteLine($"len = {length}");
                FillDiagnosticOutput(buffer, scaled, length, state);
                WriteFrames(outputHandle, buffer, length);

                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            }

            case PLAYOUT_DROP:
                Console.WriteLine($">> Drop {nextScheduledReceive}");
                break;

            case PLAYOUT_NOFRAME:
                Console.WriteLine(
                    $">> No frame {nextScheduledReceive} {playout_next_due(state)} " +
                    $"{state.LastSpeechSenderStamp} {state.LastSpeechSenderLength}");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;

            case PLAYOUT_EMPTY:
                Console.WriteLine($">> Empty {nextScheduledReceive}");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;

            case PLAYOUT_ERROR:
                Console.WriteLine($">> Error {nextScheduledReceive}");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;

            default:
                Console.WriteLine($">> Eh? {nextScheduledReceive}");
                break;
            }
        }

        CloseInput(inputHandle, InputFileName);
        CloseOutput(outputHandle, OutputFileName);

        Console.WriteLine(
            $"{state.StateJustInTime,10} {state.StateLate,10} " +
            $"{playout_current_length(state),10}");

        PlayoutFrame? pending;
        while ((pending = playout_get_unconditional(state)) is not null)
        {
            pending.Data = null;
        }

        _ = playout_free(state);
        _ = plc_free(plc);
        _ = time_scale_free(timeScale);
    }

    private static void StaticBufferTests()
    {
        int nextScheduledSend = 0;
        int transitTime = 320;
        int nextActualReceive = unchecked(nextScheduledSend + transitTime);
        int nextScheduledReceive = 960;

        byte[] frameBytes = new byte[BlockLength];
        int type = PLAYOUT_TYPE_SPEECH;
        int length = BlockLength;

        PlayoutState state = playout_init(2 * BlockLength, 2 * BlockLength);
        PlayoutFrame frame = new();

        for (int clock = 0; clock < 1_000_000; clock++)
        {
            if (clock >= nextActualReceive)
            {
                int result = playout_put(
                    state,
                    frameBytes,
                    type,
                    length,
                    nextScheduledSend,
                    nextActualReceive);

                switch (result)
                {
                case PLAYOUT_OK:
                    Console.WriteLine("<< Record");
                    break;
                case PLAYOUT_ERROR:
                    Console.WriteLine("<< Error");
                    break;
                default:
                    Console.WriteLine("<< Eh?");
                    break;
                }

                nextScheduledSend = unchecked(nextScheduledSend + BlockLength);
                int jitter = NextRand() & 0xFF;
                jitter = (jitter * jitter) >> 7;
                transitTime = 320 + jitter;
                nextActualReceive = unchecked(nextScheduledSend + transitTime);
            }

            if (clock < nextScheduledReceive)
            {
                continue;
            }

            int resultCode;
            do
            {
                resultCode = playout_get(state, frame, nextScheduledReceive);
            }
            while (resultCode == PLAYOUT_DROP);

            switch (resultCode)
            {
            case PLAYOUT_OK:
                Console.WriteLine(">> Play");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            case PLAYOUT_FILLIN:
                Console.WriteLine(">> Fill");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            case PLAYOUT_DROP:
                Console.WriteLine(">> Drop");
                break;
            case PLAYOUT_NOFRAME:
                Console.WriteLine(">> No frame");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            case PLAYOUT_EMPTY:
                Console.WriteLine(">> Empty");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            case PLAYOUT_ERROR:
                Console.WriteLine(">> Error");
                nextScheduledReceive = unchecked(nextScheduledReceive + BlockLength);
                break;
            default:
                Console.WriteLine(">> Eh?");
                break;
            }
        }

        PlayoutFrame? pending;
        while ((pending = playout_get_unconditional(state)) is not null)
        {
            pending.Data = null;
        }

        _ = playout_free(state);
    }

    private static short[] RequireShortFrame(PlayoutFrame frame)
    {
        if (frame.Data is short[] samples && frame.SenderLength <= samples.Length)
        {
            return samples;
        }

        Console.Error.WriteLine("    Playout frame does not contain the expected PCM samples");
        Exit(2);
        return Array.Empty<short>();
    }

    private static void FillDiagnosticOutput(
        short[] destination,
        short[] source,
        int length,
        PlayoutState state)
    {
        if (length < 0 || length > source.Length || 2 * length > destination.Length)
        {
            Console.Error.WriteLine("    Time-scale output exceeded the test buffers");
            Exit(2);
        }

        short bufferLength = SaturateToInt16(10 * playout_current_length(state));
        for (int index = 0; index < length; index++)
        {
            destination[2 * index] = source[index];
            destination[2 * index + 1] = bufferLength;
        }
    }

    private static short SaturateToInt16(int value)
    {
        if (value > short.MaxValue)
        {
            return short.MaxValue;
        }

        if (value < short.MinValue)
        {
            return short.MinValue;
        }

        return (short)value;
    }

private static void WriteFrames(SndFile handle, short[] samples, int frames)
{
    if (frames == 0)
    {
        return;
    }

    if (frames < 0)
    {
        Console.Error.WriteLine("    Invalid frame count");
        Exit(2);
    }

    int written = checked((int)handle.WriteFrames(samples, frames));
    if (written != frames)
    {
        Console.Error.WriteLine("    Error writing out sound");
        Exit(2);
    }
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
            Console.Error.WriteLine($"    Failed to create audio file '{name}'");
            Exit(2);
        }

        return handle;
    }

    private static void CloseInput(SndFile handle, string name)
    {
        try
        {
            handle.Dispose();
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{name}'");
            Exit(2);
        }
    }

    private static void CloseOutput(SndFile handle, string name)
    {
        try
        {
            handle.Dispose();
        }
        catch (InvalidOperationException)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{name}'");
            Exit(2);
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
