/*
 * TKFaxEngine - managed C# port of the spanDSP test
 *
 * ima_adpcm_tests_adapter.c
 *
 * Direct port of ima_adpcm_tests.c. Original implementation written by
 * Steve Underwood <steveu@coppice.org>, Copyright (C) 2004 Steve Underwood.
 *
 * This test file preserves the GNU General Public License version 2 terms
 * of the original test source.
 */

#nullable enable

using libsndfile.NET8;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.ImaAdpcm;

namespace TKFaxEngineTest.Audio.Test;

public static class ImaAdpcmTestsAdapter
{
    private const string InFileName = "Test-Files/local/short_nb_voice.wav";
    private const string OutFileName = "post_ima_adpcm.wav";
    private const int HistLen = 2000;
    private const int SampleRate = 8000;

    public static int tkfx_ima_adpcm_tests_main(string[] testArguments)
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
        int variant = IMA_ADPCM_DVI4;
        string inFileName = InFileName;
        int chunkSize = 160;
        int encChunkSize = 0;
        bool logEncodedData = false;

        ParseArguments(
            args,
            ref variant,
            ref inFileName,
            ref chunkSize,
            ref encChunkSize,
            ref logEncodedData);

        SndFile inHandle = SfOpenTelephonyRead(inFileName, 1);
        SndFile outHandle = SfOpenTelephonyWrite(OutFileName, 1);

        ImaAdpcmState? imaEncState = ima_adpcm_init(null, variant, encChunkSize);
        if (imaEncState is null)
        {
            Console.Error.WriteLine("    Cannot create encoder");
            Exit(2);
        }

        ImaAdpcmState? imaDecState = ima_adpcm_init(null, variant, encChunkSize);
        if (imaDecState is null)
        {
            Console.Error.WriteLine("    Cannot create decoder");
            Exit(2);
        }

        short[] preAmp = new short[HistLen];
        short[] postAmp = new short[HistLen];
        byte[] imaData = new byte[HistLen];
        short[] history = new short[HistLen];

        int histIn = 0;
        int histOut = 0;
        double preEnergy = 0.0;
        double postEnergy = 0.0;
        double diffEnergy = 0.0;
        int totalPreSamples = 0;
        int totalCompressedBytes = 0;
        int totalPostSamples = 0;

        while (true)
        {
            int frames = checked((int)inHandle.ReadFrames(preAmp, chunkSize));
            if (frames == 0)
            {
                break;
            }

            totalPreSamples += frames;
            int imaBytes = ima_adpcm_encode(imaEncState, imaData, preAmp, frames);
            if (logEncodedData)
            {
                Console.OpenStandardOutput().Write(imaData, 0, imaBytes);
            }

            totalCompressedBytes += imaBytes;
            int decFrames = ima_adpcm_decode(imaDecState, postAmp, imaData, imaBytes);
            totalPostSamples += decFrames;

            for (int i = 0; i < frames; i++)
            {
                history[histIn++] = preAmp[i];
                if (histIn >= HistLen)
                {
                    histIn = 0;
                }

                preEnergy += (double)preAmp[i] * preAmp[i];
            }

            for (int i = 0; i < decFrames; i++)
            {
                postEnergy += (double)postAmp[i] * postAmp[i];
                int difference = postAmp[i] - history[histOut++];
                if (histOut >= HistLen)
                {
                    histOut = 0;
                }

                diffEnergy += (double)difference * difference;
            }

            outHandle.WriteFrames(postAmp, decFrames);
        }

        if (SfCloseTelephony(inHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{inFileName}'");
            Exit(2);
        }

        if (SfCloseTelephony(outHandle) != 0)
        {
            Console.Error.WriteLine($"    Cannot close audio file '{OutFileName}'");
            Exit(2);
        }

        ima_adpcm_free(imaEncState);
        ima_adpcm_free(imaDecState);

        Console.WriteLine($"Pre samples: {totalPreSamples}");
        Console.WriteLine($"Compressed bytes: {totalCompressedBytes}");
        Console.WriteLine($"Post samples: {totalPostSamples}");
        Console.WriteLine($"Output energy is {100.0 * postEnergy / preEnergy:F6}% of input energy.");
        Console.WriteLine($"Residual energy is {100.0 * diffEnergy / postEnergy:F6}% of the total.");

        if (Math.Abs(1.0 - postEnergy / preEnergy) > 0.05
            || Math.Abs(diffEnergy / postEnergy) > 0.03)
        {
            Console.WriteLine("Tests failed.");
            Exit(2);
        }

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void ParseArguments(
        string[] args,
        ref int variant,
        ref string inFileName,
        ref int chunkSize,
        ref int encChunkSize,
        ref bool logEncodedData)
    {
        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "-a")
            {
                variant = IMA_ADPCM_IMA4;
                chunkSize = 505;
            }
            else if (argument == "-l")
            {
                logEncodedData = true;
            }
            else if (argument == "-v")
            {
                variant = IMA_ADPCM_VDVI;
            }
            else if (argument == "-c" || argument.StartsWith("-c", StringComparison.Ordinal))
            {
                string value = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
                if (!int.TryParse(value, out encChunkSize))
                {
                    Exit(2);
                }
            }
            else if (argument == "-i" || argument.StartsWith("-i", StringComparison.Ordinal))
            {
                inFileName = argument.Length > 2
                    ? argument[2..]
                    : ReadOptionValue(args, ref index);
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
            Console.Error.WriteLine($"    Cannot open audio file '{name}' for reading");
            Exit(2);
        }

        if (handle.Format.SampleRate != SampleRate)
        {
            handle.Dispose();
            Console.WriteLine($"    Unexpected sample rate in audio file '{name}'");
            Exit(2);
        }

        if (handle.Format.Channels != channels)
        {
            handle.Dispose();
            Console.WriteLine($"    Unexpected number of channels in audio file '{name}'");
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
            Console.Error.WriteLine($"    Cannot open audio file '{name}' for writing");
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
