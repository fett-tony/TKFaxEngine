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
    /// <summary>
    /// Represents the <c>CarrierDown</c> value.
    /// </summary>
    CarrierDown = -1,
    /// <summary>
    /// Represents the <c>CarrierUp</c> value.
    /// </summary>
    CarrierUp = -2,
    /// <summary>
    /// Represents the <c>TrainingInProgress</c> value.
    /// </summary>
    TrainingInProgress = -3,
    /// <summary>
    /// Represents the <c>TrainingSucceeded</c> value.
    /// </summary>
    TrainingSucceeded = -4,
    /// <summary>
    /// Represents the <c>TrainingFailed</c> value.
    /// </summary>
    TrainingFailed = -5,
    /// <summary>
    /// Represents the <c>FramingOk</c> value.
    /// </summary>
    FramingOk = -6,
    /// <summary>
    /// Represents the <c>EndOfData</c> value.
    /// </summary>
    EndOfData = -7,
    /// <summary>
    /// Represents the <c>Abort</c> value.
    /// </summary>
    Abort = -8,
    /// <summary>
    /// Represents the <c>Break</c> value.
    /// </summary>
    Break = -9,
    /// <summary>
    /// Represents the <c>ShutdownComplete</c> value.
    /// </summary>
    ShutdownComplete = -10,
    /// <summary>
    /// Represents the <c>OctetReport</c> value.
    /// </summary>
    OctetReport = -11,
    /// <summary>
    /// Represents the <c>PoorSignalQuality</c> value.
    /// </summary>
    PoorSignalQuality = -12,
    /// <summary>
    /// Represents the <c>ModemRetrainOccurred</c> value.
    /// </summary>
    ModemRetrainOccurred = -13,
    /// <summary>
    /// Represents the <c>LinkConnected</c> value.
    /// </summary>
    LinkConnected = -14,
    /// <summary>
    /// Represents the <c>LinkDisconnected</c> value.
    /// </summary>
    LinkDisconnected = -15,
    /// <summary>
    /// Represents the <c>LinkError</c> value.
    /// </summary>
    LinkError = -16,
    /// <summary>
    /// Represents the <c>LinkIdle</c> value.
    /// </summary>
    LinkIdle = -17
}

/// <summary>
/// Parity modes supported by the asynchronous serial converter.
/// </summary>
public enum AsyncParity {
    /// <summary>
    /// Represents the <c>None</c> value.
    /// </summary>
    None = 0,
    /// <summary>
    /// Represents the <c>Even</c> value.
    /// </summary>
    Even = 1,
    /// <summary>
    /// Represents the <c>Odd</c> value.
    /// </summary>
    Odd = 2,
    /// <summary>
    /// Represents the <c>Mark</c> value.
    /// </summary>
    Mark = 3,
    /// <summary>
    /// Represents the <c>Space</c> value.
    /// </summary>
    Space = 4
}

/// <summary>
/// Represents the <c>SpanPutMessageDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="message">The message value.</param>
public delegate void SpanPutMessageDelegate(object? userData, ReadOnlySpan<byte> message);

/// <summary>
/// Represents the <c>SpanGetMessageDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="message">The message value.</param>
/// <returns>The result of the operation.</returns>
public delegate int SpanGetMessageDelegate(object? userData, Span<byte> message);

/// <summary>
/// Represents the <c>SpanPutByteDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="value">The value value.</param>
public delegate void SpanPutByteDelegate(object? userData, int value);

/// <summary>
/// Represents the <c>SpanGetByteDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <returns>The result of the operation.</returns>
public delegate int SpanGetByteDelegate(object? userData);

/// <summary>
/// Represents the <c>SpanPutBitDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="bit">The bit value.</param>
public delegate void SpanPutBitDelegate(object? userData, int bit);

/// <summary>
/// Represents the <c>SpanGetBitDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <returns>The result of the operation.</returns>
public delegate int SpanGetBitDelegate(object? userData);

/// <summary>
/// Represents the <c>SpanModemStatusDelegate</c> callback.
/// </summary>
/// <param name="userData">The user data value.</param>
/// <param name="status">The status value.</param>
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

    /// <summary>
    /// Initializes a new instance of the <c>AsyncReceiver</c> type.
    /// </summary>
    /// <param name="dataBits">The data bits value.</param>
    /// <param name="parity">The parity value.</param>
    /// <param name="stopBits">The stop bits value.</param>
    /// <param name="useV14">The use v14 value.</param>
    /// <param name="putByte">The put byte value.</param>
    /// <param name="userData">The user data value.</param>
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

    /// <summary>
    /// Gets or sets the <c>DataBits</c> value.
    /// </summary>
    public short DataBits { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Parity</c> value.
    /// </summary>
    public AsyncParity Parity { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TotalDataBits</c> value.
    /// </summary>
    public short TotalDataBits { get; private set; }

    /// <summary>
    /// Gets or sets the <c>UseV14</c> value.
    /// </summary>
    public bool UseV14 { get; private set; }

    /// <summary>
    /// Gets or sets the <c>ParityErrors</c> value.
    /// </summary>
    public int ParityErrors { get; private set; }

    /// <summary>
    /// Gets or sets the <c>FramingErrors</c> value.
    /// </summary>
    public int FramingErrors { get; private set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
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

    /// <summary>
    /// Performs the <c>GetParityErrors</c> operation.
    /// </summary>
    /// <param name="reset">The reset value.</param>
    /// <returns>The result of the operation.</returns>
    public int GetParityErrors(bool reset) {
        ThrowIfDisposed();

        int result = ParityErrors;
        if (reset)
            ParityErrors = 0;

        return result;
    }

    /// <summary>
    /// Performs the <c>GetFramingErrors</c> operation.
    /// </summary>
    /// <param name="reset">The reset value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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

    /// <summary>
    /// Initializes a new instance of the <c>AsyncTransmitter</c> type.
    /// </summary>
    /// <param name="dataBits">The data bits value.</param>
    /// <param name="parity">The parity value.</param>
    /// <param name="stopBits">The stop bits value.</param>
    /// <param name="useV14">The use v14 value.</param>
    /// <param name="getByte">The get byte value.</param>
    /// <param name="userData">The user data value.</param>
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

    /// <summary>
    /// Gets or sets the <c>DataBits</c> value.
    /// </summary>
    public short DataBits { get; private set; }

    /// <summary>
    /// Gets or sets the <c>Parity</c> value.
    /// </summary>
    public AsyncParity Parity { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TotalDataBits</c> value.
    /// </summary>
    public short TotalDataBits { get; private set; }

    /// <summary>
    /// Gets or sets the <c>TotalBits</c> value.
    /// </summary>
    public short TotalBits { get; private set; }

    /// <summary>
    /// Gets or sets the <c>PresendBits</c> value.
    /// </summary>
    public int PresendBits { get; private set; }

    /// <summary>
    /// Gets the <c>IsDisposed</c> value.
    /// </summary>
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

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
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
    /// <summary>
    /// Performs the <c>signal_status_to_str</c> operation.
    /// </summary>
    /// <param name="status">The status value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>async_rx_put_bit</c> operation.
    /// </summary>
    /// <param name="userData">The user data value.</param>
    /// <param name="bit">The bit value.</param>
    public static void async_rx_put_bit(object? userData, int bit) {
        ArgumentNullException.ThrowIfNull(userData);

        if (userData is not AsyncReceiver receiver)
            throw new ArgumentException("userData must reference an AsyncReceiver.", nameof(userData));

        receiver.PutBit(bit);
    }

    /// <summary>
    /// Performs the <c>async_rx_get_parity_errors</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="reset">The reset value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_rx_get_parity_errors(AsyncReceiver state, bool reset) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetParityErrors(reset);
    }

    /// <summary>
    /// Performs the <c>async_rx_get_framing_errors</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="reset">The reset value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_rx_get_framing_errors(AsyncReceiver state, bool reset) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetFramingErrors(reset);
    }

    /// <summary>
    /// Performs the <c>async_rx_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="dataBits">The data bits value.</param>
    /// <param name="parity">The parity value.</param>
    /// <param name="stopBits">The stop bits value.</param>
    /// <param name="useV14">The use v14 value.</param>
    /// <param name="putByte">The put byte value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>async_rx_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_rx_release(AsyncReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>async_rx_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_rx_free(AsyncReceiver state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>async_tx_presend_bits</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="bits">The bits value.</param>
    public static void async_tx_presend_bits(AsyncTransmitter state, int bits) {
        ArgumentNullException.ThrowIfNull(state);
        state.SetPresendBits(bits);
    }

    /// <summary>
    /// Performs the <c>async_tx_get_bit</c> operation.
    /// </summary>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_tx_get_bit(object? userData) {
        ArgumentNullException.ThrowIfNull(userData);

        if (userData is not AsyncTransmitter transmitter)
            throw new ArgumentException("userData must reference an AsyncTransmitter.", nameof(userData));

        return transmitter.GetBit();
    }

    /// <summary>
    /// Performs the <c>async_tx_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="dataBits">The data bits value.</param>
    /// <param name="parity">The parity value.</param>
    /// <param name="stopBits">The stop bits value.</param>
    /// <param name="useV14">The use v14 value.</param>
    /// <param name="getByte">The get byte value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>
    /// Performs the <c>async_tx_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_tx_release(AsyncTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    /// <summary>
    /// Performs the <c>async_tx_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int async_tx_free(AsyncTransmitter state) {
        ArgumentNullException.ThrowIfNull(state);
        state.Dispose();
        return 0;
    }
}
