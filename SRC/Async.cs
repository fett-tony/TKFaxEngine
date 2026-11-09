/*
 * TKFaxEngine - managed C# port
 *
 * Async.cs - combined port of async.h, private/async.h and async.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

namespace TKFaxEngine;

/// <summary>
/// Special values passed through bit and byte callbacks to report modem,
/// framing, and link state changes.
/// </summary>
public enum SignalStatus {
    CarrierDown = -1,
    CarrierUp = -2,
    TrainingInProgress = -3,
    TrainingSucceeded = -4,
    TrainingFailed = -5,
    FramingOk = -6,
    EndOfData = -7,
    Abort = -8,
    Break = -9,
    ShutdownComplete = -10,
    OctetReport = -11,
    PoorSignalQuality = -12,
    ModemRetrainOccurred = -13,
    LinkConnected = -14,
    LinkDisconnected = -15,
    LinkError = -16,
    LinkIdle = -17
}

/// <summary>
/// Parity modes supported by the asynchronous serial converter.
/// </summary>
public enum AsyncParity {
    None = 0,
    Even = 1,
    Odd = 2,
    Mark = 3,
    Space = 4
}

public delegate void SpanPutMessageDelegate(object? userData, ReadOnlySpan<byte> message);

public delegate int SpanGetMessageDelegate(object? userData, Span<byte> message);

public delegate void SpanPutByteDelegate(object? userData, int value);

public delegate int SpanGetByteDelegate(object? userData);

public delegate void SpanPutBitDelegate(object? userData, int bit);

public delegate int SpanGetBitDelegate(object? userData);

public delegate void SpanModemStatusDelegate(object? userData, int status);

/// <summary>
/// Converts a hard asynchronous serial bit stream into bytes or status values.
/// Managed equivalent of <c>async_rx_state_t</c>.
/// </summary>
public sealed class AsyncReceiver : IDisposable {
    private SpanPutByteDelegate? _putByte;
    private object? _userData;
    private ushort _frameInProgress;
    private short _bitPosition;
    private bool _disposed;

    public AsyncReceiver(
        int dataBits,
        AsyncParity parity,
        int stopBits,
        bool useV14,
        SpanPutByteDelegate putByte,
        object? userData = null) {
        Initialize(dataBits, parity, stopBits, useV14, putByte, userData);
    }

    internal AsyncReceiver() {
    }

    public short DataBits { get; private set; }

    public AsyncParity Parity { get; private set; }

    public short TotalDataBits { get; private set; }

    public bool UseV14 { get; private set; }

    public int ParityErrors { get; private set; }

    public int FramingErrors { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Reinitializes this receiver, matching <c>async_rx_init()</c>.
    /// The receive side intentionally does not store the stop-bit count because
    /// the original implementation accepts it only for API completeness.
    /// </summary>
    public void Initialize(
        int dataBits,
        AsyncParity parity,
        int stopBits,
        bool useV14,
        SpanPutByteDelegate putByte,
        object? userData = null) {
        ValidateDataBits(dataBits);
        ValidateParity(parity);
        ValidateStopBits(stopBits);
        ArgumentNullException.ThrowIfNull(putByte);

        DataBits = checked((short)dataBits);
        Parity = parity;
        TotalDataBits = checked((short)(dataBits + (parity == AsyncParity.None ? 0 : 1)));
        UseV14 = useV14;

        _putByte = putByte;
        _userData = userData;
        _frameInProgress = 0;
        _bitPosition = 0;

        ParityErrors = 0;
        FramingErrors = 0;
        _disposed = false;
    }

    /// <summary>
    /// Accepts one hard bit or one negative <see cref="SignalStatus"/> value.
    /// Managed equivalent of <c>async_rx_put_bit()</c>.
    /// </summary>
    public void PutBit(int bit) {
        ThrowIfDisposed();

        if (bit < 0) {
            switch ((SignalStatus)bit) {
                case SignalStatus.CarrierUp:
                case SignalStatus.CarrierDown:
                case SignalStatus.TrainingInProgress:
                case SignalStatus.TrainingSucceeded:
                case SignalStatus.TrainingFailed:
                case SignalStatus.EndOfData:
                    Emit(bit);
                    _bitPosition = 0;
                    _frameInProgress = 0;
                    break;
            }

            return;
        }

        if (bit is not 0 and not 1)
            throw new ArgumentOutOfRangeException(nameof(bit), bit, "A serial bit must be 0, 1, or a negative signal status.");

        if (_bitPosition == 0) {
            // Search for the start bit. A zero starts a frame; a one is idle.
            _bitPosition += checked((short)(bit ^ 1));
            _frameInProgress = 0;
            return;
        }

        if (_bitPosition <= TotalDataBits) {
            _frameInProgress = unchecked((ushort)((_frameInProgress >> 1) | (bit << 15)));
            _bitPosition++;
            return;
        }

        // We should now be at the first stop-bit position.
        if (bit == 0 && !UseV14) {
            FramingErrors++;
            _bitPosition = 0;
            return;
        }

        if (Parity != AsyncParity.None) {
            int receivedParity = (_frameInProgress >> 15) & 0x01;

            _frameInProgress &= 0x7FFF;
            _frameInProgress >>= 16 - TotalDataBits;

            int expectedParity = Parity switch {
                AsyncParity.Odd => Parity8((byte)_frameInProgress) ^ 1,
                AsyncParity.Even => Parity8((byte)_frameInProgress),
                AsyncParity.Mark => 1,
                AsyncParity.Space => 0,
                _ => 0
            };

            if (receivedParity == expectedParity)
                Emit(_frameInProgress);
            else
                ParityErrors++;
        } else {
            _frameInProgress >>= 16 - TotalDataBits;
            Emit(_frameInProgress);
        }

        if (bit == 1) {
            // First stop bit was present.
            _bitPosition = 0;
        } else {
            // V.14 may have removed the stop bit. Treat this zero as the start
            // bit of the following character.
            _bitPosition = 1;
            _frameInProgress = 0;
        }
    }

    public int GetParityErrors(bool reset) {
        ThrowIfDisposed();

        int result = ParityErrors;
        if (reset)
            ParityErrors = 0;

        return result;
    }

    public int GetFramingErrors(bool reset) {
        ThrowIfDisposed();

        int result = FramingErrors;
        if (reset)
            FramingErrors = 0;

        return result;
    }

    /// <summary>
    /// Matches <c>async_rx_release()</c>. No unmanaged resources are held.
    /// </summary>
    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _putByte = null;
        _userData = null;
        _frameInProgress = 0;
        _bitPosition = 0;
        _disposed = true;
    }

    private void Emit(int value) {
        SpanPutByteDelegate callback = _putByte
            ?? throw new InvalidOperationException("The asynchronous receiver has not been initialized.");

        callback(_userData, value);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static int Parity8(byte value) {
        // Exact equivalent of the original parity8(uint8_t) helper.
        value = (byte)((value ^ (value >> 4)) & 0x0F);
        return (0x6996 >> value) & 1;
    }

    private static void ValidateDataBits(int dataBits) {
        if (dataBits is < 5 or > 9)
            throw new ArgumentOutOfRangeException(nameof(dataBits), dataBits, "Supported character sizes are 5 through 9 data bits.");
    }

    private static void ValidateParity(AsyncParity parity) {
        if (!Enum.IsDefined(parity))
            throw new ArgumentOutOfRangeException(nameof(parity), parity, "Unknown asynchronous parity mode.");
    }

    private static void ValidateStopBits(int stopBits) {
        if (stopBits is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, "Supported stop-bit counts are 1 or 2.");
    }
}

/// <summary>
/// Converts bytes or status values into a hard asynchronous serial bit stream.
/// Managed equivalent of <c>async_tx_state_t</c>.
/// </summary>
public sealed class AsyncTransmitter : IDisposable {
    private SpanGetByteDelegate? _getByte;
    private object? _userData;
    private ushort _frameInProgress;
    private short _bitPosition;
    private bool _disposed;

    public AsyncTransmitter(
        int dataBits,
        AsyncParity parity,
        int stopBits,
        bool useV14,
        SpanGetByteDelegate getByte,
        object? userData = null) {
        Initialize(dataBits, parity, stopBits, useV14, getByte, userData);
    }

    internal AsyncTransmitter() {
    }

    public short DataBits { get; private set; }

    public AsyncParity Parity { get; private set; }

    public short TotalDataBits { get; private set; }

    public short TotalBits { get; private set; }

    public int PresendBits { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Reinitializes this transmitter, matching <c>async_tx_init()</c>.
    /// The original transmitter accepts <paramref name="useV14"/> for API
    /// compatibility but does not use it.
    /// </summary>
    public void Initialize(
        int dataBits,
        AsyncParity parity,
        int stopBits,
        bool useV14,
        SpanGetByteDelegate getByte,
        object? userData = null) {
        _ = useV14;

        ValidateDataBits(dataBits);
        ValidateParity(parity);
        ValidateStopBits(stopBits);
        ArgumentNullException.ThrowIfNull(getByte);

        DataBits = checked((short)dataBits);
        Parity = parity;
        TotalDataBits = checked((short)(dataBits + (parity == AsyncParity.None ? 0 : 1)));
        TotalBits = checked((short)(TotalDataBits + stopBits));

        _getByte = getByte;
        _userData = userData;
        _frameInProgress = 0;
        _bitPosition = 0;
        PresendBits = 0;
        _disposed = false;
    }

    /// <summary>
    /// Configures the number of idle one-bits sent before the next character.
    /// Managed equivalent of <c>async_tx_presend_bits()</c>.
    /// </summary>
    public void SetPresendBits(int bits) {
        ThrowIfDisposed();

        if (bits < 0)
            throw new ArgumentOutOfRangeException(nameof(bits), bits, "The presend bit count cannot be negative.");

        PresendBits = bits;
    }

    /// <summary>
    /// Returns the next serial bit, or passes through a negative status returned
    /// by the byte provider. Managed equivalent of <c>async_tx_get_bit()</c>.
    /// </summary>
    public int GetBit() {
        ThrowIfDisposed();

        if (_bitPosition == 0) {
            if (PresendBits > 0) {
                PresendBits--;
                return 1;
            }

            SpanGetByteDelegate callback = _getByte
                ?? throw new InvalidOperationException("The asynchronous transmitter has not been initialized.");

            int nextByte = callback(_userData);
            if (nextByte < 0) {
                if (nextByte != (int)SignalStatus.LinkIdle)
                    return nextByte;

                // Idle for one bit time.
                return 1;
            }

            _frameInProgress = unchecked((ushort)nextByte);

            // Trim upper bits outside the configured character width.
            _frameInProgress &= unchecked((ushort)(0xFFFF >> (16 - DataBits)));

            switch (Parity) {
                case AsyncParity.Mark:
                    _frameInProgress |= unchecked((ushort)(1 << DataBits));
                    break;

                case AsyncParity.Even: {
                        int parityBit = Parity8((byte)_frameInProgress);
                        _frameInProgress |= unchecked((ushort)(parityBit << DataBits));
                        break;
                    }

                case AsyncParity.Odd: {
                        int parityBit = Parity8((byte)_frameInProgress) ^ 1;
                        _frameInProgress |= unchecked((ushort)(parityBit << DataBits));
                        break;
                    }

                case AsyncParity.None:
                case AsyncParity.Space:
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported parity mode: {Parity}.");
            }

            // Fill all remaining high positions with stop-bit state.
            _frameInProgress |= unchecked((ushort)(0xFFFF << TotalDataBits));

            // Start bit.
            _bitPosition++;
            return 0;
        }

        int bit = _frameInProgress & 1;
        _frameInProgress >>= 1;

        if (++_bitPosition > TotalBits)
            _bitPosition = 0;

        return bit;
    }

    /// <summary>
    /// Matches <c>async_tx_release()</c>. No unmanaged resources are held.
    /// </summary>
    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _getByte = null;
        _userData = null;
        _frameInProgress = 0;
        _bitPosition = 0;
        PresendBits = 0;
        _disposed = true;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static int Parity8(byte value) {
        // Exact equivalent of the original parity8(uint8_t) helper.
        value = (byte)((value ^ (value >> 4)) & 0x0F);
        return (0x6996 >> value) & 1;
    }

    private static void ValidateDataBits(int dataBits) {
        if (dataBits is < 5 or > 9)
            throw new ArgumentOutOfRangeException(nameof(dataBits), dataBits, "Supported character sizes are 5 through 9 data bits.");
    }

    private static void ValidateParity(AsyncParity parity) {
        if (!Enum.IsDefined(parity))
            throw new ArgumentOutOfRangeException(nameof(parity), parity, "Unknown asynchronous parity mode.");
    }

    private static void ValidateStopBits(int stopBits) {
        if (stopBits is < 1 or > 2)
            throw new ArgumentOutOfRangeException(nameof(stopBits), stopBits, "Supported stop-bit counts are 1 or 2.");
    }
}

/// <summary>
/// C-compatible facade retaining the original TKFaxEngine function names.
/// </summary>
public static class AsyncApi {
    public static string signal_status_to_str(int status) {
        return status switch {
            (int)SignalStatus.CarrierDown => "Carrier down",
            (int)SignalStatus.CarrierUp => "Carrier up",
            (int)SignalStatus.TrainingInProgress => "Training in progress",
            (int)SignalStatus.TrainingSucceeded => "Training succeeded",
            (int)SignalStatus.TrainingFailed => "Training failed",
            (int)SignalStatus.FramingOk => "Framing OK",
            (int)SignalStatus.EndOfData => "End of data",
            (int)SignalStatus.Abort => "Abort",
            (int)SignalStatus.Break => "Break",
            (int)SignalStatus.ShutdownComplete => "Shutdown complete",
            (int)SignalStatus.OctetReport => "Octet report",
            (int)SignalStatus.PoorSignalQuality => "Poor signal quality",
            (int)SignalStatus.ModemRetrainOccurred => "Modem retrain occurred",
            (int)SignalStatus.LinkConnected => "Link connected",
            (int)SignalStatus.LinkDisconnected => "Link disconnected",
            (int)SignalStatus.LinkError => "Link error",
            (int)SignalStatus.LinkIdle => "Link idle",
            _ => "???"
        };
    }

    public static void async_rx_put_bit(object? userData, int bit) {
        ArgumentNullException.ThrowIfNull(userData);

        if (userData is not AsyncReceiver receiver)
            throw new ArgumentException("userData must reference an AsyncReceiver.", nameof(userData));

        receiver.PutBit(bit);
    }

    public static int async_rx_get_parity_errors(AsyncReceiver state, bool reset) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetParityErrors(reset);
    }

    public static int async_rx_get_framing_errors(AsyncReceiver state, bool reset) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetFramingErrors(reset);
    }

    public static AsyncReceiver async_rx_init(
        AsyncReceiver? state,
        int dataBits,
        int parity,
        int stopBits,
        bool useV14,
        SpanPutByteDelegate putByte,
        object? userData) {
        state ??= new AsyncReceiver();
        state.Initialize(dataBits, (AsyncParity)parity, stopBits, useV14, putByte, userData);
        return state;
    }

    public static int async_rx_release(AsyncReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int async_rx_free(AsyncReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }

    public static void async_tx_presend_bits(AsyncTransmitter state, int bits) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetPresendBits(bits);
    }

    public static int async_tx_get_bit(object? userData) {
        ArgumentNullException.ThrowIfNull(userData);

        if (userData is not AsyncTransmitter transmitter)
            throw new ArgumentException("userData must reference an AsyncTransmitter.", nameof(userData));

        return transmitter.GetBit();
    }

    public static AsyncTransmitter async_tx_init(
        AsyncTransmitter? state,
        int dataBits,
        int parity,
        int stopBits,
        bool useV14,
        SpanGetByteDelegate getByte,
        object? userData) {
        state ??= new AsyncTransmitter();
        state.Initialize(dataBits, (AsyncParity)parity, stopBits, useV14, getByte, userData);
        return state;
    }

    public static int async_tx_release(AsyncTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int async_tx_free(AsyncTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }
}
