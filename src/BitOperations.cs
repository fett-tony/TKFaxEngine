/*
 * TKFaxEngine - managed C# port
 *
 * BitOperations.cs
 *
 * Combined port of:
 *   bit_operations.h
 *   bit_operations.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2006 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using NumericsBitOperations = System.Numerics.BitOperations;

namespace TKFaxEngine;

/// <summary>
/// Bit-level helpers corresponding to bit_operations.h and bit_operations.c.
/// </summary>
public static class BitOperationsEx {
    /// <summary>
    /// Finds the bit position of the highest set bit.
    /// Returns -1 when <paramref name="bits"/> is zero.
    /// </summary>
    public static int TopBit(uint bits) {
        return bits == 0
            ? -1
            : 31 - NumericsBitOperations.LeadingZeroCount(bits);
    }

    /// <summary>
    /// Finds the bit position of the lowest set bit.
    /// Returns -1 when <paramref name="bits"/> is zero.
    /// </summary>
    public static int BottomBit(uint bits) {
        return bits == 0
            ? -1
            : NumericsBitOperations.TrailingZeroCount(bits);
    }

    /// <summary>
    /// Reverses all eight bits in one byte.
    /// </summary>
    public static byte Reverse8(byte value) {
        uint x = value;

        x = ((x & 0xF0u) >> 4) |
            ((x & 0x0Fu) << 4);

        x = ((x & 0xCCu) >> 2) |
            ((x & 0x33u) << 2);

        x = ((x & 0xAAu) >> 1) |
            ((x & 0x55u) << 1);

        return unchecked((byte)x);
    }

    /// <summary>
    /// Reverses all sixteen bits in one word.
    /// </summary>
    public static ushort Reverse16(ushort value) {
        uint x = value;

        x = ((x >> 8) | (x << 8)) & 0xFFFFu;

        x = ((x & 0xF0F0u) >> 4) |
            ((x & 0x0F0Fu) << 4);

        x = ((x & 0xCCCCu) >> 2) |
            ((x & 0x3333u) << 2);

        x = ((x & 0xAAAAu) >> 1) |
            ((x & 0x5555u) << 1);

        return unchecked((ushort)x);
    }

    /// <summary>
    /// Reverses all thirty-two bits in one word.
    /// </summary>
    public static uint Reverse32(uint value) {
        uint x = value;

        x = (x >> 16) |
            (x << 16);

        x = ((x & 0xFF00FF00u) >> 8) |
            ((x & 0x00FF00FFu) << 8);

        x = ((x & 0xF0F0F0F0u) >> 4) |
            ((x & 0x0F0F0F0Fu) << 4);

        x = ((x & 0xCCCCCCCCu) >> 2) |
            ((x & 0x33333333u) << 2);

        return ((x & 0xAAAAAAAAu) >> 1) |
               ((x & 0x55555555u) << 1);
    }

    /// <summary>
    /// Reverses the bits inside each of the four bytes without changing
    /// byte order.
    /// </summary>
    public static uint ReverseFourBytes(uint value) {
        uint x = value;

        x = ((x & 0xF0F0F0F0u) >> 4) |
            ((x & 0x0F0F0F0Fu) << 4);

        x = ((x & 0xCCCCCCCCu) >> 2) |
            ((x & 0x33333333u) << 2);

        return ((x & 0xAAAAAAAAu) >> 1) |
               ((x & 0x55555555u) << 1);
    }

    /// <summary>
    /// Reverses the bits inside each of the eight bytes without changing
    /// byte order.
    /// </summary>
    public static ulong ReverseEightBytes(ulong value) {
        ulong x = value;

        x = ((x & 0xF0F0F0F0F0F0F0F0UL) >> 4) |
            ((x & 0x0F0F0F0F0F0F0F0FUL) << 4);

        x = ((x & 0xCCCCCCCCCCCCCCCCUL) >> 2) |
            ((x & 0x3333333333333333UL) << 2);

        return ((x & 0xAAAAAAAAAAAAAAAAUL) >> 1) |
               ((x & 0x5555555555555555UL) << 1);
    }

    /// <summary>
    /// Reverses the bits in each byte of a buffer.
    /// Exact in-place operation is supported.
    /// </summary>
    public static void Reverse(
        Span<byte> destination,
        ReadOnlySpan<byte> source,
        int length) {
        ValidateLength(destination.Length, length);
        ValidateLength(source.Length, length);

        for (int index = 0; index < length; index++)
            destination[index] = Reverse8(source[index]);
    }

    /// <summary>
    /// Reverses the bits in every byte of a buffer and returns a new array.
    /// </summary>
    public static byte[] Reverse(
        ReadOnlySpan<byte> source) {
        byte[] result = new byte[source.Length];
        Reverse(result, source, source.Length);
        return result;
    }

    /// <summary>
    /// Counts the number of set bits in a 32-bit word.
    /// </summary>
    public static int OneBits32(uint value) {
        return NumericsBitOperations.PopCount(value);
    }

    /// <summary>
    /// Creates a mask from bit zero through the highest set bit.
    /// </summary>
    public static uint MakeMask32(uint value) {
        uint x = value;

        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;
        x |= x >> 16;

        return x;
    }

    /// <summary>
    /// Creates a mask from bit zero through the highest set bit.
    /// </summary>
    public static ushort MakeMask16(ushort value) {
        uint x = value;

        x |= x >> 1;
        x |= x >> 2;
        x |= x >> 4;
        x |= x >> 8;

        return unchecked((ushort)x);
    }

    /// <summary>
    /// Returns a word containing only the least-significant set bit.
    /// Returns zero when the input is zero.
    /// </summary>
    public static uint LeastSignificantOne32(uint value) {
        return value & unchecked(0u - value);
    }

    /// <summary>
    /// Returns a word containing only the most-significant set bit.
    /// Returns zero when the input is zero.
    /// </summary>
    public static uint MostSignificantOne32(uint value) {
        if (value == 0)
            return 0;

        return 1u << TopBit(value);
    }

    /// <summary>
    /// Returns 1 for odd parity or 0 for even parity.
    /// </summary>
    public static int Parity8(byte value) {
        uint x = value;

        x = (x ^ (x >> 4)) & 0x0Fu;
        return unchecked((int)((0x6996u >> (int)x) & 1u));
    }

    /// <summary>
    /// Returns 1 for odd parity or 0 for even parity.
    /// </summary>
    public static int Parity16(ushort value) {
        uint x = value;

        x ^= x >> 8;
        x = (x ^ (x >> 4)) & 0x0Fu;

        return unchecked((int)((0x6996u >> (int)x) & 1u));
    }

    /// <summary>
    /// Returns 1 for odd parity or 0 for even parity.
    /// </summary>
    public static int Parity32(uint value) {
        uint x = value;

        x ^= x >> 16;
        x ^= x >> 8;
        x = (x ^ (x >> 4)) & 0x0Fu;

        return unchecked((int)((0x6996u >> (int)x) & 1u));
    }

    private static void ValidateLength(
        int available,
        int length) {
        if (length < 0 || length > available)
            throw new ArgumentOutOfRangeException(nameof(length));
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class BitOperationsApi {
    public static int top_bit(uint bits) =>
        BitOperationsEx.TopBit(bits);

    public static int bottom_bit(uint bits) =>
        BitOperationsEx.BottomBit(bits);

    public static byte bit_reverse8(byte value) =>
        BitOperationsEx.Reverse8(value);

    public static ushort bit_reverse16(ushort value) =>
        BitOperationsEx.Reverse16(value);

    public static uint bit_reverse32(uint value) =>
        BitOperationsEx.Reverse32(value);

    public static uint bit_reverse_4bytes(uint value) =>
        BitOperationsEx.ReverseFourBytes(value);

    public static ulong bit_reverse_8bytes(ulong value) =>
        BitOperationsEx.ReverseEightBytes(value);

    public static void bit_reverse(
        Span<byte> destination,
        ReadOnlySpan<byte> source,
        int length) =>
        BitOperationsEx.Reverse(destination, source, length);

    public static int one_bits32(uint value) =>
        BitOperationsEx.OneBits32(value);

    public static uint make_mask32(uint value) =>
        BitOperationsEx.MakeMask32(value);

    public static ushort make_mask16(ushort value) =>
        BitOperationsEx.MakeMask16(value);

    public static uint least_significant_one32(uint value) =>
        BitOperationsEx.LeastSignificantOne32(value);

    public static uint most_significant_one32(uint value) =>
        BitOperationsEx.MostSignificantOne32(value);

    public static int parity8(byte value) =>
        BitOperationsEx.Parity8(value);

    public static int parity16(ushort value) =>
        BitOperationsEx.Parity16(value);

    public static int parity32(uint value) =>
        BitOperationsEx.Parity32(value);
}
