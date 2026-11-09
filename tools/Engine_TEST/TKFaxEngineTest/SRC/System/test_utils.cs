/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * test_utils.cs - Utility declarations and routines for module tests.
 *
 * Managed C# port preserving the supplied spanDSP test helper algorithms.
 */

#nullable enable

using System.Buffers.Binary;
using TKFaxEngine;
using TKFaxEngine.Audio;

using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;

namespace TKFaxEngineTest.System;

public enum MungeCodec {
    None = 0,
    ALaw,
    ULaw,
    G72640K,
    G72632K,
    G72624K,
    G72616K
}

public sealed class CodecMungeState : IDisposable {
    public const int MUNGE_CODEC_NONE = (int)MungeCodec.None;
    public const int MUNGE_CODEC_ALAW = (int)MungeCodec.ALaw;
    public const int MUNGE_CODEC_ULAW = (int)MungeCodec.ULaw;
    public const int MUNGE_CODEC_G726_40K = (int)MungeCodec.G72640K;
    public const int MUNGE_CODEC_G726_32K = (int)MungeCodec.G72632K;
    public const int MUNGE_CODEC_G726_24K = (int)MungeCodec.G72624K;
    public const int MUNGE_CODEC_G726_16K = (int)MungeCodec.G72616K;

    internal MungeCodec MungingCodec;
    internal g726_state_t? G726EncoderState;
    internal g726_state_t? G726DecoderState;
    internal int RbsPattern;
    internal int Sequence;
    private bool disposed;

    public void Dispose() {
        if (disposed)
            return;

        if (G726EncoderState is not null)
            g726_free(G726EncoderState);
        if (G726DecoderState is not null)
            g726_free(G726DecoderState);
        G726EncoderState = null;
        G726DecoderState = null;
        disposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class ComplexifyState {
    internal readonly float[] History = new float[128];
    internal int Pointer;
}

public sealed class TestUtilityExitException : Exception {
    public TestUtilityExitException(int exitCode) => ExitCode = exitCode;
    public int ExitCode { get; }
}

public enum Pcm16WaveFileMode {
    Read,
    Write
}

/// <summary>
/// Managed PCM16 RIFF/WAVE handle used in place of libsndfile by the converted
/// spanDSP tests. It accepts ordinary PCM WAVE files with arbitrary extra chunks.
/// </summary>
public sealed class Pcm16WaveFile : IDisposable {
    private readonly FileStream stream;
    private readonly BinaryReader? reader;
    private readonly BinaryWriter? writer;
    private readonly long dataStart;
    private readonly long declaredDataLength;
    private long samplesWritten;
    private long bytesRead;
    private bool disposed;

    private Pcm16WaveFile(
        FileStream stream,
        BinaryReader reader,
        int sampleRate,
        int channels,
        long dataStart,
        long dataLength) {
        this.stream = stream;
        this.reader = reader;
        SampleRate = sampleRate;
        Channels = channels;
        Mode = Pcm16WaveFileMode.Read;
        this.dataStart = dataStart;
        declaredDataLength = dataLength;
    }

    private Pcm16WaveFile(
        FileStream stream,
        BinaryWriter writer,
        int sampleRate,
        int channels) {
        this.stream = stream;
        this.writer = writer;
        SampleRate = sampleRate;
        Channels = channels;
        Mode = Pcm16WaveFileMode.Write;
        dataStart = 44;
        declaredDataLength = 0;
        WriteHeader(writer, sampleRate, channels);
    }

    public int SampleRate { get; }
    public int Channels { get; }
    public int BitsPerSample => 16;
    public Pcm16WaveFileMode Mode { get; }
    public long FramesWritten => Channels == 0 ? 0 : samplesWritten / Channels;

    public static Pcm16WaveFile OpenRead(string fileName) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        FileStream stream = new(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        BinaryReader reader = new(stream);
        try {
            if (ReadFourCc(reader) != "RIFF")
                throw new InvalidDataException("The file is not a RIFF file.");

            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
                throw new InvalidDataException("The RIFF file is not a WAVE file.");

            int audioFormat = -1;
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;
            long dataStart = -1;
            long dataLength = 0;

            while (stream.Position + 8 <= stream.Length) {
                string chunkId = ReadFourCc(reader);
                uint chunkLength = reader.ReadUInt32();
                long chunkStart = stream.Position;
                long nextChunk = checked(chunkStart + chunkLength + (chunkLength & 1U));
                if (nextChunk > stream.Length + 1)
                    throw new InvalidDataException("The WAVE chunk extends past the end of the file.");

                switch (chunkId) {
                    case "fmt ":
                        if (chunkLength < 16)
                            throw new InvalidDataException("The WAVE fmt chunk is too short.");
                        audioFormat = reader.ReadUInt16();
                        channels = reader.ReadUInt16();
                        sampleRate = checked((int)reader.ReadUInt32());
                        _ = reader.ReadUInt32();
                        _ = reader.ReadUInt16();
                        bitsPerSample = reader.ReadUInt16();
                        break;
                    case "data":
                        dataStart = stream.Position;
                        dataLength = Math.Min(chunkLength, stream.Length - dataStart);
                        break;
                }

                stream.Position = Math.Min(nextChunk, stream.Length);
                if (audioFormat >= 0 && dataStart >= 0)
                    break;
            }

            if (audioFormat != 1)
                throw new InvalidDataException("Only linear PCM WAVE files are supported.");
            if (channels <= 0)
                throw new InvalidDataException("The WAVE file has no channels.");
            if (sampleRate <= 0)
                throw new InvalidDataException("The WAVE file has an invalid sample rate.");
            if (bitsPerSample != 16)
                throw new InvalidDataException("Only signed 16-bit PCM WAVE files are supported.");
            if (dataStart < 0)
                throw new InvalidDataException("The WAVE file has no data chunk.");

            stream.Position = dataStart;
            return new Pcm16WaveFile(
                stream,
                reader,
                sampleRate,
                channels,
                dataStart,
                dataLength);
        } catch {
            reader.Dispose();
            stream.Dispose();
            throw;
        }
    }

    public static Pcm16WaveFile OpenWrite(string fileName, int sampleRate, int channels) {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0 || channels > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(channels));

        FileStream stream = new(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        BinaryWriter writer = new(stream);
        try {
            return new Pcm16WaveFile(stream, writer, sampleRate, channels);
        } catch {
            writer.Dispose();
            stream.Dispose();
            throw;
        }
    }

    public int ReadFrames(short[] destination, int maximumFrames) {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Mode != Pcm16WaveFileMode.Read)
            throw new InvalidOperationException("The WAVE file is not open for reading.");
        if (maximumFrames < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFrames));

        int requestedSamples = checked(maximumFrames * Channels);
        if (requestedSamples > destination.Length)
            throw new ArgumentException("The destination buffer is too small.", nameof(destination));

        long remainingBytes = declaredDataLength - bytesRead;
        int availableSamples = checked((int)Math.Min(
            requestedSamples,
            Math.Max(0, remainingBytes / sizeof(short))));

        int samplesRead = 0;
        for (; samplesRead < availableSamples; samplesRead++) {
            Span<byte> bytes = stackalloc byte[2];
            int read = stream.Read(bytes);
            if (read != 2)
                break;
            destination[samplesRead] = BinaryPrimitives.ReadInt16LittleEndian(bytes);
            bytesRead += 2;
        }

        return samplesRead / Channels;
    }

    public int WriteFrames(ReadOnlySpan<short> samples) {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (Mode != Pcm16WaveFileMode.Write)
            throw new InvalidOperationException("The WAVE file is not open for writing.");
        if (samples.Length % Channels != 0)
            throw new ArgumentException("The sample count must contain complete frames.", nameof(samples));

        foreach (short sample in samples)
            writer!.Write(sample);
        samplesWritten += samples.Length;
        return samples.Length / Channels;
    }

    public void Dispose() {
        if (disposed)
            return;

        if (Mode == Pcm16WaveFileMode.Write) {
            long dataBytes = checked(samplesWritten * sizeof(short));
            writer!.Flush();
            stream.Position = 4;
            writer.Write(checked((uint)(36L + dataBytes)));
            stream.Position = 40;
            writer.Write(checked((uint)dataBytes));
            writer.Flush();
        }

        reader?.Dispose();
        writer?.Dispose();
        stream.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void WriteHeader(BinaryWriter writer, int sampleRate, int channels) {
        int blockAlign = checked(channels * sizeof(short));
        int byteRate = checked(sampleRate * blockAlign);

        WriteFourCc(writer, "RIFF");
        writer.Write(0U);
        WriteFourCc(writer, "WAVE");
        WriteFourCc(writer, "fmt ");
        writer.Write(16U);
        writer.Write((ushort)1);
        writer.Write((ushort)channels);
        writer.Write((uint)sampleRate);
        writer.Write((uint)byteRate);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)16);
        WriteFourCc(writer, "data");
        writer.Write(0U);
    }

    private static string ReadFourCc(BinaryReader reader) {
        byte[] bytes = reader.ReadBytes(4);
        if (bytes.Length != 4)
            throw new EndOfStreamException();
        return global::System.Text.Encoding.ASCII.GetString(bytes);
    }

    private static void WriteFourCc(BinaryWriter writer, string value) {
        if (value.Length != 4)
            throw new ArgumentException("A FOURCC must contain four characters.", nameof(value));
        writer.Write(global::System.Text.Encoding.ASCII.GetBytes(value));
    }
}

public static partial class TestUtils {
    public const int MUNGE_CODEC_NONE = (int)MungeCodec.None;
    public const int MUNGE_CODEC_ALAW = (int)MungeCodec.ALaw;
    public const int MUNGE_CODEC_ULAW = (int)MungeCodec.ULaw;
    public const int MUNGE_CODEC_G726_40K = (int)MungeCodec.G72640K;
    public const int MUNGE_CODEC_G726_32K = (int)MungeCodec.G72632K;
    public const int MUNGE_CODEC_G726_24K = (int)MungeCodec.G72624K;
    public const int MUNGE_CODEC_G726_16K = (int)MungeCodec.G72616K;
}

public static partial class TestUtils {
    private const int MAX_FFT_LEN = 8192;
    private const int SF_MAX_HANDLE = 32;
    private const int SAMPLE_RATE = 8000;
    private const double HILBERT_GAIN = 1.569546344;

    private static readonly float[] HilbertCoefficients = {
        +0.0012698413f, +0.0013489483f,
        +0.0015105196f, +0.0017620440f,
        +0.0021112899f, +0.0025663788f,
        +0.0031358856f, +0.0038289705f,
        +0.0046555545f, +0.0056265487f,
        +0.0067541562f, +0.0080522707f,
        +0.0095370033f, +0.0112273888f,
        +0.0131463382f, +0.0153219442f,
        +0.0177892941f, +0.0205930381f,
        +0.0237910974f, +0.0274601544f,
        +0.0317040029f, +0.0366666667f,
        +0.0425537942f, +0.0496691462f,
        +0.0584802574f, +0.0697446887f,
        +0.0847739823f, +0.1060495199f,
        +0.1388940865f, +0.1971551103f,
        +0.3316207267f, +0.9994281838f
    };

    private static readonly ComplexDouble[] Circle = new ComplexDouble[MAX_FFT_LEN / 2];
    private static readonly ComplexDouble[] InverseCircle = new ComplexDouble[MAX_FFT_LEN / 2];
    private static readonly Pcm16WaveFile?[] CloseAtExitList = new Pcm16WaveFile?[SF_MAX_HANDLE];
    private static bool circleInitialized;
    private static bool inverseCircleInitialized;
    private static bool closeAtExitRegistered;

    public static ComplexifyState complexify_init() => new();

    public static int complexify_free(ComplexifyState? state) {
        if (state is not null) {
            Array.Clear(state.History);
            state.Pointer = 0;
        }
        return 0;
    }

    public static ComplexFloat complexify(ComplexifyState state, short amplitude) {
        ArgumentNullException.ThrowIfNull(state);

        state.History[state.Pointer] = amplitude;

        int realIndex = state.Pointer - 63;
        if (realIndex < 0)
            realIndex += 128;
        float real = state.History[realIndex];

        float imaginary = 0.0f;
        int forwardIndex = state.Pointer - 126;
        if (forwardIndex < 0)
            forwardIndex += 128;

        int reverseIndex = state.Pointer;
        for (int i = 0; i < 32; i++) {
            imaginary +=
                (state.History[reverseIndex] - state.History[forwardIndex]) *
                HilbertCoefficients[i];

            forwardIndex += 2;
            if (forwardIndex >= 128)
                forwardIndex -= 128;

            reverseIndex -= 2;
            if (reverseIndex < 0)
                reverseIndex += 128;
        }

        state.Pointer++;
        if (state.Pointer >= 128)
            state.Pointer = 0;

        return new ComplexFloat(real, (float)(imaginary / HILBERT_GAIN));
    }

    public static void fft(ComplexDouble[] data, int length) {
        ValidateFftArguments(data, length);
        if (!circleInitialized) {
            for (int i = 0; i < Circle.Length; i++) {
                double angle = -(2.0 * 3.1415926535 * i) / MAX_FFT_LEN;
                Circle[i] = ExpJ(angle);
            }
            circleInitialized = true;
        }

        ComplexDouble[] temporary = new ComplexDouble[MAX_FFT_LEN];
        Fftx(data, 0, temporary, 0, length);
    }

    public static void ifft(ComplexDouble[] data, int length) {
        ValidateFftArguments(data, length);
        if (!inverseCircleInitialized) {
            for (int i = 0; i < InverseCircle.Length; i++) {
                double angle = (2.0 * 3.1415926535 * i) / MAX_FFT_LEN;
                InverseCircle[i] = ExpJ(angle);
            }
            inverseCircleInitialized = true;
        }

        ComplexDouble[] temporary = new ComplexDouble[MAX_FFT_LEN];
        Ifftx(data, 0, temporary, 0, length);
    }

    public static CodecMungeState? codec_munge_init(int codec, int info) {
        CodecMungeState state = new() {
            MungingCodec = (MungeCodec)codec,
            Sequence = 0,
            RbsPattern = info
        };

        int bitRate = codec switch {
            MUNGE_CODEC_G726_40K => 40_000,
            MUNGE_CODEC_G726_32K => 32_000,
            MUNGE_CODEC_G726_24K => 24_000,
            MUNGE_CODEC_G726_16K => 16_000,
            _ => 0
        };

        if (bitRate != 0) {
            state.G726EncoderState = g726_init(
                null,
                bitRate,
                G726_ENCODING_LINEAR,
                G726_PACKING_NONE);
            state.G726DecoderState = g726_init(
                null,
                bitRate,
                G726_ENCODING_LINEAR,
                G726_PACKING_NONE);

            if (state.G726EncoderState is null || state.G726DecoderState is null) {
                state.Dispose();
                return null;
            }

            // The native helper uses the 32K selector for every initialized
            // G.726 state; the actual rate remains stored in each g726_state_t.
            state.MungingCodec = MungeCodec.G72632K;
        }

        return state;
    }

    public static int codec_munge_free(CodecMungeState? state) {
        state?.Dispose();
        return 0;
    }

    public static void codec_munge(CodecMungeState state, short[] amplitudes, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);
        if ((uint)length > (uint)amplitudes.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        switch (state.MungingCodec) {
            case MungeCodec.None:
                break;

            case MungeCodec.ALaw:
                for (int i = 0; i < length; i++) {
                    byte law = linear_to_alaw(amplitudes[i]);
                    amplitudes[i] = alaw_to_linear(law);
                }
                break;

            case MungeCodec.ULaw:
                for (int i = 0; i < length; i++) {
                    byte law = linear_to_ulaw(amplitudes[i]);
                    if ((state.RbsPattern & (1 << state.Sequence)) != 0)
                        law &= 0xFE;
                    amplitudes[i] = ulaw_to_linear(law);
                }
                break;

            case MungeCodec.G72632K:
                if (state.G726EncoderState is null || state.G726DecoderState is null)
                    throw new InvalidOperationException("The G.726 states are not initialized.");

                byte[] adpcmData = new byte[160];
                short[] inputChunk = new short[160];
                short[] outputChunk = new short[160];
                for (int offset = 0; offset < length;) {
                    int chunkLength = Math.Min(160, length - offset);
                    Array.Copy(amplitudes, offset, inputChunk, 0, chunkLength);
                    Array.Clear(adpcmData);
                    Array.Clear(outputChunk);

                    int adpcmBytes = g726_encode(
                        state.G726EncoderState,
                        adpcmData,
                        inputChunk,
                        chunkLength);
                    int decodedSamples = g726_decode(
                        state.G726DecoderState,
                        outputChunk,
                        adpcmData,
                        adpcmBytes);
                    if (decodedSamples < chunkLength)
                        throw new InvalidDataException("The G.726 decoder returned too few samples.");

                    Array.Copy(outputChunk, 0, amplitudes, offset, chunkLength);
                    offset += chunkLength;
                }
                break;
        }
    }

    public static Pcm16WaveFile sf_open_telephony_read(string name, int channels) {
        try {
            Pcm16WaveFile handle = Pcm16WaveFile.OpenRead(name);
            if (handle.SampleRate != SAMPLE_RATE) {
                handle.Dispose();
                Console.WriteLine($"    Unexpected sample rate in audio file '{name}'");
                throw new TestUtilityExitException(2);
            }
            if (handle.Channels != channels) {
                handle.Dispose();
                Console.WriteLine($"    Unexpected number of channels in audio file '{name}'");
                throw new TestUtilityExitException(2);
            }
            RecordHandle(handle);
            return handle;
        } catch (TestUtilityExitException) {
            throw;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException) {
            Console.Error.WriteLine($"    Cannot open audio file '{name}' for reading: {exception.Message}");
            throw new TestUtilityExitException(2);
        }
    }

    public static Pcm16WaveFile sf_open_telephony_write(string name, int channels) {
        try {
            Pcm16WaveFile handle = Pcm16WaveFile.OpenWrite(name, SAMPLE_RATE, channels);
            RecordHandle(handle);
            return handle;
        } catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException) {
            Console.Error.WriteLine($"    Cannot open audio file '{name}' for writing: {exception.Message}");
            throw new TestUtilityExitException(2);
        }
    }

    public static int sf_close_telephony(Pcm16WaveFile handle) {
        ArgumentNullException.ThrowIfNull(handle);
        try {
            handle.Dispose();
            for (int i = 0; i < CloseAtExitList.Length; i++) {
                if (!ReferenceEquals(CloseAtExitList[i], handle))
                    continue;
                CloseAtExitList[i] = null;
                break;
            }
            return 0;
        } catch (IOException) {
            return -1;
        }
    }

    private static ComplexDouble ExpJ(double theta) =>
        new(Math.Cos(theta), Math.Sin(theta));

    private static ComplexDouble Add(in ComplexDouble left, in ComplexDouble right) =>
        new(left.Re + right.Re, left.Im + right.Im);

    private static ComplexDouble Subtract(in ComplexDouble left, in ComplexDouble right) =>
        new(left.Re - right.Re, left.Im - right.Im);

    private static ComplexDouble Multiply(in ComplexDouble left, in ComplexDouble right) =>
        new(
            left.Re * right.Re - left.Im * right.Im,
            left.Re * right.Im + left.Im * right.Re);

    private static void Fftx(
        ComplexDouble[] data,
        int dataOffset,
        ComplexDouble[] temporary,
        int temporaryOffset,
        int length) {
        if (length <= 1)
            return;

        int half = length / 2;
        for (int i = 0; i < half; i++) {
            int source = dataOffset + i * 2;
            temporary[temporaryOffset + i] = data[source];
            temporary[temporaryOffset + half + i] = data[source + 1];
        }

        Fftx(temporary, temporaryOffset, data, dataOffset, half);
        Fftx(temporary, temporaryOffset + half, data, dataOffset + half, half);

        int twiddle = 0;
        int twiddleStep = MAX_FFT_LEN / length;
        for (int i = 0; i < half; i++) {
            ComplexDouble product = Multiply(
                Circle[twiddle],
                temporary[temporaryOffset + half + i]);
            data[dataOffset + i] = Add(temporary[temporaryOffset + i], product);
            data[dataOffset + half + i] = Subtract(temporary[temporaryOffset + i], product);
            twiddle += twiddleStep;
        }
    }

    private static void Ifftx(
        ComplexDouble[] data,
        int dataOffset,
        ComplexDouble[] temporary,
        int temporaryOffset,
        int length) {
        if (length <= 1)
            return;

        int half = length / 2;
        for (int i = 0; i < half; i++) {
            int source = dataOffset + i * 2;
            temporary[temporaryOffset + i] = data[source];
            temporary[temporaryOffset + half + i] = data[source + 1];
        }

        // This deliberately mirrors the supplied C implementation, which calls
        // fftx for the two recursive halves before applying inverse twiddles.
        Fftx(temporary, temporaryOffset, data, dataOffset, half);
        Fftx(temporary, temporaryOffset + half, data, dataOffset + half, half);

        int twiddle = 0;
        int twiddleStep = MAX_FFT_LEN / length;
        for (int i = 0; i < half; i++) {
            ComplexDouble product = Multiply(
                InverseCircle[twiddle],
                temporary[temporaryOffset + half + i]);
            data[dataOffset + i] = Add(temporary[temporaryOffset + i], product);
            data[dataOffset + half + i] = Subtract(temporary[temporaryOffset + i], product);
            twiddle += twiddleStep;
        }
    }

    private static void ValidateFftArguments(ComplexDouble[] data, int length) {
        ArgumentNullException.ThrowIfNull(data);
        if (length <= 0 || length > MAX_FFT_LEN || length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));
        if ((length & (length - 1)) != 0)
            throw new ArgumentException("The FFT length must be a power of two.", nameof(length));
    }

    private static void RecordHandle(Pcm16WaveFile handle) {
        for (int i = 0; i < CloseAtExitList.Length; i++) {
            if (CloseAtExitList[i] is not null)
                continue;
            CloseAtExitList[i] = handle;
            RegisterCloseAtExit();
            return;
        }

        handle.Dispose();
        throw new InvalidOperationException("The telephony WAVE handle table is full.");
    }

    private static void RegisterCloseAtExit() {
        if (closeAtExitRegistered)
            return;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseAtExit();
        closeAtExitRegistered = true;
    }

    private static void CloseAtExit() {
        for (int i = 0; i < CloseAtExitList.Length; i++) {
            CloseAtExitList[i]?.Dispose();
            CloseAtExitList[i] = null;
        }
    }
}
