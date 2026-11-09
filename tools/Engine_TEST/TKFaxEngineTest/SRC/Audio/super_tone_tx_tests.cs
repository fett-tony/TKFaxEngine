/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * super_tone_tx_tests.cs - Managed C# port of the spanDSP supervisory-tone
 * generation test.
 *
 * Original test written by Steve Underwood.
 * Copyright (C) 2003 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static class super_tone_tx_tests {
    private const string OutFileName = "super_tone.wav";
    private const string DefaultToneFile = "Test-Files/global-tones.xml";
    private const int MaximumBlocks = 500;
    private const int BlockSamples = 160;

    public static int tkfx_super_tone_tx_tests_body_main(string[] testArguments) {
        string setId = testArguments.Length > 0 ? testArguments[0] : "hk";

        try {
            List<SuperToneTxStep> trees = get_tone_set(DefaultToneFile, setId);
            using TelephonyWaveWriter writer = new(OutFileName, SuperToneTx.SampleRate, 1);

            foreach (SuperToneTxStep tree in trees) {
                SuperToneTx tone = new(tree);
                play_tones(tone, writer, int.MaxValue);
            }

            Console.WriteLine("Done");
            return 0;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception.Message);
            Console.WriteLine("Test failed");
            return 2;
        }
    }

    private static void play_tones(SuperToneTx tone, TelephonyWaveWriter writer, int maxSamples) {
        short[] samples = new short[BlockSamples];
        int totalLength = 0;
        int blocks = MaximumBlocks;
        int length;

        do {
            int remaining = maxSamples - totalLength;
            if (remaining <= 0)
                break;

            int requested = Math.Min(samples.Length, remaining);
            length = tone.Generate(samples, 0, requested);
            if (length > 0)
                writer.Write(samples.AsSpan(0, length));
            totalLength += length;
        }
        while (length > 0 && --blocks > 0);

        Console.WriteLine($"Tone length = {totalLength} samples ({totalLength / 8}ms)");
    }

    private static SuperToneTxStep? parse_tone(XElement parent) {
        SuperToneTxStep? first = null;
        SuperToneTxStep? previous = null;

        foreach (XElement element in parent.Elements("step")) {
            float frequency1 = 0.0f;
            float frequency2 = 0.0f;
            float frequencyTolerance = 1.0f;
            float level1 = -11.0f;
            float level2 = -11.0f;
            float lengthSeconds = 0.0f;
            float lengthTolerance = 10.0f;
            int cycles = 1;

            Console.Write("Step - ");

            string? frequencyText = (string?)element.Attribute("freq");
            if (!string.IsNullOrWhiteSpace(frequencyText)) {
                ParseFrequency(frequencyText, out frequency1, out frequency2, out frequencyTolerance);
                Console.Write($"Frequency={frequency1:F2}+{frequency2:F2} [{frequencyTolerance:F2}%] ");
            }

            string? levelText = (string?)element.Attribute("level");
            if (!string.IsNullOrWhiteSpace(levelText)) {
                ParsePair(levelText, out level1, out level2);
                Console.Write($"Level={level1:F2}+{level2:F2} ");
            }

            string? lengthText = (string?)element.Attribute("length");
            if (!string.IsNullOrWhiteSpace(lengthText)) {
                ParseValueAndTolerance(lengthText, out lengthSeconds, out lengthTolerance);
                Console.Write($"Length={lengthSeconds:F2} [{lengthTolerance:F2}%] ");
            }

            string? recognitionLength = (string?)element.Attribute("recognition-length");
            if (recognitionLength is not null)
                Console.Write($"Recognition length='{recognitionLength}' ");

            string? cyclesText = (string?)element.Attribute("cycles");
            if (!string.IsNullOrWhiteSpace(cyclesText)) {
                cycles = cyclesText.Equals("endless", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : int.Parse(cyclesText, CultureInfo.InvariantCulture);
                Console.Write($"Cycles={cycles} ");
            }

            string? announcement = (string?)element.Attribute("recorded-announcement");
            if (announcement is not null)
                Console.Write($"Recorded announcement='{announcement}' ");

            Console.WriteLine();

            int lengthMilliseconds = checked((int)(lengthSeconds * 1000.0f + 0.5f));
            SuperToneTxStep step = new(
                frequency1,
                level1,
                frequency2,
                level2,
                lengthMilliseconds,
                cycles);

            step.Nest = parse_tone(element);

            if (first is null)
                first = step;
            else
                previous!.Next = step;

            previous = step;
        }

        return first;
    }

    private static List<SuperToneTxStep> parse_tone_set(XElement toneSet) {
        Console.WriteLine("Parsing tone set");
        List<SuperToneTxStep> result = new();

        foreach (XElement toneElement in toneSet.Elements()) {
            if (!toneElement.Name.LocalName.EndsWith("-tone", StringComparison.Ordinal))
                continue;

            Console.WriteLine($"Hit {toneElement.Name.LocalName}");
            SuperToneTxStep? tree = parse_tone(toneElement);
            if (tree is not null)
                result.Add(tree);
        }

        return result;
    }

    private static List<SuperToneTxStep> get_tone_set(string toneFile, string setId) {
        if (!File.Exists(toneFile))
            throw new FileNotFoundException("Failed to read the XML document", toneFile);

        XmlReaderSettings settings = new() {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver(),
            IgnoreWhitespace = true
        };

        using XmlReader reader = XmlReader.Create(toneFile, settings);
        XDocument document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        XElement root = document.Root ?? throw new InvalidDataException("Empty document");

        if (root.Name.LocalName != "global-tones")
            throw new InvalidDataException("Document of the wrong type, root node != global-tones");

        foreach (XElement toneSet in root.Elements("tone-set")) {
            string? code = (string?)toneSet.Attribute("uncode");
            if (string.Equals(code, setId, StringComparison.Ordinal))
                return parse_tone_set(toneSet);
        }

        throw new InvalidDataException($"Tone set '{setId}' was not found in '{toneFile}'.");
    }

    private static void ParseFrequency(string text, out float first, out float second, out float tolerance) {
        first = 0.0f;
        second = 0.0f;
        tolerance = 1.0f;

        Match match = Regex.Match(
            text,
            @"^\s*(?<f1>[+-]?[0-9]+(?:\.[0-9]+)?)\s*(?:\+\s*(?<f2>[+-]?[0-9]+(?:\.[0-9]+)?))?\s*(?:\[\s*(?<tol>[+-]?[0-9]+(?:\.[0-9]+)?)%\s*\])?",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            return;

        first = ParseFloat(match.Groups["f1"].Value);
        if (match.Groups["f2"].Success)
            second = ParseFloat(match.Groups["f2"].Value);
        if (match.Groups["tol"].Success)
            tolerance = ParseFloat(match.Groups["tol"].Value);
    }

    private static void ParsePair(string text, out float first, out float second) {
        Match match = Regex.Match(
            text,
            @"^\s*(?<first>[+-]?[0-9]+(?:\.[0-9]+)?)\s*(?:\+\s*(?<second>[+-]?[0-9]+(?:\.[0-9]+)?))?",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            throw new FormatException($"Invalid numeric pair '{text}'.");

        first = ParseFloat(match.Groups["first"].Value);
        second = match.Groups["second"].Success
            ? ParseFloat(match.Groups["second"].Value)
            : first;
    }

    private static void ParseValueAndTolerance(string text, out float value, out float tolerance) {
        Match match = Regex.Match(
            text,
            @"^\s*(?<value>[+-]?[0-9]+(?:\.[0-9]+)?)\s*(?:\[\s*(?<tol>[+-]?[0-9]+(?:\.[0-9]+)?)%\s*\])?",
            RegexOptions.CultureInvariant);

        if (!match.Success)
            throw new FormatException($"Invalid numeric value '{text}'.");

        value = ParseFloat(match.Groups["value"].Value);
        tolerance = match.Groups["tol"].Success ? ParseFloat(match.Groups["tol"].Value) : 10.0f;
    }

    private static float ParseFloat(string value) =>
        float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private sealed class TelephonyWaveWriter : IDisposable {
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly int _sampleRate;
        private readonly short _channels;
        private int _sampleCount;
        private bool _disposed;

        public TelephonyWaveWriter(string path, int sampleRate, short channels) {
            _sampleRate = sampleRate;
            _channels = channels;
            _stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream);
            WriteHeader(0);
        }

        public void Write(ReadOnlySpan<short> samples) {
            foreach (short sample in samples)
                _writer.Write(sample);
            _sampleCount = checked(_sampleCount + samples.Length);
        }

        public void Dispose() {
            if (_disposed)
                return;

            _writer.Flush();
            _stream.Position = 0;
            WriteHeader(checked(_sampleCount * sizeof(short)));
            _writer.Dispose();
            _stream.Dispose();
            _disposed = true;
        }

        private void WriteHeader(int dataLength) {
            int byteRate = checked(_sampleRate * _channels * sizeof(short));
            short blockAlign = checked((short)(_channels * sizeof(short)));

            _writer.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            _writer.Write(checked(36 + dataLength));
            _writer.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            _writer.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            _writer.Write(16);
            _writer.Write((short)1);
            _writer.Write(_channels);
            _writer.Write(_sampleRate);
            _writer.Write(byteRate);
            _writer.Write(blockAlign);
            _writer.Write((short)16);
            _writer.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            _writer.Write(dataLength);
        }
    }
}
