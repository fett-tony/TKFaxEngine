/*
 * TKFaxEngine - managed C# port
 *
 * Crc.cs
 *
 * Combined port of:
 *   crc.h
 *   crc.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2003 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

namespace TKFaxEngine;

/// <summary>
/// ITU/CCITT CRC-32 functions corresponding to crc_itu32_*.
/// </summary>
public static class CrcItu32 {
    public const uint InitialValue = 0xFFFFFFFFu;
    public const uint FinalXorValue = 0xFFFFFFFFu;
    public const uint ValidFrameRemainder = 0xDEBB20E3u;

    private static readonly uint[] Table =
    {
        0x00000000u, 0x77073096u, 0xEE0E612Cu, 0x990951BAu, 0x076DC419u, 0x706AF48Fu, 0xE963A535u, 0x9E6495A3u,
        0x0EDB8832u, 0x79DCB8A4u, 0xE0D5E91Eu, 0x97D2D988u, 0x09B64C2Bu, 0x7EB17CBDu, 0xE7B82D07u, 0x90BF1D91u,
        0x1DB71064u, 0x6AB020F2u, 0xF3B97148u, 0x84BE41DEu, 0x1ADAD47Du, 0x6DDDE4EBu, 0xF4D4B551u, 0x83D385C7u,
        0x136C9856u, 0x646BA8C0u, 0xFD62F97Au, 0x8A65C9ECu, 0x14015C4Fu, 0x63066CD9u, 0xFA0F3D63u, 0x8D080DF5u,
        0x3B6E20C8u, 0x4C69105Eu, 0xD56041E4u, 0xA2677172u, 0x3C03E4D1u, 0x4B04D447u, 0xD20D85FDu, 0xA50AB56Bu,
        0x35B5A8FAu, 0x42B2986Cu, 0xDBBBC9D6u, 0xACBCF940u, 0x32D86CE3u, 0x45DF5C75u, 0xDCD60DCFu, 0xABD13D59u,
        0x26D930ACu, 0x51DE003Au, 0xC8D75180u, 0xBFD06116u, 0x21B4F4B5u, 0x56B3C423u, 0xCFBA9599u, 0xB8BDA50Fu,
        0x2802B89Eu, 0x5F058808u, 0xC60CD9B2u, 0xB10BE924u, 0x2F6F7C87u, 0x58684C11u, 0xC1611DABu, 0xB6662D3Du,
        0x76DC4190u, 0x01DB7106u, 0x98D220BCu, 0xEFD5102Au, 0x71B18589u, 0x06B6B51Fu, 0x9FBFE4A5u, 0xE8B8D433u,
        0x7807C9A2u, 0x0F00F934u, 0x9609A88Eu, 0xE10E9818u, 0x7F6A0DBBu, 0x086D3D2Du, 0x91646C97u, 0xE6635C01u,
        0x6B6B51F4u, 0x1C6C6162u, 0x856530D8u, 0xF262004Eu, 0x6C0695EDu, 0x1B01A57Bu, 0x8208F4C1u, 0xF50FC457u,
        0x65B0D9C6u, 0x12B7E950u, 0x8BBEB8EAu, 0xFCB9887Cu, 0x62DD1DDFu, 0x15DA2D49u, 0x8CD37CF3u, 0xFBD44C65u,
        0x4DB26158u, 0x3AB551CEu, 0xA3BC0074u, 0xD4BB30E2u, 0x4ADFA541u, 0x3DD895D7u, 0xA4D1C46Du, 0xD3D6F4FBu,
        0x4369E96Au, 0x346ED9FCu, 0xAD678846u, 0xDA60B8D0u, 0x44042D73u, 0x33031DE5u, 0xAA0A4C5Fu, 0xDD0D7CC9u,
        0x5005713Cu, 0x270241AAu, 0xBE0B1010u, 0xC90C2086u, 0x5768B525u, 0x206F85B3u, 0xB966D409u, 0xCE61E49Fu,
        0x5EDEF90Eu, 0x29D9C998u, 0xB0D09822u, 0xC7D7A8B4u, 0x59B33D17u, 0x2EB40D81u, 0xB7BD5C3Bu, 0xC0BA6CADu,
        0xEDB88320u, 0x9ABFB3B6u, 0x03B6E20Cu, 0x74B1D29Au, 0xEAD54739u, 0x9DD277AFu, 0x04DB2615u, 0x73DC1683u,
        0xE3630B12u, 0x94643B84u, 0x0D6D6A3Eu, 0x7A6A5AA8u, 0xE40ECF0Bu, 0x9309FF9Du, 0x0A00AE27u, 0x7D079EB1u,
        0xF00F9344u, 0x8708A3D2u, 0x1E01F268u, 0x6906C2FEu, 0xF762575Du, 0x806567CBu, 0x196C3671u, 0x6E6B06E7u,
        0xFED41B76u, 0x89D32BE0u, 0x10DA7A5Au, 0x67DD4ACCu, 0xF9B9DF6Fu, 0x8EBEEFF9u, 0x17B7BE43u, 0x60B08ED5u,
        0xD6D6A3E8u, 0xA1D1937Eu, 0x38D8C2C4u, 0x4FDFF252u, 0xD1BB67F1u, 0xA6BC5767u, 0x3FB506DDu, 0x48B2364Bu,
        0xD80D2BDAu, 0xAF0A1B4Cu, 0x36034AF6u, 0x41047A60u, 0xDF60EFC3u, 0xA867DF55u, 0x316E8EEFu, 0x4669BE79u,
        0xCB61B38Cu, 0xBC66831Au, 0x256FD2A0u, 0x5268E236u, 0xCC0C7795u, 0xBB0B4703u, 0x220216B9u, 0x5505262Fu,
        0xC5BA3BBEu, 0xB2BD0B28u, 0x2BB45A92u, 0x5CB36A04u, 0xC2D7FFA7u, 0xB5D0CF31u, 0x2CD99E8Bu, 0x5BDEAE1Du,
        0x9B64C2B0u, 0xEC63F226u, 0x756AA39Cu, 0x026D930Au, 0x9C0906A9u, 0xEB0E363Fu, 0x72076785u, 0x05005713u,
        0x95BF4A82u, 0xE2B87A14u, 0x7BB12BAEu, 0x0CB61B38u, 0x92D28E9Bu, 0xE5D5BE0Du, 0x7CDCEFB7u, 0x0BDBDF21u,
        0x86D3D2D4u, 0xF1D4E242u, 0x68DDB3F8u, 0x1FDA836Eu, 0x81BE16CDu, 0xF6B9265Bu, 0x6FB077E1u, 0x18B74777u,
        0x88085AE6u, 0xFF0F6A70u, 0x66063BCAu, 0x11010B5Cu, 0x8F659EFFu, 0xF862AE69u, 0x616BFFD3u, 0x166CCF45u,
        0xA00AE278u, 0xD70DD2EEu, 0x4E048354u, 0x3903B3C2u, 0xA7672661u, 0xD06016F7u, 0x4969474Du, 0x3E6E77DBu,
        0xAED16A4Au, 0xD9D65ADCu, 0x40DF0B66u, 0x37D83BF0u, 0xA9BCAE53u, 0xDEBB9EC5u, 0x47B2CF7Fu, 0x30B5FFE9u,
        0xBDBDF21Cu, 0xCABAC28Au, 0x53B39330u, 0x24B4A3A6u, 0xBAD03605u, 0xCDD70693u, 0x54DE5729u, 0x23D967BFu,
        0xB3667A2Eu, 0xC4614AB8u, 0x5D681B02u, 0x2A6F2B94u, 0xB40BBE37u, 0xC30C8EA1u, 0x5A05DF1Bu, 0x2D02EF8Du
    };

    /// <summary>
    /// Calculates or continues an ITU/CCITT CRC-32.
    /// </summary>
    public static uint Calculate(
        ReadOnlySpan<byte> buffer,
        uint crc) {
        foreach (byte value in buffer) {
            int index = unchecked((int)((crc ^ value) & 0xFFu));
            crc = ((crc >> 8) & 0x00FFFFFFu) ^ Table[index];
        }

        return crc;
    }

    /// <summary>
    /// Appends the finalized CRC in little-endian byte order and returns the
    /// new frame length.
    /// </summary>
    public static int Append(
        Span<byte> buffer,
        int length) {
        ValidateFrameLength(buffer.Length, length, 4);

        uint crc = Calculate(buffer[..length], InitialValue);
        crc ^= FinalXorValue;

        buffer[length] = unchecked((byte)crc);
        buffer[length + 1] = unchecked((byte)(crc >> 8));
        buffer[length + 2] = unchecked((byte)(crc >> 16));
        buffer[length + 3] = unchecked((byte)(crc >> 24));

        return checked(length + 4);
    }

    /// <summary>
    /// Checks a frame containing its four CRC bytes.
    /// </summary>
    public static bool Check(ReadOnlySpan<byte> frame) {
        return Calculate(frame, InitialValue) == ValidFrameRemainder;
    }

    private static void ValidateFrameLength(
        int bufferLength,
        int frameLength,
        int requiredTrailerBytes) {
        if (frameLength < 0 || frameLength > bufferLength)
            throw new ArgumentOutOfRangeException(nameof(frameLength));

        if (bufferLength - frameLength < requiredTrailerBytes) {
            throw new ArgumentException(
                $"The buffer requires at least {requiredTrailerBytes} free trailer bytes.",
                nameof(bufferLength));
        }
    }
}

/// <summary>
/// ITU/CCITT CRC-16 functions corresponding to crc_itu16_*.
/// </summary>
public static class CrcItu16 {
    public const ushort InitialValue = 0xFFFF;
    public const ushort FinalXorValue = 0xFFFF;
    public const ushort ValidFrameRemainder = 0xF0B8;

    private static readonly ushort[] Table =
    {
        0x0000, 0x1189, 0x2312, 0x329B, 0x4624, 0x57AD, 0x6536, 0x74BF,
        0x8C48, 0x9DC1, 0xAF5A, 0xBED3, 0xCA6C, 0xDBE5, 0xE97E, 0xF8F7,
        0x1081, 0x0108, 0x3393, 0x221A, 0x56A5, 0x472C, 0x75B7, 0x643E,
        0x9CC9, 0x8D40, 0xBFDB, 0xAE52, 0xDAED, 0xCB64, 0xF9FF, 0xE876,
        0x2102, 0x308B, 0x0210, 0x1399, 0x6726, 0x76AF, 0x4434, 0x55BD,
        0xAD4A, 0xBCC3, 0x8E58, 0x9FD1, 0xEB6E, 0xFAE7, 0xC87C, 0xD9F5,
        0x3183, 0x200A, 0x1291, 0x0318, 0x77A7, 0x662E, 0x54B5, 0x453C,
        0xBDCB, 0xAC42, 0x9ED9, 0x8F50, 0xFBEF, 0xEA66, 0xD8FD, 0xC974,
        0x4204, 0x538D, 0x6116, 0x709F, 0x0420, 0x15A9, 0x2732, 0x36BB,
        0xCE4C, 0xDFC5, 0xED5E, 0xFCD7, 0x8868, 0x99E1, 0xAB7A, 0xBAF3,
        0x5285, 0x430C, 0x7197, 0x601E, 0x14A1, 0x0528, 0x37B3, 0x263A,
        0xDECD, 0xCF44, 0xFDDF, 0xEC56, 0x98E9, 0x8960, 0xBBFB, 0xAA72,
        0x6306, 0x728F, 0x4014, 0x519D, 0x2522, 0x34AB, 0x0630, 0x17B9,
        0xEF4E, 0xFEC7, 0xCC5C, 0xDDD5, 0xA96A, 0xB8E3, 0x8A78, 0x9BF1,
        0x7387, 0x620E, 0x5095, 0x411C, 0x35A3, 0x242A, 0x16B1, 0x0738,
        0xFFCF, 0xEE46, 0xDCDD, 0xCD54, 0xB9EB, 0xA862, 0x9AF9, 0x8B70,
        0x8408, 0x9581, 0xA71A, 0xB693, 0xC22C, 0xD3A5, 0xE13E, 0xF0B7,
        0x0840, 0x19C9, 0x2B52, 0x3ADB, 0x4E64, 0x5FED, 0x6D76, 0x7CFF,
        0x9489, 0x8500, 0xB79B, 0xA612, 0xD2AD, 0xC324, 0xF1BF, 0xE036,
        0x18C1, 0x0948, 0x3BD3, 0x2A5A, 0x5EE5, 0x4F6C, 0x7DF7, 0x6C7E,
        0xA50A, 0xB483, 0x8618, 0x9791, 0xE32E, 0xF2A7, 0xC03C, 0xD1B5,
        0x2942, 0x38CB, 0x0A50, 0x1BD9, 0x6F66, 0x7EEF, 0x4C74, 0x5DFD,
        0xB58B, 0xA402, 0x9699, 0x8710, 0xF3AF, 0xE226, 0xD0BD, 0xC134,
        0x39C3, 0x284A, 0x1AD1, 0x0B58, 0x7FE7, 0x6E6E, 0x5CF5, 0x4D7C,
        0xC60C, 0xD785, 0xE51E, 0xF497, 0x8028, 0x91A1, 0xA33A, 0xB2B3,
        0x4A44, 0x5BCD, 0x6956, 0x78DF, 0x0C60, 0x1DE9, 0x2F72, 0x3EFB,
        0xD68D, 0xC704, 0xF59F, 0xE416, 0x90A9, 0x8120, 0xB3BB, 0xA232,
        0x5AC5, 0x4B4C, 0x79D7, 0x685E, 0x1CE1, 0x0D68, 0x3FF3, 0x2E7A,
        0xE70E, 0xF687, 0xC41C, 0xD595, 0xA12A, 0xB0A3, 0x8238, 0x93B1,
        0x6B46, 0x7ACF, 0x4854, 0x59DD, 0x2D62, 0x3CEB, 0x0E70, 0x1FF9,
        0xF78F, 0xE606, 0xD49D, 0xC514, 0xB1AB, 0xA022, 0x92B9, 0x8330,
        0x7BC7, 0x6A4E, 0x58D5, 0x495C, 0x3DE3, 0x2C6A, 0x1EF1, 0x0F78
    };

    /// <summary>
    /// Calculates or continues an ITU/CCITT CRC-16 by complete bytes.
    /// </summary>
    public static ushort Calculate(
        ReadOnlySpan<byte> buffer,
        ushort crc) {
        foreach (byte value in buffer) {
            int index = (crc ^ value) & 0xFF;
            crc = unchecked((ushort)((crc >> 8) ^ Table[index]));
        }

        return crc;
    }

    /// <summary>
    /// Updates the CRC with the requested number of bits from one byte,
    /// starting at the least-significant bit.
    /// </summary>
    public static ushort CalculateBits(
        byte value,
        int bitCount,
        ushort crc) {
        if (bitCount is < 0 or > 8) {
            throw new ArgumentOutOfRangeException(
                nameof(bitCount),
                bitCount,
                "The bit count must be between 0 and 8.");
        }

        for (int i = 0; i < bitCount; i++) {
            if (((value ^ crc) & 1) != 0)
                crc = unchecked((ushort)((crc >> 1) ^ 0x8408));
            else
                crc = unchecked((ushort)(crc >> 1));

            value >>= 1;
        }

        return crc;
    }

    /// <summary>
    /// Appends the finalized CRC in little-endian byte order and returns the
    /// new frame length.
    /// </summary>
    public static int Append(
        Span<byte> buffer,
        int length) {
        ValidateFrameLength(buffer.Length, length, 2);

        ushort crc = Calculate(buffer[..length], InitialValue);
        crc ^= FinalXorValue;

        buffer[length] = unchecked((byte)crc);
        buffer[length + 1] = unchecked((byte)(crc >> 8));

        return checked(length + 2);
    }

    /// <summary>
    /// Checks a frame containing its two CRC bytes.
    /// </summary>
    public static bool Check(ReadOnlySpan<byte> frame) {
        return Calculate(frame, InitialValue) == ValidFrameRemainder;
    }

    private static void ValidateFrameLength(
        int bufferLength,
        int frameLength,
        int requiredTrailerBytes) {
        if (frameLength < 0 || frameLength > bufferLength)
            throw new ArgumentOutOfRangeException(nameof(frameLength));

        if (bufferLength - frameLength < requiredTrailerBytes) {
            throw new ArgumentException(
                $"The buffer requires at least {requiredTrailerBytes} free trailer bytes.",
                nameof(bufferLength));
        }
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class CrcApi {
    public static uint crc_itu32_calc(
        ReadOnlySpan<byte> buffer,
        int length,
        uint crc) {
        ValidateLength(buffer.Length, length);
        return CrcItu32.Calculate(buffer[..length], crc);
    }

    public static int crc_itu32_append(
        Span<byte> buffer,
        int length) =>
        CrcItu32.Append(buffer, length);

    public static bool crc_itu32_check(
        ReadOnlySpan<byte> buffer,
        int length) {
        ValidateLength(buffer.Length, length);
        return CrcItu32.Check(buffer[..length]);
    }

    public static ushort crc_itu16_calc(
        ReadOnlySpan<byte> buffer,
        int length,
        ushort crc) {
        ValidateLength(buffer.Length, length);
        return CrcItu16.Calculate(buffer[..length], crc);
    }

    public static ushort crc_itu16_bits(
        byte value,
        int bitCount,
        ushort crc) =>
        CrcItu16.CalculateBits(value, bitCount, crc);

    public static int crc_itu16_append(
        Span<byte> buffer,
        int length) =>
        CrcItu16.Append(buffer, length);

    public static bool crc_itu16_check(
        ReadOnlySpan<byte> buffer,
        int length) {
        ValidateLength(buffer.Length, length);
        return CrcItu16.Check(buffer[..length]);
    }

    private static void ValidateLength(
        int bufferLength,
        int length) {
        if (length < 0 || length > bufferLength)
            throw new ArgumentOutOfRangeException(nameof(length));
    }
}
