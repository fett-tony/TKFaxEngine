/*
 * TKFaxEngine - managed C# port of sig_tone_tests.c.
 *
 * The full spanDSP test flow is retained: notch response sweep, Bellcore/Mitel
 * talk-off immunity, signal/noise ratio sweep, A-law/u-law codec munging and
 * the original transmit/receive signalling sequence.
 */

#nullable enable

using TKFaxEngineTest.System;

using System.Diagnostics;
using System.Globalization;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.SigTone;
using static TKFaxEngine.Audio.Telephony;

namespace TKFaxEngineTest.Audio.Test;

public static class sig_tone_tests {
    private const string OutFileName = "sig_tone.wav";
    private const int SamplesPerChunk = 160;

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

    private readonly record struct TemplateEntry(double Frequency, double MinimumLevel, double MaximumLevel);

    private static readonly (int Milliseconds, int Tone)[] PatternOneTone =
    {
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (33, SIG_TONE_1_PRESENT), (67, 0),
        (600, SIG_TONE_1_PRESENT),
        (0, 0)
    };

    private static readonly (int Milliseconds, int Tone)[] PatternTwoTones =
    {
        (100, SIG_TONE_1_PRESENT),
        (100, SIG_TONE_1_PRESENT | SIG_TONE_2_PRESENT),
        (100, SIG_TONE_2_PRESENT),
        (0, 0)
    };

    private static int _numberOfTones = 1;
    private static int _sampleNumber;
    private static int _tone1Present;
    private static int _tone2Present;
    private static int _txSection;
    private static int _rxHandlerCallbacks;
    private static int _txHandlerCallbacks;
    private static bool _useGui;

    public sealed class SigToneTestsExitException : Exception {
        public SigToneTestsExitException(int exitCode) : base($"sig_tone_tests exited with code {exitCode}.") => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    public static int Main(string[] args) {
        try {
            return tkfx_sig_tone_tests_body_main(args);
        } catch (SigToneTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_sig_tone_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        _useGui = false;
        for (int i = 0; i < testArguments.Length; i++) {
            string argument = testArguments[i];
            if (argument == "-g")
                _useGui = true;
            else
                Exit(2);
        }

        for (int type = 1; type <= 3; type++) {
            _sampleNumber = 0;
            _tone1Present = 0;
            _tone2Present = 0;
            _txHandlerCallbacks = 0;
            _rxHandlerCallbacks = 0;

            SimCodecMungeState munge;
            double[] frequencies = new double[2];
            TemplateEntry[] template;
            SigToneType toneType;

            switch (type) {
                case 1:
                    Console.WriteLine("2280Hz tests.");
                    munge = new SimCodecMungeState(CodecMungeMode.ALaw);
                    toneType = SigToneType.Tone2280Hz;
                    _numberOfTones = 1;
                    frequencies[0] = 2280.0;
                    template = new[]
                    {
                        new TemplateEntry(1150.0, -0.2, 0.0),
                        new TemplateEntry(1880.0, -0.5, 0.0),
                        new TemplateEntry(2080.0, -5.0, 0.0),
                        new TemplateEntry(2260.0, -99.0, 0.0),
                        new TemplateEntry(2300.0, -99.0, -30.0),
                        new TemplateEntry(2480.0, -99.0, 0.0),
                        new TemplateEntry(2680.0, -5.0, 0.0),
                        new TemplateEntry(4000.0, -0.5, 0.0)
                    };
                    break;

                case 2:
                    Console.WriteLine("2600Hz tests.");
                    munge = new SimCodecMungeState(CodecMungeMode.MuLaw);
                    toneType = SigToneType.Tone2600Hz;
                    _numberOfTones = 1;
                    frequencies[0] = 2600.0;
                    template = new[]
                    {
                        new TemplateEntry(2400.0, -1.0, 0.0),
                        new TemplateEntry(2580.0, -99.0, 0.0),
                        new TemplateEntry(2620.0, -99.0, -30.0),
                        new TemplateEntry(2800.0, -99.0, 0.0),
                        new TemplateEntry(4000.0, -1.0, 0.0)
                    };
                    break;

                default:
                    Console.WriteLine("2400Hz/2600Hz tests.");
                    munge = new SimCodecMungeState(CodecMungeMode.MuLaw);
                    toneType = SigToneType.Tone2400Hz2600Hz;
                    _numberOfTones = 2;
                    frequencies[0] = 2400.0;
                    frequencies[1] = 2600.0;
                    template = new[]
                    {
                        new TemplateEntry(2200.0, -1.0, 0.0),
                        new TemplateEntry(2380.0, -99.0, 0.0),
                        new TemplateEntry(2420.0, -99.0, -30.0),
                        new TemplateEntry(2580.0, -99.0, 0.0),
                        new TemplateEntry(2620.0, -99.0, -30.0),
                        new TemplateEntry(2800.0, -99.0, 0.0),
                        new TemplateEntry(4000.0, -1.0, 0.0)
                    };
                    break;
            }

            SigToneTxState txState = null!;
            txState = new SigToneTxState(toneType, (_, what, level, duration) => TxHandler(txState, what, level, duration));
            using (txState)
            using (var rxState = new SigToneRxState(toneType, RxHandler))
            using (munge) {
                MapFrequencyResponse(rxState, template);
                SpeechImmunityTests(rxState);
                LevelAndRatioTests(rxState, frequencies);
                SequenceTests(txState, rxState, munge);
            }
        }

        Console.WriteLine("Tests completed.");
        return 0;
    }

    private static void PlotFrequencyResponse() {
        try {
            using var process = new Process {
                StartInfo = new ProcessStartInfo {
                    FileName = "gnuplot",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = false
                }
            };
            if (!process.Start())
                Exit(2);

            using StreamWriter input = process.StandardInput;
            input.WriteLine("set autoscale");
            input.WriteLine("unset log");
            input.WriteLine("unset label");
            input.WriteLine("set xtic auto");
            input.WriteLine("set ytic auto");
            input.WriteLine("set title 'Notch filter frequency response'");
            input.WriteLine("set xlabel 'Frequency (Hz)'");
            input.WriteLine("set ylabel 'Gain (dB)'");
            input.WriteLine("plot 'sig_tone_notch' using 1:3 title 'min' with lines,'sig_tone_notch' using 1:6 title 'actual' with lines,'sig_tone_notch' using 1:9 title 'max' with lines");
            input.Flush();
            Console.ReadLine();
            input.Close();
            process.WaitForExit();
            if (process.ExitCode != 0)
                Exit(2);
        } catch (Exception ex) when (ex is global::System.ComponentModel.Win32Exception or InvalidOperationException or IOException) {
            Console.Error.WriteLine($"Unable to run gnuplot: {ex.Message}");
            Exit(2);
        }
    }

    private static void TxHandler(SigToneTxState state, int what, int level, int duration) {
        _ = level;
        _ = duration;
        _txHandlerCallbacks++;
        if ((what & SIG_TONE_TX_UPDATE_REQUEST) == 0)
            return;

        Console.WriteLine("Tx: update request");
        (int milliseconds, int tone) = _numberOfTones == 1
            ? PatternOneTone[_txSection]
            : PatternTwoTones[_txSection];

        if (milliseconds != 0) {
            Console.WriteLine(
                "Tx: [{0:X4}] {1} {2} for {3} samples ({4}ms)",
                tone,
                (tone & SIG_TONE_1_PRESENT) != 0 ? "on " : "off",
                (tone & SIG_TONE_2_PRESENT) != 0 ? "on " : "off",
                milliseconds_to_samples(milliseconds),
                milliseconds);
            state.SetMode(tone, milliseconds_to_samples(milliseconds));
            _txSection++;
        } else {
            Console.WriteLine("End of sequence");
        }
    }

    private static void RxHandler(object? userData, int what, int level, int duration) {
        _ = userData;
        _ = level;
        _rxHandlerCallbacks++;
        float milliseconds = 1000.0f * duration / SAMPLE_RATE;
        Console.Write("Rx: [{0:X4}]", what);

        int present = what & SIG_TONE_1_PRESENT;
        if ((what & SIG_TONE_1_CHANGE) != 0) {
            Console.Write(present != 0 ? " on " : " off");
            if (present == _tone1Present)
                Exit(2);
            _tone1Present = present;
        } else {
            Console.Write(" ---");
            if (present != _tone1Present)
                Exit(2);
        }

        present = what & SIG_TONE_2_PRESENT;
        if ((what & SIG_TONE_2_CHANGE) != 0) {
            Console.Write(present != 0 ? " on " : " off");
            if (present == _tone2Present)
                Exit(2);
            _tone2Present = present;
        } else {
            if (present != _tone2Present)
                Exit(2);
            Console.Write(" ---");
        }

        Console.WriteLine(" after {0} samples ({1:F3}ms)", duration, milliseconds);
    }

    private static void MapFrequencyResponse(SigToneRxState state, TemplateEntry[] template) {
        Console.WriteLine("Frequency response test");
        state.SetMode(SIG_TONE_RX_PASSTHROUGH | SIG_TONE_RX_FILTER_TONE, 0);
        using SweptToneState swept = SweptTone.Initialize(null, 200.0f, 3900.0f, -10.0f, 120 * SAMPLE_RATE, false);
        using var file = new StreamWriter(File.Open("sig_tone_notch", FileMode.Create, FileAccess.Write, FileShare.Read));
        short[] buffer = new short[SamplesPerChunk];
        int templateEntry = 0;

        for (; ; )
        {
            int length = swept.Generate(buffer.AsSpan());
            if (length <= 0)
                break;

            double inputPower = 0.0;
            for (int i = 0; i < length; i++)
                inputPower += (double)buffer[i] * buffer[i];

            state.Process(buffer.AsSpan(0, length));
            double outputPower = 0.0;
            for (int i = 0; i < length; i++)
                outputPower += (double)buffer[i] * buffer[i];

            double frequency = swept.CurrentFrequency;
            double gain = inputPower != 0.0 ? 10.0 * Math.Log10(outputPower / inputPower + 1.0e-10) : 0.0;
            TemplateEntry limit = template[templateEntry];
            string line = string.Format(
                CultureInfo.InvariantCulture,
                "{0,7:F1} Hz {1:F3} dBm0 < {2:F3} dBm0 < {3:F3} dBm0",
                frequency,
                limit.MinimumLevel,
                gain,
                limit.MaximumLevel);
            Console.WriteLine(line);
            file.WriteLine(line);

            if (gain < limit.MinimumLevel || gain > limit.MaximumLevel) {
                Console.WriteLine("Expected: {0:F3} dBm0 to  {1:F3} dBm0", limit.MinimumLevel, limit.MaximumLevel);
                Console.WriteLine("    Failed");
                Exit(2);
            }

            if (frequency > limit.Frequency && templateEntry + 1 < template.Length)
                templateEntry++;
        }

        file.Flush();
        if (_useGui)
            PlotFrequencyResponse();
        Console.WriteLine("    Passed");
    }

    private static void SpeechImmunityTests(SigToneRxState state) {
        Console.WriteLine("Speech immunity test");
        int totalHits = 0;
        short[] amplitudes = new short[SAMPLE_RATE];

        for (int fileIndex = 0; fileIndex < BellcoreFiles.Length; fileIndex++) {
            Array.Clear(amplitudes);
            state.Process(amplitudes.AsSpan());
            _rxHandlerCallbacks = 0;

            try {
                using var reader = new Pcm16WaveReader(BellcoreFiles[fileIndex], SAMPLE_RATE, 1);
                int frames;
                while ((frames = reader.ReadFrames(amplitudes, SAMPLE_RATE)) != 0)
                    state.Process(amplitudes.AsSpan(0, frames));
            } catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                Console.WriteLine("    Cannot process speech file '{0}': {1}", BellcoreFiles[fileIndex], ex.Message);
                Exit(2);
            }

            Console.WriteLine("    File {0} gave {1} false hits.", fileIndex + 1, _rxHandlerCallbacks);
            totalHits += _rxHandlerCallbacks;
        }

        Console.WriteLine("    {0} hits in total", totalHits);
        if (totalHits > 0) {
            Console.WriteLine("    Failed");
            Exit(2);
        }
        Console.WriteLine("    Passed");
    }

    private static void LevelAndRatioTests(SigToneRxState state, double[] pitch) {
        Console.WriteLine("Acceptable level and ratio test - {0:F2} Hz + {1:F2} Hz", pitch[0], pitch[1]);
        int[] phaseRate = new int[2];
        uint[] phase = new uint[2];
        for (int i = 0; i < 2; i++)
            phaseRate[i] = pitch[i] != 0.0 ? Dds.PhaseRate((float)pitch[i]) : 0;

        short[] amplitudes = new short[SAMPLE_RATE];
        var noiseMeter = new PowerMeterState(6);
        var toneMeter = new PowerMeterState(6);

        for (int noiseIndex = -25; noiseIndex > -60; noiseIndex--) {
            float noiseLevel = noiseIndex;
            var noiseSource = new Awgn(1234567, noiseLevel);
            float toneLevel = noiseLevel;

            Array.Clear(amplitudes);
            state.Process(amplitudes.AsSpan());
            noiseMeter.Initialize(6);
            toneMeter.Initialize(6);

            for (int iteration = 0; iteration < 20; iteration++) {
                _rxHandlerCallbacks = 0;
                short gain = Dds.ScalingDbm0(toneLevel);
                for (int i = 0; i < SamplesPerChunk; i++) {
                    short noise = noiseSource.NextSample();
                    int tone = Dds.GenerateModulated(ref phase[0], phaseRate[0], gain, 0);
                    if (phaseRate[1] != 0)
                        tone += Dds.GenerateModulated(ref phase[1], phaseRate[1], gain, 0);

                    short toneSample = unchecked((short)tone);
                    noiseMeter.Update(noise);
                    toneMeter.Update(toneSample);
                    amplitudes[i] = unchecked((short)(noise + tone));
                }

                state.Process(amplitudes.AsSpan(0, SamplesPerChunk));
                if (_rxHandlerCallbacks != 0) {
                    Console.WriteLine("Hit at   tone = {0:F2}dBm0, noise = {1:F2}dBm0", toneLevel, noiseLevel);
                    Console.WriteLine("Measured tone = {0:F2}dBm0, noise = {1:F2}dBm0", toneMeter.CurrentDbm0, noiseMeter.CurrentDbm0);
                    if (_rxHandlerCallbacks != 1)
                        Console.WriteLine("Callbacks = {0}", _rxHandlerCallbacks);
                }
                toneLevel += 1.0f;
            }
        }
        Console.WriteLine("    Passed");
    }

    private static void SequenceTests(SigToneTxState txState, SigToneRxState rxState, SimCodecMungeState munge) {
        Console.WriteLine("Signalling sequence test");
        _txSection = 0;
        short[] amplitudes = new short[SamplesPerChunk];
        short[] output = new short[2 * SamplesPerChunk];
        var noiseSource = new Awgn(1234567, -20.0f);

        try {
            using var writer = new Pcm16WaveWriter(OutFileName, SAMPLE_RATE, 2);
            txState.SetMode(SIG_TONE_1_PRESENT | SIG_TONE_2_PRESENT | SIG_TONE_TX_PASSTHROUGH, 0);
            rxState.SetMode(SIG_TONE_RX_PASSTHROUGH, 0);

            for (_sampleNumber = 0; _sampleNumber < 4000; _sampleNumber += SamplesPerChunk) {
                if (_sampleNumber == 800) {
                    Console.WriteLine("Tx: [0000] off off for {0} samples ({1}ms)", milliseconds_to_samples(100), 100);
                    txState.SetMode(0, milliseconds_to_samples(100));
                }

                noiseSource.Fill(amplitudes.AsSpan());
                int txSamples = txState.Generate(amplitudes.AsSpan());
                for (int i = 0; i < txSamples; i++)
                    output[2 * i] = amplitudes[i];

                munge.Process(amplitudes.AsSpan(0, txSamples));
                int rxSamples = rxState.Process(amplitudes.AsSpan(0, txSamples));
                for (int i = 0; i < rxSamples; i++)
                    output[2 * i + 1] = amplitudes[i];

                if (writer.WriteFrames(output, rxSamples) != rxSamples) {
                    Console.Error.WriteLine("    Error writing audio file");
                    Exit(2);
                }
            }
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            Console.Error.WriteLine("    Cannot create or close audio file '{0}': {1}", OutFileName, ex.Message);
            Exit(2);
        }
    }

    private static void Exit(int code) => throw new SigToneTestsExitException(code);
}
