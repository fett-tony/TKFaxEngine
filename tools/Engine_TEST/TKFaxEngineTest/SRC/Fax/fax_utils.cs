/*
 * TKFaxEngine - managed C# port
 *
 * fax_utils.cs - fax test logging and TIFF page counting.
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 *
 * The control flow and output fields follow spanDSP tests/fax_utils.c.
 */

#nullable enable

using System.Buffers.Binary;
using TKFaxEngine.Daten.T30;
using static TKFaxEngine.Daten.T30.T30;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.FaxImage.t4_rx;

namespace TKFaxEngineTest.Fax.Test;

public static partial class fax_utils {
    public const int TelephonySampleRate = 8_000;
    public static void fax_log_tx_parameters(T30State state, string tag) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tag);

        LogIfPresent(tag, "Local ident", t30_get_tx_ident(state));
        LogIfPresent(tag, "Local sub-address", t30_get_tx_sub_address(state));
        LogIfPresent(tag, "Local polled sub-address", t30_get_tx_polled_sub_address(state));
        LogIfPresent(tag, "Local selective polling address", t30_get_tx_selective_polling_address(state));
        LogIfPresent(tag, "Local sender ident", t30_get_tx_sender_ident(state));
        LogIfPresent(tag, "Local password", t30_get_tx_password(state));
    }

    public static void fax_log_rx_parameters(T30State state, string tag) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tag);

        LogIfPresent(tag, "Remote ident", t30_get_rx_ident(state));
        LogIfPresent(tag, "Remote sub-address", t30_get_rx_sub_address(state));
        LogIfPresent(tag, "Remote polled sub-address", t30_get_rx_polled_sub_address(state));
        LogIfPresent(tag, "Remote selective polling address", t30_get_rx_selective_polling_address(state));
        LogIfPresent(tag, "Remote sender ident", t30_get_rx_sender_ident(state));
        LogIfPresent(tag, "Remote password", t30_get_rx_password(state));
        LogIfPresent(tag, "Remote was made in", t30_get_rx_country(state));
        LogIfPresent(tag, "Remote was made by", t30_get_rx_vendor(state));
        LogIfPresent(tag, "Remote is model", t30_get_rx_model(state));
    }

    public static void fax_log_final_transfer_statistics(T30State state, string tag) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tag);

        var statistics = new T30Statistics();
        t30_get_transfer_statistics(state, statistics);

        Console.WriteLine($"{tag}: Bit rate {statistics.BitRate}");
        Console.WriteLine($"{tag}: ECM {(statistics.ErrorCorrectingMode ? "on" : "off")}");
        Console.WriteLine($"{tag}: RTP events {statistics.RtpEvents}. RTN events {statistics.RtnEvents}");
        Console.WriteLine($"{tag}: Tx pages {statistics.PagesTransmitted}, rx pages {statistics.PagesReceived}");
    }

    public static void fax_log_page_transfer_statistics(T30State state, string tag) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(tag);

        var statistics = new T30Statistics();
        t30_get_transfer_statistics(state, statistics);

        Console.WriteLine($"{tag}: Page statistics");
        Console.WriteLine($"{tag}:   Pages in the file {statistics.PagesInFile}");
        Console.WriteLine($"{tag}:   Bad rows {statistics.BadRows}, longest bad row run {statistics.LongestBadRowRun}");
        Console.WriteLine($"{tag}:   Bad ECM frames {statistics.ErrorCorrectingModeRetries}");
        Console.WriteLine($"{tag}:   Compression type {t4_compression_to_str(statistics.Compression)} ({statistics.Compression})");
        Console.WriteLine($"{tag}:   Compressed image size {statistics.ImageSize} bytes");
        Console.WriteLine(
            $"{tag}:   Image type {t4_image_type_to_str(statistics.ExchangedType)} " +
            $"({t4_image_type_to_str(statistics.ImageType)} in the file)");
        Console.WriteLine(
            $"{tag}:   Image size {statistics.Width} pels x {statistics.Length} pels " +
            $"({statistics.ImageWidth} pels x {statistics.ImageLength} pels in the file)");
        Console.WriteLine(
            $"{tag}:   Image resolution {statistics.XResolution} pels/m x {statistics.YResolution} pels/m " +
            $"({statistics.ImageXResolution} pels/m x {statistics.ImageYResolution} pels/m in the file)");

        fax_log_final_transfer_statistics(state, tag);
    }

    /// <summary>
    /// Counts TIFF IFDs, matching the native TIFFSetDirectory loop. Both classic
    /// TIFF and BigTIFF are accepted; malformed loops and truncated offsets fail.
    /// </summary>
    public static int get_tiff_total_pages(string file) {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        try {
            using FileStream stream = File.OpenRead(file);
            Span<byte> header = stackalloc byte[16];
            if (stream.Read(header[..8]) != 8)
                return -1;

            bool littleEndian;
            if (header[0] == (byte)'I' && header[1] == (byte)'I')
                littleEndian = true;
            else if (header[0] == (byte)'M' && header[1] == (byte)'M')
                littleEndian = false;
            else
                return -1;

            ushort magic = ReadUInt16(header[2..4], littleEndian);
            ulong nextIfd;
            bool bigTiff;

            if (magic == 42) {
                bigTiff = false;
                nextIfd = ReadUInt32(header[4..8], littleEndian);
            } else if (magic == 43) {
                bigTiff = true;
                if (stream.Read(header[8..16]) != 8)
                    return -1;
                if (ReadUInt16(header[4..6], littleEndian) != 8 ||
                    ReadUInt16(header[6..8], littleEndian) != 0) {
                    return -1;
                }

                nextIfd = ReadUInt64(header[8..16], littleEndian);
            } else {
                return -1;
            }

            var visited = new HashSet<ulong>();
            int pages = 0;

            while (nextIfd != 0) {
                if (nextIfd >= (ulong)stream.Length || !visited.Add(nextIfd))
                    return -1;

                stream.Position = checked((long)nextIfd);
                ulong entryCount;

                if (bigTiff) {
                    if (stream.Read(header[..8]) != 8)
                        return -1;
                    entryCount = ReadUInt64(header[..8], littleEndian);
                    ulong nextPosition = checked(nextIfd + 8UL + entryCount * 20UL);
                    if (nextPosition + 8UL > (ulong)stream.Length)
                        return -1;
                    stream.Position = checked((long)nextPosition);
                    if (stream.Read(header[..8]) != 8)
                        return -1;
                    nextIfd = ReadUInt64(header[..8], littleEndian);
                } else {
                    if (stream.Read(header[..2]) != 2)
                        return -1;
                    entryCount = ReadUInt16(header[..2], littleEndian);
                    ulong nextPosition = checked(nextIfd + 2UL + entryCount * 12UL);
                    if (nextPosition + 4UL > (ulong)stream.Length)
                        return -1;
                    stream.Position = checked((long)nextPosition);
                    if (stream.Read(header[..4]) != 4)
                        return -1;
                    nextIfd = ReadUInt32(header[..4], littleEndian);
                }

                pages++;
            }

            return pages;
        } catch (IOException) {
            return -1;
        } catch (UnauthorizedAccessException) {
            return -1;
        } catch (OverflowException) {
            return -1;
        }
    }

    private static void LogIfPresent(string tag, string label, string? value) {
        if (!string.IsNullOrEmpty(value))
            Console.WriteLine($"{tag}: {label} '{value}'");
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt16LittleEndian(data)
            : BinaryPrimitives.ReadUInt16BigEndian(data);

    private static uint ReadUInt32(ReadOnlySpan<byte> data, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt32LittleEndian(data)
            : BinaryPrimitives.ReadUInt32BigEndian(data);

    private static ulong ReadUInt64(ReadOnlySpan<byte> data, bool littleEndian) =>
        littleEndian
            ? BinaryPrimitives.ReadUInt64LittleEndian(data)
            : BinaryPrimitives.ReadUInt64BigEndian(data);
}
