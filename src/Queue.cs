/*
 * TKFaxEngine - managed C# port
 *
 * Queue.cs
 *
 * Combined port of:
 *   queue.h
 *   private/queue.h
 *   queue.c
 *
 * Simple in-process byte-stream and message queue.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2004 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using System.Buffers.Binary;

namespace TKFaxEngine;

[Flags]
public enum QueueFlags {
    None = 0,

    /// <summary>
    /// A read succeeds only when the complete requested byte count is
    /// available.
    /// </summary>
    ReadAtomic = 0x0001,

    /// <summary>
    /// A write succeeds only when the complete requested byte count fits.
    /// </summary>
    WriteAtomic = 0x0002
}

/// <summary>
/// Lock-free single-producer/single-consumer byte ring buffer corresponding
/// to <c>queue_state_t</c>.
/// </summary>
/// <remarks>
/// One thread may write while one other thread reads without locking. Multiple
/// simultaneous writers or multiple simultaneous readers are not supported,
/// matching the native implementation.
/// </remarks>
public sealed class QueueState : IDisposable {
    private byte[] _data = Array.Empty<byte>();

    // One slot is deliberately unused so input == output means empty.
    private int _inputPointer;
    private int _outputPointer;
    private bool _disposed;

    public QueueState(
        int capacity,
        QueueFlags flags = QueueFlags.None) {
        Initialize(capacity, flags);
    }

    internal QueueState() {
    }

    /// <summary>
    /// Queue behavior flags.
    /// </summary>
    public QueueFlags Flags { get; private set; }

    /// <summary>
    /// Usable queue capacity in bytes.
    /// </summary>
    public int Capacity =>
        _data.Length == 0
            ? 0
            : _data.Length - 1;

    /// <summary>
    /// Internal ring-buffer length, including the reserved empty slot.
    /// </summary>
    public int InternalLength => _data.Length;

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Initializes or resets the queue.
    /// </summary>
    public void Initialize(
        int capacity,
        QueueFlags flags = QueueFlags.None) {
        if (capacity < 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        int internalLength = checked(capacity + 1);

        _data = new byte[internalLength];
        Volatile.Write(ref _inputPointer, 0);
        Volatile.Write(ref _outputPointer, 0);

        Flags = flags;
        _disposed = false;
    }

    public bool Empty() {
        ThrowIfDisposed();

        return Volatile.Read(ref _inputPointer) ==
               Volatile.Read(ref _outputPointer);
    }

    public int FreeSpace() {
        ThrowIfDisposed();

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int length =
            outputPointer -
            inputPointer -
            1;

        if (length < 0)
            length += _data.Length;

        return length;
    }

    public int Contents() {
        ThrowIfDisposed();

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int length =
            inputPointer -
            outputPointer;

        if (length < 0)
            length += _data.Length;

        return length;
    }

    /// <summary>
    /// Discards all currently queued bytes.
    /// </summary>
    public void Flush() {
        ThrowIfDisposed();

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        Volatile.Write(
            ref _outputPointer,
            inputPointer);
    }

    /// <summary>
    /// Copies bytes without removing them.
    /// </summary>
    /// <param name="destination">
    /// Destination buffer. An empty span may be supplied to query the result
    /// length without copying.
    /// </param>
    /// <param name="length">Requested byte count.</param>
    /// <returns>
    /// Number of bytes copied or available, zero when empty, or -1 when
    /// ReadAtomic is enabled and fewer than the requested bytes are present.
    /// </returns>
    public int View(
        Span<byte> destination,
        int length) {
        ThrowIfDisposed();
        ValidateRequestedLength(destination, length);

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int realLength =
            inputPointer -
            outputPointer;

        if (realLength < 0)
            realLength += _data.Length;

        if (realLength < length) {
            if ((Flags & QueueFlags.ReadAtomic) != 0)
                return -1;
        } else {
            realLength = length;
        }

        if (realLength == 0)
            return 0;

        CopyFromRing(
            destination,
            outputPointer,
            inputPointer,
            realLength);

        return realLength;
    }

    /// <summary>
    /// Reads and removes bytes from the queue.
    /// </summary>
    /// <param name="destination">
    /// Destination buffer. An empty span discards the requested bytes.
    /// </param>
    /// <param name="length">Requested byte count.</param>
    public int Read(
        Span<byte> destination,
        int length) {
        ThrowIfDisposed();
        ValidateRequestedLength(destination, length);

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int realLength =
            inputPointer -
            outputPointer;

        if (realLength < 0)
            realLength += _data.Length;

        if (realLength < length) {
            if ((Flags & QueueFlags.ReadAtomic) != 0)
                return -1;
        } else {
            realLength = length;
        }

        if (realLength == 0)
            return 0;

        CopyFromRing(
            destination,
            outputPointer,
            inputPointer,
            realLength);

        int newOutputPointer =
            outputPointer + realLength;

        if (newOutputPointer >= _data.Length)
            newOutputPointer -= _data.Length;

        // Publish the consumed region only after all copying is complete.
        Volatile.Write(
            ref _outputPointer,
            newOutputPointer);

        return realLength;
    }

    /// <summary>
    /// Reads and removes one byte, or returns -1 when empty.
    /// </summary>
    public int ReadByte() {
        ThrowIfDisposed();

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int realLength =
            inputPointer -
            outputPointer;

        if (realLength < 0)
            realLength += _data.Length;

        if (realLength < 1)
            return -1;

        int value = _data[outputPointer];

        outputPointer++;

        if (outputPointer >= _data.Length)
            outputPointer = 0;

        Volatile.Write(
            ref _outputPointer,
            outputPointer);

        return value;
    }

    /// <summary>
    /// Writes bytes into the queue.
    /// </summary>
    /// <returns>
    /// Number of bytes written, zero when full, or -1 when WriteAtomic is
    /// enabled and the complete request does not fit.
    /// </returns>
    public int Write(
        ReadOnlySpan<byte> source,
        int length) {
        ThrowIfDisposed();

        if (length < 0 || length > source.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int realLength =
            outputPointer -
            inputPointer -
            1;

        if (realLength < 0)
            realLength += _data.Length;

        if (realLength < length) {
            if ((Flags & QueueFlags.WriteAtomic) != 0)
                return -1;
        } else {
            realLength = length;
        }

        if (realLength == 0)
            return 0;

        int toEnd =
            _data.Length -
            inputPointer;

        int firstPart =
            Math.Min(realLength, toEnd);

        source[..firstPart].CopyTo(
            _data.AsSpan(
                inputPointer,
                firstPart));

        int secondPart =
            realLength -
            firstPart;

        if (secondPart > 0) {
            source.Slice(
                    firstPart,
                    secondPart)
                .CopyTo(
                    _data.AsSpan(
                        0,
                        secondPart));
        }

        int newInputPointer =
            inputPointer +
            realLength;

        if (newInputPointer >= _data.Length)
            newInputPointer -= _data.Length;

        // Publish new data only after the bytes have been copied.
        Volatile.Write(
            ref _inputPointer,
            newInputPointer);

        return realLength;
    }

    /// <summary>
    /// Writes one byte.
    /// </summary>
    public int WriteByte(byte value) {
        ThrowIfDisposed();

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int realLength =
            outputPointer -
            inputPointer -
            1;

        if (realLength < 0)
            realLength += _data.Length;

        if (realLength < 1) {
            return (Flags & QueueFlags.WriteAtomic) != 0
                ? -1
                : 0;
        }

        _data[inputPointer] = value;

        inputPointer++;

        if (inputPointer >= _data.Length)
            inputPointer = 0;

        Volatile.Write(
            ref _inputPointer,
            inputPointer);

        return 1;
    }

    /// <summary>
    /// Returns the payload length of the next complete message, or -1 when
    /// no complete two-byte message header is available.
    /// </summary>
    public int TestMessage() {
        ThrowIfDisposed();

        Span<byte> lengthBytes =
            stackalloc byte[sizeof(ushort)];

        int result =
            View(
                lengthBytes,
                lengthBytes.Length);

        if (result != lengthBytes.Length)
            return -1;

        return ReadUInt16Native(lengthBytes);
    }

    /// <summary>
    /// Reads one message. When the destination is too short, the leading part
    /// is returned and the remainder of that message is discarded.
    /// </summary>
    public int ReadMessage(
        Span<byte> destination,
        int length) {
        ThrowIfDisposed();
        ValidateRequestedLength(destination, length);

        Span<byte> lengthBytes =
            stackalloc byte[sizeof(ushort)];

        if (Read(
                lengthBytes,
                lengthBytes.Length) !=
            lengthBytes.Length) {
            return -1;
        }

        int messageLength =
            ReadUInt16Native(lengthBytes);

        if (messageLength == 0)
            return 0;

        if (messageLength > length) {
            int readLength =
                Read(destination, length);

            if (readLength < 0)
                return readLength;

            int remaining =
                messageLength -
                readLength;

            if (remaining > 0) {
                int discarded =
                    Read(
                        Span<byte>.Empty,
                        remaining);

                if (discarded < 0)
                    return discarded;
            }

            return readLength;
        }

        return Read(
            destination,
            messageLength);
    }

    /// <summary>
    /// Writes one length-prefixed message atomically.
    /// The two-byte length uses the host-endian representation of the native
    /// C implementation.
    /// </summary>
    public int WriteMessage(
        ReadOnlySpan<byte> source,
        int length) {
        ThrowIfDisposed();

        if (length < 0 || length > source.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (length > ushort.MaxValue) {
            throw new ArgumentOutOfRangeException(
                nameof(length),
                length,
                "A queue message length is stored as uint16.");
        }

        int required =
            checked(length + sizeof(ushort));

        int inputPointer =
            Volatile.Read(ref _inputPointer);

        int outputPointer =
            Volatile.Read(ref _outputPointer);

        int freeLength =
            outputPointer -
            inputPointer -
            1;

        if (freeLength < 0)
            freeLength += _data.Length;

        if (freeLength < required)
            return -1;

        Span<byte> lengthBytes =
            stackalloc byte[sizeof(ushort)];

        WriteUInt16Native(
            lengthBytes,
            checked((ushort)length));

        WriteToRingWithoutPublishing(
            inputPointer,
            lengthBytes);

        int payloadPointer =
            inputPointer +
            lengthBytes.Length;

        if (payloadPointer >= _data.Length)
            payloadPointer -= _data.Length;

        WriteToRingWithoutPublishing(
            payloadPointer,
            source[..length]);

        int newInputPointer =
            inputPointer +
            required;

        if (newInputPointer >= _data.Length)
            newInputPointer %= _data.Length;

        Volatile.Write(
            ref _inputPointer,
            newInputPointer);

        return length;
    }

    /// <summary>
    /// Matches <c>queue_release()</c>. The native implementation has no
    /// release work.
    /// </summary>
    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        Array.Clear(_data);
        _data = Array.Empty<byte>();

        Volatile.Write(ref _inputPointer, 0);
        Volatile.Write(ref _outputPointer, 0);

        Flags = QueueFlags.None;
        _disposed = true;
    }

    private void CopyFromRing(
        Span<byte> destination,
        int outputPointer,
        int inputPointer,
        int realLength) {
        if (destination.IsEmpty)
            return;

        int toEnd =
            _data.Length -
            outputPointer;

        if (inputPointer < outputPointer &&
            toEnd < realLength) {
            _data.AsSpan(
                    outputPointer,
                    toEnd)
                .CopyTo(
                    destination[..toEnd]);

            _data.AsSpan(
                    0,
                    realLength - toEnd)
                .CopyTo(
                    destination[toEnd..realLength]);

            return;
        }

        _data.AsSpan(
                outputPointer,
                realLength)
            .CopyTo(
                destination[..realLength]);
    }

    private void WriteToRingWithoutPublishing(
        int start,
        ReadOnlySpan<byte> source) {
        if (source.IsEmpty)
            return;

        int toEnd =
            _data.Length -
            start;

        int firstPart =
            Math.Min(
                source.Length,
                toEnd);

        source[..firstPart].CopyTo(
            _data.AsSpan(
                start,
                firstPart));

        int secondPart =
            source.Length -
            firstPart;

        if (secondPart > 0) {
            source[firstPart..].CopyTo(
                _data.AsSpan(
                    0,
                    secondPart));
        }
    }

    private static ushort ReadUInt16Native(
        ReadOnlySpan<byte> source) {
        return BitConverter.IsLittleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(source)
            : BinaryPrimitives.ReadUInt16BigEndian(source);
    }

    private static void WriteUInt16Native(
        Span<byte> destination,
        ushort value) {
        if (BitConverter.IsLittleEndian) {
            BinaryPrimitives.WriteUInt16LittleEndian(
                destination,
                value);
        } else {
            BinaryPrimitives.WriteUInt16BigEndian(
                destination,
                value);
        }
    }

    private static void ValidateRequestedLength(
        Span<byte> destination,
        int length) {
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (!destination.IsEmpty &&
            length > destination.Length) {
            throw new ArgumentException(
                "The destination buffer is shorter than the requested length.",
                nameof(destination));
        }
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(QueueState));
        }
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class QueueApi {
    public const int QUEUE_READ_ATOMIC =
        (int)QueueFlags.ReadAtomic;

    public const int QUEUE_WRITE_ATOMIC =
        (int)QueueFlags.WriteAtomic;

    public static bool queue_empty(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Empty();
    }

    public static int queue_free_space(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.FreeSpace();
    }

    public static int queue_contents(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Contents();
    }

    public static void queue_flush(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Flush();
    }

    public static int queue_view(
        QueueState state,
        Span<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.View(buffer, length);
    }

    public static int queue_read(
        QueueState state,
        Span<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Read(buffer, length);
    }

    /// <summary>
    /// Compatibility overload for native calls that pass a null destination
    /// to discard bytes.
    /// </summary>
    public static int queue_read(
        QueueState state,
        int length) {
        ArgumentNullException.ThrowIfNull(state);

        return state.Read(
            Span<byte>.Empty,
            length);
    }

    public static int queue_read_byte(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.ReadByte();
    }

    public static int queue_write(
        QueueState state,
        ReadOnlySpan<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Write(buffer, length);
    }

    public static int queue_write_byte(
        QueueState state,
        byte value) {
        ArgumentNullException.ThrowIfNull(state);
        return state.WriteByte(value);
    }

    public static int queue_state_test_msg(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.TestMessage();
    }

    public static int queue_read_msg(
        QueueState state,
        Span<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);

        return state.ReadMessage(
            buffer,
            length);
    }

    public static int queue_write_msg(
        QueueState state,
        ReadOnlySpan<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);

        return state.WriteMessage(
            buffer,
            length);
    }

    public static QueueState queue_init(
        QueueState? state,
        int length,
        int flags) {
        state ??= new QueueState();

        state.Initialize(
            length,
            (QueueFlags)flags);

        return state;
    }

    public static int queue_release(
        QueueState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int queue_free(
        QueueState? state) {
        if (state is null)
            return 0;

        int result = state.Release();
        state.Dispose();
        return result;
    }
}
