/*
 * TKFaxEngine - managed C# port of sim_support.h and the concrete simulator
 * services required by the converted sig/socket test sources.
 *
 * The native aggregation header pulls in test utilities, line simulation and
 * pseudo-terminal support. C# has no textual include mechanism, so the shared
 * callback contracts, codec munger, PCM wave I/O and full-duplex pseudo
 * terminal used by this source group are collected here.
 */

#nullable enable

using System.Buffers.Binary;
using System.Text;
using TKFaxEngine.Audio;

namespace TKFaxEngineTest.System;

public enum CodecMungeMode {
    ALaw,
    MuLaw
}

public sealed class SimCodecMungeState : IDisposable {
    private bool _disposed;

    public SimCodecMungeState(CodecMungeMode mode, int robbedBitPattern = 0) {
        if ((uint)robbedBitPattern > 0xFFU)
            throw new ArgumentOutOfRangeException(nameof(robbedBitPattern));
        Mode = mode;
        RobbedBitPattern = robbedBitPattern;
    }

    public CodecMungeMode Mode { get; }
    public int RobbedBitPattern { get; }

    public int Process(Span<short> samples) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        for (int i = 0; i < samples.Length; i++) {
            byte encoded = Mode == CodecMungeMode.ALaw
                ? g711.linear_to_alaw(samples[i])
                : g711.linear_to_ulaw(samples[i]);

            if (RobbedBitPattern != 0)
                encoded = unchecked((byte)(encoded & ~RobbedBitPattern));

            samples[i] = Mode == CodecMungeMode.ALaw
                ? g711.alaw_to_linear(encoded)
                : g711.ulaw_to_linear(encoded);
        }
        return samples.Length;
    }

    public void Dispose() {
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public sealed class Pcm16WaveReader : IDisposable {
    private readonly FileStream _stream;
    private readonly BinaryReader _reader;
    private readonly long _dataEnd;
    private bool _disposed;

    public Pcm16WaveReader(string path, int expectedSampleRate, int expectedChannels) {
        _stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        _reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);

        if (ReadFourCc() != "RIFF")
            throw new InvalidDataException("The file is not a RIFF wave file.");
        _ = _reader.ReadUInt32();
        if (ReadFourCc() != "WAVE")
            throw new InvalidDataException("The RIFF form is not WAVE.");

        bool formatFound = false;
        bool dataFound = false;
        long dataStart = 0;
        uint dataLength = 0;
        ushort channels = 0;
        uint sampleRate = 0;
        ushort bitsPerSample = 0;
        ushort formatTag = 0;

        while (_stream.Position + 8 <= _stream.Length) {
            string id = ReadFourCc();
            uint length = _reader.ReadUInt32();
            long chunkStart = _stream.Position;
            long next = checked(chunkStart + length + (length & 1U));
            if (next > _stream.Length)
                throw new InvalidDataException("A WAVE chunk extends beyond the end of the file.");

            if (id == "fmt ") {
                if (length < 16)
                    throw new InvalidDataException("The WAVE format chunk is too short.");
                formatTag = _reader.ReadUInt16();
                channels = _reader.ReadUInt16();
                sampleRate = _reader.ReadUInt32();
                _ = _reader.ReadUInt32();
                _ = _reader.ReadUInt16();
                bitsPerSample = _reader.ReadUInt16();
                formatFound = true;
            } else if (id == "data") {
                dataStart = chunkStart;
                dataLength = length;
                dataFound = true;
            }

            _stream.Position = next;
            if (formatFound && dataFound)
                break;
        }

        if (!formatFound || !dataFound)
            throw new InvalidDataException("The WAVE file has no usable format/data chunks.");
        if (formatTag != 1 || bitsPerSample != 16)
            throw new InvalidDataException("Only signed 16-bit PCM WAVE input is supported.");
        if (channels != expectedChannels || sampleRate != expectedSampleRate)
            throw new InvalidDataException($"Expected {expectedChannels} channel(s) at {expectedSampleRate} Hz, got {channels} channel(s) at {sampleRate} Hz.");

        Channels = channels;
        SampleRate = checked((int)sampleRate);
        _stream.Position = dataStart;
        _dataEnd = checked(dataStart + dataLength);
    }

    public int Channels { get; }
    public int SampleRate { get; }

    public int ReadFrames(short[] destination, int frameCapacity) {
        ArgumentNullException.ThrowIfNull(destination);
        if (frameCapacity < 0 || checked(frameCapacity * Channels) > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(frameCapacity));

        int sampleCapacity = frameCapacity * Channels;
        int availableSamples = checked((int)Math.Min(sampleCapacity, (_dataEnd - _stream.Position) / 2));
        int read = 0;
        while (read < availableSamples) {
            destination[read] = _reader.ReadInt16();
            read++;
        }
        return read / Channels;
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;
        _reader.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    private string ReadFourCc() => Encoding.ASCII.GetString(_reader.ReadBytes(4));
}

public sealed class Pcm16WaveWriter : IDisposable {
    private readonly FileStream _stream;
    private readonly BinaryWriter _writer;
    private readonly long _riffLengthPosition;
    private readonly long _dataLengthPosition;
    private long _dataBytes;
    private bool _disposed;

    public Pcm16WaveWriter(string path, int sampleRate, int channels) {
        if (sampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0 || channels > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(channels));

        SampleRate = sampleRate;
        Channels = channels;
        _stream = File.Open(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        _writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);

        WriteFourCc("RIFF");
        _riffLengthPosition = _stream.Position;
        _writer.Write(0U);
        WriteFourCc("WAVE");
        WriteFourCc("fmt ");
        _writer.Write(16U);
        _writer.Write((ushort)1);
        _writer.Write((ushort)channels);
        _writer.Write((uint)sampleRate);
        _writer.Write((uint)(sampleRate * channels * sizeof(short)));
        _writer.Write((ushort)(channels * sizeof(short)));
        _writer.Write((ushort)16);
        WriteFourCc("data");
        _dataLengthPosition = _stream.Position;
        _writer.Write(0U);
    }

    public int SampleRate { get; }
    public int Channels { get; }

    public int WriteFrames(ReadOnlySpan<short> interleavedSamples, int frames) {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int sampleCount = checked(frames * Channels);
        if (frames < 0 || sampleCount > interleavedSamples.Length)
            throw new ArgumentOutOfRangeException(nameof(frames));

        Span<byte> bytes = stackalloc byte[2];
        for (int i = 0; i < sampleCount; i++) {
            BinaryPrimitives.WriteInt16LittleEndian(bytes, interleavedSamples[i]);
            _writer.Write(bytes);
        }
        _dataBytes = checked(_dataBytes + sampleCount * 2L);
        return frames;
    }

    public void Dispose() {
        if (_disposed)
            return;
        _disposed = true;

        long end = _stream.Position;
        _stream.Position = _dataLengthPosition;
        _writer.Write(checked((uint)_dataBytes));
        _stream.Position = _riffLengthPosition;
        _writer.Write(checked((uint)(end - 8)));
        _stream.Position = end;
        _writer.Flush();
        _writer.Dispose();
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    private void WriteFourCc(string value) {
        if (value.Length != 4)
            throw new ArgumentException("A FourCC must contain exactly four characters.", nameof(value));
        _writer.Write(Encoding.ASCII.GetBytes(value));
    }
}
