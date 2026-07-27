/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * Bitstream.cs - Managed C# port of bitstream.c and bitstream.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2006 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem {
    using System;

    /// <summary>
    /// Working state for bitstream composition and decomposition
    /// </summary>
    public sealed class BitstreamState : IDisposable {
        /// <summary>
        /// Defines the _disposed
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Defines the BitBuffer
        /// </summary>
        internal uint BitBuffer;

        /// <summary>
        /// Defines the Residue
        /// </summary>
        internal int Residue;

        /// <summary>
        /// Defines the LsbFirst
        /// </summary>
        internal bool LsbFirst;

        /// <summary>
        /// Gets a value indicating whether IsLeastSignificantBitFirst
        /// Gets whether bits are processed least-significant-bit first
        /// </summary>
        public bool IsLeastSignificantBitFirst {
            get {
                ThrowIfDisposed();
                return LsbFirst;
            }
        }

        /// <summary>
        /// Gets the number of unconsumed or not-yet-emitted bits in the state
        /// </summary>
        public int ResidualBitCount {
            get {
                ThrowIfDisposed();
                return Residue;
            }
        }

        /// <summary>
        /// Adds bits to an output buffer and advances <paramref name="offset"/>
        /// for every complete byte produced
        /// </summary>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="value">The value<see cref="uint"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        public void Put(Span<byte> output, ref int offset, uint value, int bits) {
            Bitstream.Put(this, output, ref offset, value, bits);
        }

        /// <summary>
        /// Reads bits from an input buffer and advances <paramref name="offset"/>
        /// for every byte consumed
        /// </summary>
        /// <param name="input">The input<see cref="ReadOnlySpan{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        /// <returns>The <see cref="uint"/></returns>
        public uint Get(ReadOnlySpan<byte> input, ref int offset, int bits) {
            return Bitstream.Get(this, input, ref offset, bits);
        }

        /// <summary>
        /// Writes the current residual bits at <paramref name="offset"/> without
        /// advancing the offset or changing the state
        /// </summary>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public void Emit(Span<byte> output, int offset) {
            Bitstream.Emit(this, output, offset);
        }

        /// <summary>
        /// Writes and clears residual bits. The offset advances by one byte when
        /// residual bits are present
        /// </summary>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public void Flush(Span<byte> output, ref int offset) {
            Bitstream.Flush(this, output, ref offset);
        }

        /// <summary>
        /// Resets the state and selects the bit order
        /// </summary>
        /// <param name="leastSignificantBitFirst">The leastSignificantBitFirst<see cref="bool"/></param>
        public void Reset(bool leastSignificantBitFirst) {
            ThrowIfDisposed();
            BitBuffer = 0;
            Residue = 0;
            LsbFirst = leastSignificantBitFirst;
        }

        /// <summary>
        /// The Dispose
        /// </summary>
        public void Dispose() {
            if (_disposed)
                return;

            BitBuffer = 0;
            Residue = 0;
            LsbFirst = false;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// The ResetForInitialization
        /// </summary>
        /// <param name="leastSignificantBitFirst">The leastSignificantBitFirst<see cref="bool"/></param>
        internal void ResetForInitialization(bool leastSignificantBitFirst) {
            _disposed = false;
            BitBuffer = 0;
            Residue = 0;
            LsbFirst = leastSignificantBitFirst;
        }

        /// <summary>
        /// The ThrowIfDisposed
        /// </summary>
        internal void ThrowIfDisposed() {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BitstreamState));
        }
    }

    /// <summary>
    /// Managed bitstream composition and decomposition routines
    /// </summary>
    public static class Bitstream {
        /// <summary>
        /// Defines the MinimumBitsPerOperation
        /// </summary>
        public const int MinimumBitsPerOperation = 1;

        /// <summary>
        /// Defines the MaximumBitsPerOperation
        /// </summary>
        public const int MaximumBitsPerOperation = 25;

        /// <summary>
        /// Initializes a new state or reinitializes an existing state
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState?"/></param>
        /// <param name="leastSignificantBitFirst">The leastSignificantBitFirst<see cref="bool"/></param>
        /// <returns>The <see cref="BitstreamState"/></returns>
        public static BitstreamState Initialize(
            BitstreamState? state,
            bool leastSignificantBitFirst) {
            state ??= new BitstreamState();
            state.ResetForInitialization(leastSignificantBitFirst);
            return state;
        }

        /// <summary>
        /// Adds between 1 and 25 bits to the output bitstream
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="value">The value<see cref="uint"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        public static void Put(
            BitstreamState state,
            Span<byte> output,
            ref int offset,
            uint value,
            int bits) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            ValidateBitCount(bits);
            ValidateOffset(output.Length, offset);

            int completeBytes = (state.Residue + bits) / 8;
            EnsureAvailable(output.Length, offset, completeBytes, nameof(output));

            uint mask = CreateMask(bits);
            value &= mask;

            if (state.LsbFirst) {
                state.BitBuffer |= unchecked(value << state.Residue);
                state.Residue += bits;

                while (state.Residue >= 8) {
                    state.Residue -= 8;
                    output[offset++] = unchecked((byte)(state.BitBuffer & 0xFFu));
                    state.BitBuffer >>= 8;
                }
            } else {
                state.BitBuffer = unchecked((state.BitBuffer << bits) | value);
                state.Residue += bits;

                while (state.Residue >= 8) {
                    state.Residue -= 8;
                    output[offset++] = unchecked((byte)((state.BitBuffer >> state.Residue) & 0xFFu));
                }
            }
        }

        /// <summary>
        /// Writes residual bits to the current output byte without clearing the
        /// state. The supplied offset is intentionally not advanced
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void Emit(
            BitstreamState state,
            Span<byte> output,
            int offset) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            ValidateOffset(output.Length, offset);

            if (state.Residue <= 0)
                return;

            EnsureAvailable(output.Length, offset, 1, nameof(output));

            uint residualBits = state.BitBuffer & CreateMask(state.Residue);
            output[offset] = state.LsbFirst
                ? unchecked((byte)residualBits)
                : unchecked((byte)(residualBits << (8 - state.Residue)));
        }

        /// <summary>
        /// Writes residual bits to the current output byte and clears the state
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void Flush(
            BitstreamState state,
            Span<byte> output,
            ref int offset) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            ValidateOffset(output.Length, offset);

            if (state.Residue > 0) {
                Emit(state, output, offset);
                offset++;
                state.Residue = 0;
            }

            state.BitBuffer = 0;
        }

        /// <summary>
        /// Gets between 1 and 25 bits from an input bitstream
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="input">The input<see cref="ReadOnlySpan{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        /// <returns>The <see cref="uint"/></returns>
        public static uint Get(
            BitstreamState state,
            ReadOnlySpan<byte> input,
            ref int offset,
            int bits) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            ValidateBitCount(bits);
            ValidateOffset(input.Length, offset);

            int missingBits = bits - state.Residue;
            int requiredBytes = missingBits > 0 ? (missingBits + 7) / 8 : 0;
            EnsureAvailable(input.Length, offset, requiredBytes, nameof(input));

            uint result;
            if (state.LsbFirst) {
                while (state.Residue < bits) {
                    state.BitBuffer |= unchecked((uint)input[offset++] << state.Residue);
                    state.Residue += 8;
                }

                state.Residue -= bits;
                result = state.BitBuffer & CreateMask(bits);
                state.BitBuffer >>= bits;
            } else {
                while (state.Residue < bits) {
                    state.BitBuffer = unchecked((state.BitBuffer << 8) | input[offset++]);
                    state.Residue += 8;
                }

                state.Residue -= bits;
                result = (state.BitBuffer >> state.Residue) & CreateMask(bits);
            }

            return result;
        }

        /// <summary>
        /// The Release
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Release(BitstreamState state) {
            ArgumentNullException.ThrowIfNull(state);
            state.ThrowIfDisposed();
            return 0;
        }

        /// <summary>
        /// The Free
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int Free(BitstreamState? state) {
            state?.Dispose();
            return 0;
        }

        /// <summary>
        /// The CreateMask
        /// </summary>
        /// <param name="bits">The bits<see cref="int"/></param>
        /// <returns>The <see cref="uint"/></returns>
        private static uint CreateMask(int bits) {
            return bits == 32
                ? uint.MaxValue
                : unchecked((1u << bits) - 1u);
        }

        /// <summary>
        /// The ValidateBitCount
        /// </summary>
        /// <param name="bits">The bits<see cref="int"/></param>
        private static void ValidateBitCount(int bits) {
            if (bits is < MinimumBitsPerOperation or > MaximumBitsPerOperation) {
                throw new ArgumentOutOfRangeException(
                    nameof(bits),
                    bits,
                    $"The bit count must be between {MinimumBitsPerOperation} and {MaximumBitsPerOperation}.");
            }
        }

        /// <summary>
        /// The ValidateOffset
        /// </summary>
        /// <param name="length">The length<see cref="int"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        private static void ValidateOffset(int length, int offset) {
            if ((uint)offset > (uint)length)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        /// <summary>
        /// The EnsureAvailable
        /// </summary>
        /// <param name="length">The length<see cref="int"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="requiredLength">The requiredLength<see cref="int"/></param>
        /// <param name="parameterName">The parameterName<see cref="string"/></param>
        private static void EnsureAvailable(
            int length,
            int offset,
            int requiredLength,
            string parameterName) {
            if (requiredLength < 0 || offset > length - requiredLength) {
                throw new ArgumentException(
                    "The supplied buffer does not contain enough space or data for this operation.",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Compatibility facade retaining the original native function names.
    /// Pointer movement is represented by a managed buffer plus a reference
    /// offset
    /// </summary>
    public static class BitstreamApi {
        /// <summary>
        /// The bitstream_init
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState?"/></param>
        /// <param name="lsbFirst">The lsbFirst<see cref="int"/></param>
        /// <returns>The <see cref="BitstreamState"/></returns>
        public static BitstreamState bitstream_init(BitstreamState? state, int lsbFirst) =>
            Bitstream.Initialize(state, lsbFirst != 0);

        /// <summary>
        /// The bitstream_put
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="value">The value<see cref="uint"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        public static void bitstream_put(
            BitstreamState state,
            Span<byte> output,
            ref int offset,
            uint value,
            int bits) =>
            Bitstream.Put(state, output, ref offset, value, bits);

        /// <summary>
        /// The bitstream_put
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="value">The value<see cref="uint"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        public static void bitstream_put(
            BitstreamState state,
            byte[] output,
            ref int offset,
            uint value,
            int bits) {
            ArgumentNullException.ThrowIfNull(output);
            Bitstream.Put(state, output.AsSpan(), ref offset, value, bits);
        }

        /// <summary>
        /// The bitstream_get
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="input">The input<see cref="ReadOnlySpan{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        /// <returns>The <see cref="uint"/></returns>
        public static uint bitstream_get(
            BitstreamState state,
            ReadOnlySpan<byte> input,
            ref int offset,
            int bits) =>
            Bitstream.Get(state, input, ref offset, bits);

        /// <summary>
        /// The bitstream_get
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="input">The input<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        /// <param name="bits">The bits<see cref="int"/></param>
        /// <returns>The <see cref="uint"/></returns>
        public static uint bitstream_get(
            BitstreamState state,
            byte[] input,
            ref int offset,
            int bits) {
            ArgumentNullException.ThrowIfNull(input);
            return Bitstream.Get(state, input.AsSpan(), ref offset, bits);
        }

        /// <summary>
        /// The bitstream_emit
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void bitstream_emit(
            BitstreamState state,
            Span<byte> output,
            int offset) =>
            Bitstream.Emit(state, output, offset);

        /// <summary>
        /// The bitstream_emit
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void bitstream_emit(
            BitstreamState state,
            byte[] output,
            int offset) {
            ArgumentNullException.ThrowIfNull(output);
            Bitstream.Emit(state, output.AsSpan(), offset);
        }

        /// <summary>
        /// The bitstream_flush
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="Span{byte}"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void bitstream_flush(
            BitstreamState state,
            Span<byte> output,
            ref int offset) =>
            Bitstream.Flush(state, output, ref offset);

        /// <summary>
        /// The bitstream_flush
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <param name="output">The output<see cref="byte[]"/></param>
        /// <param name="offset">The offset<see cref="int"/></param>
        public static void bitstream_flush(
            BitstreamState state,
            byte[] output,
            ref int offset) {
            ArgumentNullException.ThrowIfNull(output);
            Bitstream.Flush(state, output.AsSpan(), ref offset);
        }

        /// <summary>
        /// The bitstream_release
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bitstream_release(BitstreamState state) =>
            Bitstream.Release(state);

        /// <summary>
        /// The bitstream_free
        /// </summary>
        /// <param name="state">The state<see cref="BitstreamState?"/></param>
        /// <returns>The <see cref="int"/></returns>
        public static int bitstream_free(BitstreamState? state) =>
            Bitstream.Free(state);
    }
}
