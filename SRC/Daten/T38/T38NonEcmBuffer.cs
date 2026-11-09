/*
 * TKFaxEngine - managed C# port
 *
 * Combined port of t38_non_ecm_buffer.c and t38_non_ecm_buffer.h.
 */

using System.Numerics;

namespace TKFaxEngine.Daten.T38;

/// <summary>
/// Represents the <c>T38NonEcmBufferState</c> type.
/// </summary>
public sealed class T38NonEcmBufferState {
    /// <summary>
    /// Defines the <c>BufferLength</c> constant.
    /// </summary>
    public const int BufferLength = T38NonEcmBuffer.T38_NON_ECM_TX_BUF_LEN;
    /// <summary>
    /// Gets or sets the <c>MinimumBitsPerRow</c> value.
    /// </summary>
    public int MinimumBitsPerRow { get; set; }
    /// <summary>
    /// Gets the <c>Data</c> value.
    /// </summary>
    public byte[] Data { get; } = new byte[BufferLength];
    /// <summary>
    /// Gets or sets the <c>InputPointer</c> value.
    /// </summary>
    public int InputPointer { get; set; }
    /// <summary>
    /// Gets or sets the <c>OutputPointer</c> value.
    /// </summary>
    public int OutputPointer { get; set; }
    /// <summary>
    /// Gets or sets the <c>LatestEolPointer</c> value.
    /// </summary>
    public int LatestEolPointer { get; set; }
    /// <summary>
    /// Gets or sets the <c>RowBits</c> value.
    /// </summary>
    public int RowBits { get; set; }
    /// <summary>
    /// Gets or sets the <c>BitStream</c> value.
    /// </summary>
    public uint BitStream { get; set; }
    /// <summary>
    /// Gets or sets the <c>FlowControlFillOctet</c> value.
    /// </summary>
    public byte FlowControlFillOctet { get; set; }
    /// <summary>
    /// Gets or sets the <c>InputPhase</c> value.
    /// </summary>
    public int InputPhase { get; set; }
    /// <summary>
    /// Gets or sets the <c>DataFinished</c> value.
    /// </summary>
    public bool DataFinished { get; set; }
    /// <summary>
    /// Gets or sets the <c>Octet</c> value.
    /// </summary>
    public uint Octet { get; set; }
    /// <summary>
    /// Gets or sets the <c>BitNumber</c> value.
    /// </summary>
    public int BitNumber { get; set; }
    /// <summary>
    /// Gets or sets the <c>ImageDataMode</c> value.
    /// </summary>
    public bool ImageDataMode { get; set; }
    /// <summary>
    /// Gets or sets the <c>InputOctets</c> value.
    /// </summary>
    public int InputOctets { get; set; }
    /// <summary>
    /// Gets or sets the <c>InputRows</c> value.
    /// </summary>
    public int InputRows { get; set; }
    /// <summary>
    /// Gets or sets the <c>MinimumRowBitsFillOctets</c> value.
    /// </summary>
    public int MinimumRowBitsFillOctets { get; set; }
    /// <summary>
    /// Gets or sets the <c>OutputOctets</c> value.
    /// </summary>
    public int OutputOctets { get; set; }
    /// <summary>
    /// Gets or sets the <c>OutputRows</c> value.
    /// </summary>
    public int OutputRows { get; set; }
    /// <summary>
    /// Gets or sets the <c>FlowControlFillOctets</c> value.
    /// </summary>
    public int FlowControlFillOctets { get; set; }
}

/// <summary>
/// Represents the <c>T38NonEcmBuffer</c> type.
/// </summary>
public static class T38NonEcmBuffer {
    /// <summary>
    /// Defines the <c>T38_NON_ECM_TX_BUF_LEN</c> constant.
    /// </summary>
    public const int T38_NON_ECM_TX_BUF_LEN = 16384;

    private const int TcfAtInitialAllOnes = 0;
    private const int TcfAtAllZeros = 1;
    private const int ImageWaitingForFirstEol = 2;
    private const int ImageInProgress = 3;
    private const int BufferMask = T38NonEcmBufferState.BufferLength - 1;

    private static void restart_buffer(T38NonEcmBufferState state) {
        state.Octet = 0xFF;
        state.FlowControlFillOctet = 0xFF;
        state.InputPhase = state.ImageDataMode
            ? ImageWaitingForFirstEol
            : TcfAtInitialAllOnes;
        state.BitStream = 0xFFFF;
        state.OutputPointer = 0;
        state.InputPointer = 0;
        state.LatestEolPointer = 0;
        state.DataFinished = false;
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_get_bit</c> operation.
    /// </summary>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t38_non_ecm_buffer_get_bit(object? userData) {
        if (userData is not T38NonEcmBufferState state)
            throw new ArgumentException(
                "The callback user data must be a T38NonEcmBufferState.",
                nameof(userData));

        if (state.BitNumber <= 0) {
            if (state.OutputPointer != state.LatestEolPointer) {
                state.Octet = state.Data[state.OutputPointer];
                state.OutputPointer = (state.OutputPointer + 1) & BufferMask;
            } else {
                if (state.DataFinished) {
                    restart_buffer(state);
                    return SignalStatus.EndOfData;
                }
                state.Octet = state.FlowControlFillOctet;
                state.FlowControlFillOctets++;
            }
            state.OutputOctets++;
            state.BitNumber = 8;
        }

        state.BitNumber--;
        int bit = (int)((state.Octet >> 7) & 1);
        state.Octet = (state.Octet << 1) & 0xFF;
        return bit;
    }



    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_push</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    public static void t38_non_ecm_buffer_push(T38NonEcmBufferState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.LatestEolPointer = state.InputPointer;
        state.DataFinished = true;
    }



    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_inject</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="length">The length value.</param>
    public static void t38_non_ecm_buffer_inject(
        T38NonEcmBufferState state,
        byte[] buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(buffer);
        if ((uint)length > (uint)buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        int index = 0;
        switch (state.InputPhase) {
            case TcfAtInitialAllOnes:
                for (; index < length; index++) {
                    if (buffer[index] != 0xFF) {
                        state.InputPhase = TcfAtAllZeros;
                        state.FlowControlFillOctet = 0x00;
                        break;
                    }
                }
                goto case TcfAtAllZeros;

            case TcfAtAllZeros:
                for (; index < length; index++) {
                    state.Data[state.InputPointer] = buffer[index];
                    state.LatestEolPointer = state.InputPointer;
                    state.InputPointer = (state.InputPointer + 1) & BufferMask;
                    state.InputOctets++;
                }
                break;

            case ImageWaitingForFirstEol:
                for (; index < length; index++) {
                    if (buffer[index] != 0) {
                        int upper = bottom_bit(state.BitStream | 0x800u);
                        int lower = top_bit(buffer[index]);
                        if (upper - lower > 3) {
                            state.InputPhase = ImageInProgress;
                            state.RowBits = lower - 8;
                            state.LatestEolPointer = state.InputPointer;
                            state.FlowControlFillOctet = 0x00;

                            state.Data[state.InputPointer] = 0x00;
                            state.InputPointer = (state.InputPointer + 1) & BufferMask;
                            state.Data[state.InputPointer] = 0x00;
                            state.InputPointer = (state.InputPointer + 1) & BufferMask;
                            state.Data[state.InputPointer] = buffer[index];
                            state.InputPointer = (state.InputPointer + 1) & BufferMask;
                            state.InputOctets += 3;
                            state.BitStream = (state.BitStream << 8) | buffer[index];
                            index++;
                            break;
                        }
                    }
                    state.BitStream = (state.BitStream << 8) | buffer[index];
                }
                if (index >= length)
                    break;
                goto case ImageInProgress;

            case ImageInProgress:
                for (; index < length; index++) {
                    if (buffer[index] != 0) {
                        int upper = bottom_bit(state.BitStream | 0x800u);
                        int lower = top_bit(buffer[index]);
                        if (upper - lower > 3) {
                            state.RowBits += 8 - lower;
                            if (state.RowBits < 12 || state.RowBits > 13) {
                                while (state.RowBits < state.MinimumBitsPerRow) {
                                    state.MinimumRowBitsFillOctets++;
                                    state.Data[state.InputPointer] = 0;
                                    state.RowBits += 8;
                                    state.InputPointer = (state.InputPointer + 1) & BufferMask;
                                }
                                state.LatestEolPointer = state.InputPointer;
                            }
                            state.RowBits = lower - 8;
                            state.InputRows++;
                        }
                    }
                    state.BitStream = (state.BitStream << 8) | buffer[index];
                    state.Data[state.InputPointer] = buffer[index];
                    state.RowBits += 8;
                    state.InputPointer = (state.InputPointer + 1) & BufferMask;
                    state.InputOctets++;
                }
                break;
        }
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_report_input_status</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="logging">The logging value.</param>
    public static void t38_non_ecm_buffer_report_input_status(
        T38NonEcmBufferState state,
        T38Log logging) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logging);

        if (state.InputOctets == 0 && state.MinimumRowBitsFillOctets == 0)
            return;

        logging.Flow(
            $"{state.InputOctets}+{state.MinimumRowBitsFillOctets} " +
            $"incoming non-ECM octets, {state.InputRows} rows.");

        state.InputOctets = 0;
        state.InputRows = 0;
        state.MinimumRowBitsFillOctets = 0;
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_report_output_status</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="logging">The logging value.</param>
    public static void t38_non_ecm_buffer_report_output_status(
        T38NonEcmBufferState state,
        T38Log logging) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(logging);

        if (state.OutputOctets == 0 && state.FlowControlFillOctets == 0)
            return;

        logging.Flow(
            $"{state.OutputOctets - state.FlowControlFillOctets}+" +
            $"{state.FlowControlFillOctets} outgoing non-ECM octets, " +
            $"{state.OutputRows} rows.");

        state.OutputOctets = 0;
        state.OutputRows = 0;
        state.FlowControlFillOctets = 0;
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_set_mode</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageMode">The image mode value.</param>
    /// <param name="minimumBitsPerRow">The minimum bits per row value.</param>
    public static void t38_non_ecm_buffer_set_mode(
        T38NonEcmBufferState state,
        bool imageMode,
        int minimumBitsPerRow) {
        ArgumentNullException.ThrowIfNull(state);

        bool oldMode = state.ImageDataMode;
        state.ImageDataMode = imageMode;
        state.MinimumBitsPerRow = minimumBitsPerRow;

        if (imageMode != oldMode)
            restart_buffer(state);
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="imageMode">The image mode value.</param>
    /// <param name="minimumBitsPerRow">The minimum bits per row value.</param>
    /// <returns>The result of the operation.</returns>
    public static T38NonEcmBufferState t38_non_ecm_buffer_init(
        T38NonEcmBufferState? state,
        bool imageMode,
        int minimumBitsPerRow) {
        state ??= new T38NonEcmBufferState();

        Array.Clear(state.Data, 0, state.Data.Length);
        state.MinimumBitsPerRow = minimumBitsPerRow;
        state.InputPointer = 0;
        state.OutputPointer = 0;
        state.LatestEolPointer = 0;
        state.RowBits = 0;
        state.BitStream = 0;
        state.FlowControlFillOctet = 0;
        state.InputPhase = 0;
        state.DataFinished = false;
        state.Octet = 0;
        state.BitNumber = 0;
        state.ImageDataMode = imageMode;
        state.InputOctets = 0;
        state.InputRows = 0;
        state.MinimumRowBitsFillOctets = 0;
        state.OutputOctets = 0;
        state.OutputRows = 0;
        state.FlowControlFillOctets = 0;

        restart_buffer(state);
        return state;
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t38_non_ecm_buffer_release(T38NonEcmBufferState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    /// <summary>
    /// Performs the <c>t38_non_ecm_buffer_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int t38_non_ecm_buffer_free(T38NonEcmBufferState? state) {
        _ = state;
        return 0;
    }



    private static int top_bit(uint value) {
        return value == 0 ? -1 : BitOperations.Log2(value);
    }

    private static int bottom_bit(uint value) {
        return value == 0 ? -1 : BitOperations.TrailingZeroCount(value);
    }
}
