/*
 * Fresh managed C# port of super_tone_rx_tests.c.
 * Supervisory-tone detector tests based on the spanDSP/TKFaxEngine source.
 */

#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.Audio.Test;

public static partial class SuperToneRxTests {
    private const string MitelDirectory = "Test-Files/mitel/";
    private const string BellcoreDirectory = "Test-Files/bellcore/";
    private const int SamplesPerChunk = 160;

    private static readonly string[] BellcoreFiles = {
        MitelDirectory + "mitel-cm7291-talkoff.wav",
        BellcoreDirectory + "tr-tsy-00763-1.wav",
        BellcoreDirectory + "tr-tsy-00763-2.wav",
        BellcoreDirectory + "tr-tsy-00763-3.wav",
        BellcoreDirectory + "tr-tsy-00763-4.wav",
        BellcoreDirectory + "tr-tsy-00763-5.wav",
        BellcoreDirectory + "tr-tsy-00763-6.wav"
    };

    private static readonly string?[] ToneNames = new string?[20];
    private static readonly ToneSegmentDefinition[,] ToneSegments = new ToneSegmentDefinition[20, 10];

    private static SuperToneTxStep? _dialToneTree;
    private static SuperToneTxStep? _ringbackTree;
    private static SuperToneTxStep? _busyToneTree;
    private static SuperToneTxStep? _numberUnobtainableToneTree;
    private static SuperToneTxStep? _congestionToneTree;
    private static SuperToneTxStep? _waitingToneTree;
    private static int _level;

    public static int Run(string[] args) {
        ArgumentNullException.ThrowIfNull(args);

        string? decodeTestFile = null;
        for (int index = 0; index < args.Length; index++) {
            switch (args[index]) {
                case "-d":
                    if (index + 1 >= args.Length)
                        return 2;
                    decodeTestFile = args[++index];
                    break;
                default:
                    if (args[index].StartsWith("-", StringComparison.Ordinal))
                        return 2;
                    break;
            }
        }

        Array.Clear(ToneNames, 0, ToneNames.Length);
        Array.Clear(ToneSegments, 0, ToneSegments.Length);
        _dialToneTree = null;
        _ringbackTree = null;
        _busyToneTree = null;
        _numberUnobtainableToneTree = null;
        _congestionToneTree = null;
        _waitingToneTree = null;

        SuperToneRxDescriptor descriptor = new();

        // Pure C# arguments begin at index 0; the first positional argument selects the tone set.
        string toneSetId = args.Length > 0 ? args[0] : "hk";
        Console.WriteLine($"Loading tone set {toneSetId}");
        try {
            GetToneSet(descriptor, "Test-Files/global-tones.xml", toneSetId);
        } catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or XmlException
            or InvalidDataException
            or InvalidOperationException) {
            Console.Error.WriteLine(exception.Message);
            Console.WriteLine("Test failed");
            return 2;
        }

        FillDescriptor(descriptor);

        SuperToneRx detector;
        try {
            detector = new SuperToneRx(descriptor, Wakeup, "test");
        } catch (Exception exception) {
            Console.WriteLine($"    Failed to create detector. {exception.Message}");
            return 2;
        }

        detector.SetSegmentCallback(ToneSegment);
        DetectionRangeTests(detector);

        if (decodeTestFile is not null) {
            if (FileDecodeTests(detector, decodeTestFile) != 0)
                return 2;
        }

        if (TalkOffTests(detector) != 0)
            return 2;

        Console.WriteLine("Done");
        return 0;
    }

    private static SuperToneTxStep? ParseTone(
        SuperToneRxDescriptor descriptor,
        int toneId,
        XElement parent) {
        SuperToneTxStep? root = null;
        SuperToneTxStep? tail = null;

        foreach (XElement current in parent.Elements().Where(element => element.Name.LocalName == "step")) {
            Console.Write("Step - ");

            float frequency1 = 0.0f;
            float frequency2 = 0.0f;
            float frequencyTolerance = 1.0f;
            float level1 = -11.0f;
            float level2 = -11.0f;
            float length = 0.0f;
            float lengthTolerance = 10.0f;
            float recognitionLength = 0.0f;
            float recognitionLengthTolerance = 10.0f;
            int cycles = 1;

            string? frequencyText = (string?)current.Attribute("freq");
            if (frequencyText is not null) {
                ParseFloatWithTolerance(frequencyText, ref frequency1, ref frequencyTolerance);
                ParseFrequencyPair(frequencyText, ref frequency1, ref frequency2, ref frequencyTolerance);
                Console.Write(
                    $" Frequency={frequency1:F2}+{frequency2:F2} [{frequencyTolerance:F2}%]");
            }

            string? levelText = (string?)current.Attribute("level");
            if (levelText is not null) {
                if (!ParseFloatPair(levelText, out level1, out level2)) {
                    if (TryParseLeadingFloat(levelText, out float parsedLevel))
                        level1 = parsedLevel;
                    level2 = level1;
                }
                Console.Write($" Level={level1:F2}+{level2:F2}");
            }

            string? lengthText = (string?)current.Attribute("length");
            if (lengthText is not null) {
                ParseFloatWithTolerance(lengthText, ref length, ref lengthTolerance);
                Console.Write($" Length={length:F2} [{lengthTolerance:F2}%]");
            }

            string? recognitionText = (string?)current.Attribute("recognition-length");
            if (recognitionText is not null) {
                ParseFloatWithTolerance(
                    recognitionText,
                    ref recognitionLength,
                    ref recognitionLengthTolerance);
                Console.Write(
                    $" Recognition length={recognitionLength:F2} " +
                    $"[{recognitionLengthTolerance:F2}%]");
            }

            string? cyclesText = (string?)current.Attribute("cycles");
            if (cyclesText is not null) {
                if (string.Equals(cyclesText, "endless", StringComparison.OrdinalIgnoreCase)) {
                    cycles = 0;
                } else if (!int.TryParse(
                    cyclesText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out cycles)) {
                    cycles = 0;
                }
                Console.Write($" Cycles='{cycles}' ");
            }

            string? recordedAnnouncement = (string?)current.Attribute("recorded-announcement");
            if (recordedAnnouncement is not null)
                Console.Write($" Recorded announcement='{recordedAnnouncement}'");

            Console.WriteLine();

            if (frequency1 != 0.0f || frequency2 != 0.0f || length != 0.0f) {
                int minimumDuration;
                int maximumDuration;

                if (length == 0.0f) {
                    minimumDuration = recognitionLength != 0.0f
                        ? (int)(recognitionLength * 1000.0f + 0.5f)
                        : 700;
                    maximumDuration = 0;
                } else {
                    minimumDuration = recognitionLength != 0.0f
                        ? (int)(recognitionLength * 1000.0f + 0.5f)
                        : (int)((length * 1000.0f + 0.5f)
                            * (1.0f - lengthTolerance / 100.0f) - 30.0f);
                    maximumDuration = (int)((length * 1000.0f + 0.5f)
                        * (1.0f + lengthTolerance / 100.0f) + 30.0f);
                }

                int roundedFrequency1 = (int)(frequency1 + 0.5f);
                int roundedFrequency2 = (int)(frequency2 + 0.5f);
                Console.WriteLine(
                    $">>>Detector element {roundedFrequency1,10} {roundedFrequency2,10} " +
                    $"{minimumDuration,10} {maximumDuration,10}");
                descriptor.AddElement(
                    toneId,
                    roundedFrequency1,
                    roundedFrequency2,
                    minimumDuration,
                    maximumDuration);
            }

            SuperToneTxStep step = new(
                frequency1,
                level1,
                frequency2,
                level2,
                (int)(length * 1000.0f + 0.5f),
                cycles);

            if (root is null)
                root = step;
            else
                tail!.Next = step;
            tail = step;

            step.Nest = ParseTone(descriptor, toneId, current);
        }

        return root;
    }

    private static void ParseToneSet(SuperToneRxDescriptor descriptor, XElement toneSet) {
        Console.WriteLine("Parsing tone set");

        foreach (XElement current in toneSet.Elements()) {
            int toneId;
            switch (current.Name.LocalName) {
                case "dial-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "Dial tone");
                    _dialToneTree = ParseTone(descriptor, toneId, current);
                    break;

                case "ringback-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "Ringback tone");
                    _ringbackTree = ParseTone(descriptor, toneId, current);
                    break;

                case "busy-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "Busy tone");
                    _busyToneTree = ParseTone(descriptor, toneId, current);
                    break;

                case "number-unobtainable-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "NU tone");
                    _numberUnobtainableToneTree = ParseTone(descriptor, toneId, current);
                    break;

                case "congestion-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "Congestion tone");
                    _congestionToneTree = ParseTone(descriptor, toneId, current);
                    break;

                case "waiting-tone":
                    Console.WriteLine($"Hit {current.Name.LocalName}");
                    toneId = AddNamedTone(descriptor, "Waiting tone");
                    _waitingToneTree = ParseTone(descriptor, toneId, current);
                    break;
            }
        }
    }

    private static int AddNamedTone(SuperToneRxDescriptor descriptor, string name) {
        int toneId = descriptor.AddTone();
        if ((uint)toneId >= ToneNames.Length)
            throw new InvalidDataException("The tone set exceeds the native 20-tone test limit.");
        ToneNames[toneId] = name;
        return toneId;
    }

    private static void GetToneSet(
        SuperToneRxDescriptor descriptor,
        string toneFile,
        string setId) {
        XmlReaderSettings settings = new() {
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.DTD,
            XmlResolver = new XmlUrlResolver(),
            IgnoreComments = false,
            IgnoreWhitespace = true
        };

        List<string> validationErrors = new();
        settings.ValidationEventHandler += (_, eventArgs) => {
            if (eventArgs.Severity == XmlSeverityType.Error)
                validationErrors.Add(eventArgs.Message);
        };

        XDocument document;
        using (XmlReader reader = XmlReader.Create(toneFile, settings)) {
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }

        if (validationErrors.Count != 0) {
            throw new InvalidDataException(
                "Failed to validate the XML document: " +
                string.Join(" | ", validationErrors));
        }

        XElement root = document.Root
            ?? throw new InvalidDataException("Empty document");
        if (root.Name.LocalName != "global-tones") {
            throw new InvalidDataException(
                "Document of the wrong type, root node != global-tones");
        }

        foreach (XElement current in root.Elements().Where(
            element => element.Name.LocalName == "tone-set")) {
            string? uncode = (string?)current.Attribute("uncode");
            if (string.Equals(uncode, setId, StringComparison.Ordinal))
                ParseToneSet(descriptor, current);
        }
    }

    private static void FillDescriptor(SuperToneRxDescriptor descriptor) {
        int toneId = AddNamedTone(descriptor, "XXX");
        descriptor.AddElement(toneId, 400, 0, 700, 0);

        toneId = AddNamedTone(descriptor, "FAX tone");
        descriptor.AddElement(toneId, 1100, 0, 400, 600);
        descriptor.AddElement(toneId, 0, 0, 2800, 3200);
    }

    private static void Wakeup(object? data, int code, int level, int delay) {
        string label = data as string ?? string.Empty;
        if (code >= 0) {
            string toneName = (uint)code < ToneNames.Length && ToneNames[code] is not null
                ? ToneNames[code]!
                : "???";
            Console.WriteLine($"Current tone is {code} '{toneName}' '{label}'");
        } else {
            Console.WriteLine($"Tone off '{label}'");
        }
    }

    private static void ToneSegment(
        object? data,
        int frequency1,
        int frequency2,
        int durationMilliseconds) {
        if (frequency1 < 0)
            Console.WriteLine($"Result {durationMilliseconds,5} silence");
        else if (frequency2 < 0)
            Console.WriteLine($"Result {durationMilliseconds,5} {frequency1,4}");
        else
            Console.WriteLine(
                $"Result {durationMilliseconds,5} {frequency1,4} + {frequency2,4}");
    }

    private static int TalkOffTests(SuperToneRx detector) {
        short[] samples = new short[8000];
        Console.WriteLine("Talk off tests");

        foreach (string fileName in BellcoreFiles) {
            try {
                using WavePcm16Reader reader = new(fileName);
                int frames;
                while ((frames = reader.ReadSamples(samples)) != 0)
                    ProcessAll(detector, samples, frames);
            } catch (Exception exception) when (
                exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or NotSupportedException) {
                Console.WriteLine($"    Cannot open audio file '{fileName}'");
                Console.Error.WriteLine(exception.Message);
                return 2;
            }
        }

        return 0;
    }

    private static int DetectionRangeTests(SuperToneRx detector) {
        short[] samples = new short[SamplesPerChunk];
        uint[] phase = new uint[2];
        int[] phaseIncrement = {
            Dds.PhaseRate(350.0f),
            Dds.PhaseRate(440.0f)
        };

        Console.WriteLine("Detection range tests");
        detector.SetToneCallback(Wakeup, "test");

        for (_level = -80; _level < 0; _level++) {
            Console.WriteLine($"Testing at {_level}dBm0");
            short scale = Dds.ScalingDbm0(_level);

            for (int block = 0; block < 100; block++) {
                for (int index = 0; index < samples.Length; index++) {
                    short value = unchecked((short)(
                        (Dds.Generate(ref phase[0], phaseIncrement[0]) * scale) >> 15));
                    value = unchecked((short)(value + (
                        (Dds.Generate(ref phase[1], phaseIncrement[1]) * scale) >> 15)));
                    // The source intentionally calls the 440 Hz DDS a second time.
                    value = unchecked((short)(value + (
                        (Dds.Generate(ref phase[1], phaseIncrement[1]) * scale) >> 15)));
                    samples[index] = value;
                }

                detector.Process(samples);
            }
        }

        return 0;
    }

    private static int FileDecodeTests(SuperToneRx detector, string fileName) {
        short[] samples = new short[8000];
        Console.WriteLine("File decode tests");
        detector.SetToneCallback(Wakeup, "test");

        // Kept to mirror awgn_init_dbm0 in the native test. The source's noise
        // injection loop is commented out, so no samples are modified here.
        _ = new Awgn(1_234_567, -30.0);

        Console.WriteLine("Processing file");
        try {
            using WavePcm16Reader reader = new(fileName);
            int frames;
            while ((frames = reader.ReadSamples(samples)) != 0)
                ProcessAll(detector, samples, frames);
        } catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or NotSupportedException) {
            Console.Error.WriteLine($"    Cannot open audio file '{fileName}'");
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        return 0;
    }

    private static void ProcessAll(SuperToneRx detector, short[] samples, int count) {
        int sample = 0;
        while (sample < count) {
            int processed = detector.Process(samples, sample, count - sample);
            if (processed <= 0)
                throw new InvalidDataException("The tone detector made no forward progress.");
            sample += processed;
        }
    }

    private static void ParseFloatWithTolerance(
        string text,
        ref float value,
        ref float tolerance) {
        Match match = FloatToleranceRegex().Match(text);
        if (!match.Success)
            return;

        value = ParseInvariantFloat(match.Groups["value"].Value);
        if (match.Groups["tolerance"].Success)
            tolerance = ParseInvariantFloat(match.Groups["tolerance"].Value);
    }

    private static void ParseFrequencyPair(
        string text,
        ref float frequency1,
        ref float frequency2,
        ref float tolerance) {
        Match match = FloatPairToleranceRegex().Match(text);
        if (!match.Success)
            return;

        frequency1 = ParseInvariantFloat(match.Groups["first"].Value);
        frequency2 = ParseInvariantFloat(match.Groups["second"].Value);
        if (match.Groups["tolerance"].Success)
            tolerance = ParseInvariantFloat(match.Groups["tolerance"].Value);
    }

    private static bool ParseFloatPair(string text, out float first, out float second) {
        Match match = FloatPairRegex().Match(text);
        if (!match.Success) {
            first = 0.0f;
            second = 0.0f;
            return false;
        }

        first = ParseInvariantFloat(match.Groups["first"].Value);
        second = ParseInvariantFloat(match.Groups["second"].Value);
        return true;
    }

    private static bool TryParseLeadingFloat(string text, out float value) {
        Match match = LeadingFloatRegex().Match(text);
        if (!match.Success) {
            value = 0.0f;
            return false;
        }

        value = ParseInvariantFloat(match.Groups["value"].Value);
        return true;
    }

    private static float ParseInvariantFloat(string text) =>
        float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);

    [GeneratedRegex(
        @"^\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+))(?:\s*\[\s*(?<tolerance>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*%\s*\])?",
        RegexOptions.CultureInvariant)]
    private static partial Regex FloatToleranceRegex();

    [GeneratedRegex(
        @"^\s*(?<first>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*\+\s*(?<second>[+-]?(?:\d+(?:\.\d*)?|\.\d+))(?:\s*\[\s*(?<tolerance>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*%\s*\])?",
        RegexOptions.CultureInvariant)]
    private static partial Regex FloatPairToleranceRegex();

    [GeneratedRegex(
        @"^\s*(?<first>[+-]?(?:\d+(?:\.\d*)?|\.\d+))\s*\+\s*(?<second>[+-]?(?:\d+(?:\.\d*)?|\.\d+))",
        RegexOptions.CultureInvariant)]
    private static partial Regex FloatPairRegex();

    [GeneratedRegex(
        @"^\s*(?<value>[+-]?(?:\d+(?:\.\d*)?|\.\d+))",
        RegexOptions.CultureInvariant)]
    private static partial Regex LeadingFloatRegex();

    private readonly record struct ToneSegmentDefinition(
        int Frequency1,
        int Frequency2,
        int DurationMilliseconds);

    private sealed class WavePcm16Reader : IDisposable {
        private readonly FileStream _stream;
        private readonly long _dataEnd;
        private byte[] _byteBuffer = Array.Empty<byte>();

        public WavePcm16Reader(string fileName) {
            _stream = File.OpenRead(fileName);
            try {
                (long dataEnd, long dataStart) = ParseHeader(_stream);
                _dataEnd = dataEnd;
                _stream.Position = dataStart;
            } catch {
                _stream.Dispose();
                throw;
            }
        }

        public int ReadSamples(short[] destination) {
            ArgumentNullException.ThrowIfNull(destination);
            long remaining = _dataEnd - _stream.Position;
            if (remaining <= 0)
                return 0;

            int sampleCount = (int)Math.Min(destination.Length, remaining / 2);
            int bytesRequired = checked(sampleCount * 2);
            if (_byteBuffer.Length < bytesRequired)
                _byteBuffer = new byte[bytesRequired];

            int total = 0;
            while (total < bytesRequired) {
                int read = _stream.Read(_byteBuffer, total, bytesRequired - total);
                if (read == 0)
                    throw new InvalidDataException("Unexpected end of PCM WAV data.");
                total += read;
            }

            for (int index = 0; index < sampleCount; index++) {
                destination[index] = BinaryPrimitives.ReadInt16LittleEndian(
                    _byteBuffer.AsSpan(index * 2, 2));
            }
            return sampleCount;
        }

        public void Dispose() => _stream.Dispose();

        private static (long DataEnd, long DataStart) ParseHeader(Stream stream) {
            Span<byte> riffHeader = stackalloc byte[12];
            ReadExactly(stream, riffHeader);
            if (!riffHeader[..4].SequenceEqual("RIFF"u8)
                || !riffHeader[8..12].SequenceEqual("WAVE"u8)) {
                throw new InvalidDataException("The input file is not a RIFF/WAVE file.");
            }

            bool formatSeen = false;
            ushort formatTag = 0;
            ushort channels = 0;
            uint sampleRate = 0;
            ushort bitsPerSample = 0;
            long dataStart = -1;
            long dataEnd = -1;
            Span<byte> chunkHeader = stackalloc byte[8];

            while (stream.Position + 8 <= stream.Length) {
                ReadExactly(stream, chunkHeader);
                uint chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
                long chunkDataStart = stream.Position;
                long chunkDataEnd = checked(chunkDataStart + chunkLength);
                if (chunkDataEnd > stream.Length)
                    throw new InvalidDataException("A WAV chunk extends beyond the file length.");

                if (chunkHeader[..4].SequenceEqual("fmt "u8)) {
                    if (chunkLength < 16)
                        throw new InvalidDataException("The WAV fmt chunk is too short.");
                    Span<byte> format = stackalloc byte[16];
                    ReadExactly(stream, format);
                    formatTag = BinaryPrimitives.ReadUInt16LittleEndian(format[0..2]);
                    channels = BinaryPrimitives.ReadUInt16LittleEndian(format[2..4]);
                    sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(format[4..8]);
                    bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(format[14..16]);
                    formatSeen = true;
                } else if (chunkHeader[..4].SequenceEqual("data"u8)) {
                    dataStart = chunkDataStart;
                    dataEnd = chunkDataEnd;
                }

                stream.Position = chunkDataEnd + (chunkLength & 1U);
                if (formatSeen && dataStart >= 0)
                    break;
            }

            if (!formatSeen || dataStart < 0)
                throw new InvalidDataException("The WAV file lacks fmt or data chunks.");
            if (formatTag != 1)
                throw new NotSupportedException($"WAV format {formatTag} is not PCM.");
            if (channels != 1)
                throw new InvalidDataException($"Expected mono telephony WAV, found {channels} channels.");
            if (sampleRate != 8000)
                throw new InvalidDataException($"Expected 8000 Hz telephony WAV, found {sampleRate} Hz.");
            if (bitsPerSample != 16)
                throw new InvalidDataException($"Expected 16-bit PCM WAV, found {bitsPerSample} bits.");
            if (((dataEnd - dataStart) & 1) != 0)
                throw new InvalidDataException("The PCM data chunk contains a partial sample.");

            return (dataEnd, dataStart);
        }

        private static void ReadExactly(Stream stream, Span<byte> destination) {
            int total = 0;
            while (total < destination.Length) {
                int read = stream.Read(destination[total..]);
                if (read == 0)
                    throw new InvalidDataException("Unexpected end of WAV file.");
                total += read;
            }
        }
    }
}
