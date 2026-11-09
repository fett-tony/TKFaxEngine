/*
 * Fresh managed C# port of v8_tests.c from the supplied udptl-V17 archive.
 * Exercises the five SpanDSP V.8 call combinations and file decode mode.
 */
#nullable enable

using System.Buffers.Binary;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V8;
using V8Engine = TKFaxEngine.Modem.V8.V8;
using V8PcmModemAvailability = TKFaxEngine.Modem.V8.V8PcmModemAvailability;
using V8PstnAccess = TKFaxEngine.Modem.V8.V8PstnAccess;
using V8State = TKFaxEngine.Modem.V8.V8State;
using V8Status = TKFaxEngine.Modem.V8.V8Status;

namespace TKFaxEngineTest.Modem.Test;

public static class V8Tests {
    private const int SamplesPerChunk = 160;
    private const string OutputFileName = "v8.wav";
    private const int Caller = 0;
    private const int Answerer = 1;

    private static readonly V8Status?[] ExpectedStatus = new V8Status?[2];
    private static int _negotiationsOk;

    private static readonly V8Modulation AllModulations =
        V8Modulation.V17 |
        V8Modulation.V21 |
        V8Modulation.V22 |
        V8Modulation.V23HalfDuplex |
        V8Modulation.V23 |
        V8Modulation.V26Bis |
        V8Modulation.V26Ter |
        V8Modulation.V27Ter |
        V8Modulation.V29 |
        V8Modulation.V32 |
        V8Modulation.V34HalfDuplex |
        V8Modulation.V34 |
        V8Modulation.V90 |
        V8Modulation.V92;

    public static int Main(string[] testArguments) {
        try {
            return Run(testArguments);
        } catch (V8TestExitException exception) {
            return exception.ExitCode;
        } catch (Exception exception) {
            Console.Error.WriteLine(exception);
            return 2;
        }
    }

    public static int Run(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);
        string? decodeTestFile = null;
        bool logAudio = false;

        for (int argumentIndex = 0; argumentIndex < testArguments.Length; argumentIndex++) {
            string argument = testArguments[argumentIndex];
            if (argument == "--")
                break;
            if (argument.Length < 2 || argument[0] != '-')
                break;

            for (int optionIndex = 1; optionIndex < argument.Length; optionIndex++) {
                switch (argument[optionIndex]) {
                    case 'l':
                        logAudio = true;
                        break;

                    case 'd':
                        if (optionIndex + 1 < argument.Length) {
                            decodeTestFile = argument[(optionIndex + 1)..];
                        } else if (++argumentIndex < testArguments.Length) {
                            decodeTestFile = testArguments[argumentIndex];
                        } else {
                            throw new V8TestExitException(2);
                        }
                        optionIndex = argument.Length;
                        break;

                    default:
                        throw new V8TestExitException(2);
                }
            }
        }

        if (decodeTestFile is not null)
            return DecodeFromFile(decodeTestFile);

        using V8WaveWriter? output = logAudio ? new V8WaveWriter(OutputFileName, 2, 8000) : null;

        Console.WriteLine("Test 1: V.8 terminal calls V.8 terminal");
        V8CallsV8(output);
        InsertSilence(output);

        Console.WriteLine("Test 2: non-V.8 terminal without calling tone calls V.8 terminal");
        NonV8WithoutCallingToneCallsV8(output);
        InsertSilence(output);

        Console.WriteLine("Test 3: non-V.8 terminal with calling tone calls V.8 terminal");
        NonV8WithCallingToneCallsV8(output);
        InsertSilence(output);

        Console.WriteLine("Test 4: V.8 terminal calls non-V.8 terminal");
        V8CallsNonV8(output);
        InsertSilence(output);

        Console.WriteLine("Test 5: FAX calls V.8 terminal");
        FaxCallsV8(output);

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void ResultHandler(object? userData, V8Parameters result) {
        int side = userData is int value ? value : -1;
        string sideText = side == Caller ? "Caller" : "Answerer";
        Console.WriteLine($"{sideText} V.8 status {V8Engine.StatusToString(result.Status)}");
        Console.WriteLine($"  Modem connect tone '{ModemConnectTones.ToneToString(result.ModemConnectTone)}' ({(int)result.ModemConnectTone})");
        Console.WriteLine($"  Call function '{V8Engine.CallFunctionToString(result.JmCm.CallFunction)}' ({(int)result.JmCm.CallFunction})");
        Console.WriteLine($"  Supported modulations 0x{(uint)result.JmCm.Modulations:X}");
        Console.WriteLine($"  Protocol '{V8Engine.ProtocolToString(result.JmCm.Protocols)}' ({(int)result.JmCm.Protocols})");
        Console.WriteLine($"  PSTN access '{V8Engine.PstnAccessToString(result.JmCm.PstnAccess)}' ({(int)result.JmCm.PstnAccess})");
        Console.WriteLine($"  PCM modem availability '{V8Engine.PcmModemAvailabilityToString(result.JmCm.PcmModemAvailability)}' ({(int)result.JmCm.PcmModemAvailability})");
        if (result.JmCm.T66 >= 0)
            Console.WriteLine($"  T.66 {result.JmCm.T66}");
        if (result.JmCm.Nsf >= 0)
            Console.WriteLine($"  NSF {result.JmCm.Nsf}");

        switch (result.Status) {
            case V8Status.InProgress:
                break;

            case V8Status.V8Offered:
                result.JmCm.Modulations &=
                    V8Modulation.V17 |
                    V8Modulation.V21 |
                    V8Modulation.V27Ter |
                    V8Modulation.V29 |
                    V8Modulation.V34HalfDuplex |
                    V8Modulation.V34 |
                    V8Modulation.V92;
                break;

            case V8Status.V8Call:
                if (result.JmCm.CallFunction == V8CallFunction.VSeriesModem &&
                    result.JmCm.Protocols == V8Protocol.LapmV42 &&
                    side is Caller or Answerer &&
                    ExpectedStatus[side] == result.Status) {
                    _negotiationsOk++;
                }
                break;

            case V8Status.NonV8Call:
            case V8Status.CallingToneReceived:
            case V8Status.FaxCngToneReceived:
                if (side is Caller or Answerer && ExpectedStatus[side] == result.Status)
                    _negotiationsOk++;
                break;

            case V8Status.Failed:
            case V8Status.CallFunctionReceived:
                break;
        }
    }

    private static int V8CallsV8(V8WaveWriter? output) {
        _negotiationsOk = 0;
        ExpectedStatus[Caller] = V8Status.V8Call;
        ExpectedStatus[Answerer] = V8Status.V8Call;

        using V8State caller = V8Engine.Init(null, true, CreateParameters(ModemConnectTone.None, true), ResultHandler, Caller);
        using V8State answerer = V8Engine.Init(null, false, CreateParameters(ModemConnectTone.AnsamWithPhaseReversals, true), ResultHandler, Answerer);
        caller.LogHandler = message => Console.Error.WriteLine($"Caller: {message}");
        answerer.LogHandler = message => Console.Error.WriteLine($"Answerer: {message}");

        var callerAudio = new short[SamplesPerChunk];
        var answererAudio = new short[SamplesPerChunk];
        for (int iteration = 0; iteration < 1000; iteration++) {
            int callerSamples = GenerateV8(caller, callerAudio);
            int answererRemnant = answerer.Receive(callerAudio.AsSpan(0, callerSamples));

            int answererSamples = GenerateV8(answerer, answererAudio);
            int callerRemnant = caller.Receive(answererAudio.AsSpan(0, answererSamples));

            output?.WriteStereo(callerAudio, answererAudio, Math.Min(callerSamples, answererSamples));
            if (callerRemnant != 0 && answererRemnant != 0)
                break;
        }

        RequireNegotiations(2);
        return 0;
    }

    private static int NonV8WithoutCallingToneCallsV8(V8WaveWriter? output) {
        _negotiationsOk = 0;
        ExpectedStatus[Caller] = null;
        ExpectedStatus[Answerer] = V8Status.V8Call;

        using V8State answerer = V8Engine.Init(null, false, CreateParameters(ModemConnectTone.AnsamWithPhaseReversals, true), ResultHandler, Answerer);
        using ModemConnectTonesRxState callerDetector = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals);
        answerer.LogHandler = message => Console.Error.WriteLine($"Answerer: {message}");

        var callerAudio = new short[SamplesPerChunk];
        var answererAudio = new short[SamplesPerChunk];
        for (int iteration = 0; iteration < 1000; iteration++) {
            Array.Clear(callerAudio);
            int remnant = answerer.Receive(callerAudio);
            if (remnant != 0)
                break;

            int answererSamples = GenerateV8(answerer, answererAudio);
            callerDetector.Process(answererAudio.AsSpan(0, answererSamples));
            CountDetectedAnsamPr(callerDetector);
            output?.WriteStereo(callerAudio, answererAudio, answererSamples);
        }

        RequireNegotiations(1);
        return 0;
    }

    private static int NonV8WithCallingToneCallsV8(V8WaveWriter? output) {
        _negotiationsOk = 0;
        ExpectedStatus[Caller] = null;
        ExpectedStatus[Answerer] = V8Status.CallingToneReceived;

        using ModemConnectTonesTxState callerTone = ModemConnectTones.TransmitInit(ModemConnectTone.CallingTone);
        using ModemConnectTonesRxState callerDetector = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals);
        using V8State answerer = V8Engine.Init(null, false, CreateParameters(ModemConnectTone.AnsamWithPhaseReversals, true), ResultHandler, Answerer);
        answerer.LogHandler = message => Console.Error.WriteLine($"Answerer: {message}");

        var callerAudio = new short[SamplesPerChunk];
        var answererAudio = new short[SamplesPerChunk];
        for (int iteration = 0; iteration < 1000; iteration++) {
            int callerSamples = GenerateTone(callerTone, callerAudio);
            int remnant = answerer.Receive(callerAudio.AsSpan(0, callerSamples));
            if (remnant != 0)
                break;

            int answererSamples = GenerateV8(answerer, answererAudio);
            callerDetector.Process(answererAudio.AsSpan(0, answererSamples));
            CountDetectedAnsamPr(callerDetector);
            output?.WriteStereo(callerAudio, answererAudio, Math.Min(callerSamples, answererSamples));
        }

        RequireNegotiations(1);
        return 0;
    }

    private static int V8CallsNonV8(V8WaveWriter? output) {
        _negotiationsOk = 0;
        ExpectedStatus[Caller] = V8Status.NonV8Call;
        ExpectedStatus[Answerer] = null;

        using V8State caller = V8Engine.Init(null, true, CreateParameters(ModemConnectTone.None, true), ResultHandler, Caller);
        using ModemConnectTonesTxState answererTone = ModemConnectTones.TransmitInit(ModemConnectTone.AnsWithPhaseReversals);
        caller.LogHandler = message => Console.Error.WriteLine($"Caller: {message}");

        var callerAudio = new short[SamplesPerChunk];
        var answererAudio = new short[SamplesPerChunk];
        for (int iteration = 0; iteration < 1000; iteration++) {
            int callerSamples = GenerateV8(caller, callerAudio);
            int answererSamples = GenerateTone(answererTone, answererAudio);
            int remnant = caller.Receive(answererAudio.AsSpan(0, answererSamples));
            output?.WriteStereo(callerAudio, answererAudio, Math.Min(callerSamples, answererSamples));
            if (remnant != 0)
                break;
        }

        RequireNegotiations(1);
        return 0;
    }

    private static int FaxCallsV8(V8WaveWriter? output) {
        _negotiationsOk = 0;
        ExpectedStatus[Caller] = null;
        ExpectedStatus[Answerer] = V8Status.FaxCngToneReceived;

        using ModemConnectTonesTxState faxTone = ModemConnectTones.TransmitInit(ModemConnectTone.FaxCng);
        using ModemConnectTonesRxState faxDetector = ModemConnectTones.ReceiveInit(ModemConnectTone.AnsWithPhaseReversals);
        using V8State answerer = V8Engine.Init(null, false, CreateParameters(ModemConnectTone.AnsamWithPhaseReversals, true), ResultHandler, Answerer);
        answerer.LogHandler = message => Console.Error.WriteLine($"Answerer: {message}");

        var callerAudio = new short[SamplesPerChunk];
        var answererAudio = new short[SamplesPerChunk];
        for (int iteration = 0; iteration < 1000; iteration++) {
            int callerSamples = GenerateTone(faxTone, callerAudio);
            int remnant = answerer.Receive(callerAudio.AsSpan(0, callerSamples));
            if (remnant != 0)
                break;

            int answererSamples = GenerateV8(answerer, answererAudio);
            faxDetector.Process(answererAudio.AsSpan(0, answererSamples));
            CountDetectedAnsamPr(faxDetector);
            output?.WriteStereo(callerAudio, answererAudio, Math.Min(callerSamples, answererSamples));
        }

        RequireNegotiations(1);
        return 0;
    }

    private static int DecodeFromFile(string path) {
        Console.WriteLine($"Decode file '{path}'");
        using var input = new V8WaveReader(path, 8000);
        using V8State caller = V8Engine.Init(null, true, CreateParameters(ModemConnectTone.None, false), ResultHandler, Caller);
        using V8State answerer = V8Engine.Init(null, false, CreateParameters(ModemConnectTone.AnsamWithPhaseReversals, false), ResultHandler, Answerer);
        caller.LogHandler = message => Console.Error.WriteLine($"Caller: {message}");
        answerer.LogHandler = message => Console.Error.WriteLine($"Answerer: {message}");

        var audio = new short[SamplesPerChunk];
        int samples;
        while ((samples = input.Read(audio)) > 0) {
            caller.DecodeReceive(audio.AsSpan(0, samples));
            answerer.DecodeReceive(audio.AsSpan(0, samples));
        }
        return 0;
    }

    private static V8Parameters CreateParameters(ModemConnectTone connectTone, bool sendCi) {
        var parameters = new V8Parameters {
            ModemConnectTone = connectTone,
            GatewayMode = false,
            SendCi = sendCi,
            V92 = -1
        };
        parameters.JmCm.CallFunction = V8CallFunction.VSeriesModem;
        parameters.JmCm.Modulations = AllModulations;
        parameters.JmCm.Protocols = V8Protocol.LapmV42;
        parameters.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
        parameters.JmCm.PstnAccess = V8PstnAccess.None;
        parameters.JmCm.Nsf = -1;
        parameters.JmCm.T66 = -1;
        return parameters;
    }

    private static int GenerateV8(V8State state, short[] audio) {
        int samples = state.Transmit(audio);
        if (samples < audio.Length) {
            Array.Clear(audio, samples, audio.Length - samples);
            samples = audio.Length;
        }
        return samples;
    }

    private static int GenerateTone(ModemConnectTonesTxState state, short[] audio) {
        int samples = state.Generate(audio);
        if (samples < audio.Length) {
            Array.Clear(audio, samples, audio.Length - samples);
            samples = audio.Length;
        }
        return samples;
    }

    private static void CountDetectedAnsamPr(ModemConnectTonesRxState detector) {
        ModemConnectTone tone = detector.GetDetectedTone();
        if (tone == ModemConnectTone.None)
            return;
        Console.WriteLine($"Detected {ModemConnectTones.ToneToString(tone)} ({(int)tone})");
        if (tone == ModemConnectTone.AnsamWithPhaseReversals)
            _negotiationsOk++;
    }

    private static void RequireNegotiations(int expected) {
        if (_negotiationsOk != expected) {
            Console.WriteLine($"Tests failed. Expected {expected} successful negotiation events, got {_negotiationsOk}.");
            throw new V8TestExitException(2);
        }
        Console.WriteLine("Test passed.");
    }

    private static void InsertSilence(V8WaveWriter? output) {
        if (output is null)
            return;
        var silence = new short[SamplesPerChunk];
        for (int i = 0; i < 200; i++)
            output.WriteStereo(silence, silence, silence.Length);
    }
}

internal sealed class V8WaveReader : IDisposable {
    private readonly FileStream _stream;
    private readonly int _blockAlign;
    private long _remainingBytes;

    public V8WaveReader(string path, int requiredSampleRate) {
        _stream = File.OpenRead(path);
        var header = ParseHeader(_stream);
        _blockAlign = header.BlockAlign;
        _remainingBytes = header.DataLength;
        if (header.SampleRate != requiredSampleRate || header.Bits != 16 || header.Channels < 1)
            throw new InvalidDataException("V.8 decode requires 8 kHz, 16-bit PCM WAV input.");
        _stream.Position = header.DataOffset;
    }

    public int Read(Span<short> destination) {
        int frames = (int)Math.Min(destination.Length, _remainingBytes / _blockAlign);
        if (frames <= 0)
            return 0;
        byte[] bytes = new byte[frames * _blockAlign];
        int read = _stream.Read(bytes, 0, bytes.Length);
        frames = read / _blockAlign;
        for (int i = 0; i < frames; i++)
            destination[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i * _blockAlign, 2));
        _remainingBytes -= frames * _blockAlign;
        return frames;
    }

    public void Dispose() => _stream.Dispose();

    internal static (int Channels, int SampleRate, int Bits, int BlockAlign, long DataOffset, long DataLength)
        ParseHeader(Stream stream) {
        Span<byte> riff = stackalloc byte[12];
        ReadExactly(stream, riff);
        if (!riff[..4].SequenceEqual("RIFF"u8) || !riff[8..12].SequenceEqual("WAVE"u8))
            throw new InvalidDataException("Not a RIFF/WAVE file.");

        int channels = 0;
        int rate = 0;
        int bits = 0;
        int align = 0;
        long dataOffset = 0;
        long dataLength = 0;
        Span<byte> chunk = stackalloc byte[8];
        while (stream.Position + 8 <= stream.Length) {
            ReadExactly(stream, chunk);
            string id = global::System.Text.Encoding.ASCII.GetString(chunk[..4]);
            int length = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
            long next = stream.Position + length + (length & 1);
            if (id == "fmt ") {
                byte[] format = new byte[length];
                ReadExactly(stream, format);
                if (BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(0, 2)) != 1)
                    throw new InvalidDataException("Only PCM WAV is supported.");
                channels = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(2, 2));
                rate = BinaryPrimitives.ReadInt32LittleEndian(format.AsSpan(4, 4));
                align = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(12, 2));
                bits = BinaryPrimitives.ReadInt16LittleEndian(format.AsSpan(14, 2));
            } else if (id == "data") {
                dataOffset = stream.Position;
                dataLength = length;
                stream.Position = next;
            } else {
                stream.Position = next;
            }
            if (channels > 0 && dataOffset > 0)
                break;
        }
        if (channels <= 0 || align <= 0 || dataOffset <= 0)
            throw new InvalidDataException("Incomplete WAV header.");
        return (channels, rate, bits, align, dataOffset, dataLength);
    }

    private static void ReadExactly(Stream stream, Span<byte> destination) {
        int offset = 0;
        while (offset < destination.Length) {
            int read = stream.Read(destination[offset..]);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
        }
    }
}

internal sealed class V8WaveWriter : IDisposable {
    private readonly FileStream _stream;
    private readonly int _channels;
    private readonly int _sampleRate;
    private long _frames;

    public V8WaveWriter(string path, int channels, int sampleRate) {
        _channels = channels;
        _sampleRate = sampleRate;
        _stream = File.Create(path);
        _stream.Write(new byte[44]);
    }

    public void WriteStereo(ReadOnlySpan<short> left, ReadOnlySpan<short> right, int frames) {
        if (_channels != 2)
            throw new InvalidOperationException("This writer is not configured for stereo.");
        frames = Math.Min(frames, Math.Min(left.Length, right.Length));
        byte[] bytes = new byte[frames * 4];
        for (int i = 0; i < frames; i++) {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 4, 2), left[i]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 4 + 2, 2), right[i]);
        }
        _stream.Write(bytes);
        _frames += frames;
    }

    public void Dispose() {
        long dataBytes = _frames * _channels * 2L;
        _stream.Position = 0;
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..8], checked((int)(36 + dataBytes)));
        "WAVEfmt "u8.CopyTo(header[8..16]);
        BinaryPrimitives.WriteInt32LittleEndian(header[16..20], 16);
        BinaryPrimitives.WriteInt16LittleEndian(header[20..22], 1);
        BinaryPrimitives.WriteInt16LittleEndian(header[22..24], (short)_channels);
        BinaryPrimitives.WriteInt32LittleEndian(header[24..28], _sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(header[28..32], _sampleRate * _channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(header[32..34], (short)(_channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(header[34..36], 16);
        "data"u8.CopyTo(header[36..40]);
        BinaryPrimitives.WriteInt32LittleEndian(header[40..44], checked((int)dataBytes));
        _stream.Write(header);
        _stream.Dispose();
    }
}

public sealed class V8TestExitException : Exception {
    public V8TestExitException(int exitCode) : base($"V.8 test requested exit code {exitCode}.") {
        ExitCode = exitCode;
    }

    public int ExitCode { get; }
}
