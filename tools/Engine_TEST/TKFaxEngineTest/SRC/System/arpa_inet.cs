/* Managed C# equivalent of arpa_inet.h for TKFaxEngine tests. */
#nullable enable

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace TKFaxEngineTest.System;

public static class arpa_inet {
    public const int AF_INET = (int)AddressFamily.InterNetwork;
    public const int AF_INET6 = (int)AddressFamily.InterNetworkV6;

    public static ushort htons(ushort value) => BinaryPrimitives.ReverseEndianness(value);
    public static ushort ntohs(ushort value) => BinaryPrimitives.ReverseEndianness(value);
    public static uint htonl(uint value) => BinaryPrimitives.ReverseEndianness(value);
    public static uint ntohl(uint value) => BinaryPrimitives.ReverseEndianness(value);

    public static int inet_pton(int addressFamily, string source, Span<byte> destination) {
        if (!IPAddress.TryParse(source, out IPAddress? address)) return 0;
        if ((addressFamily == AF_INET && address.AddressFamily != AddressFamily.InterNetwork) ||
            (addressFamily == AF_INET6 && address.AddressFamily != AddressFamily.InterNetworkV6)) return 0;
        byte[] bytes = address.GetAddressBytes();
        if (destination.Length < bytes.Length) return -1;
        bytes.CopyTo(destination);
        return 1;
    }

    public static string? inet_ntop(int addressFamily, ReadOnlySpan<byte> source) {
        int length = addressFamily == AF_INET ? 4 : addressFamily == AF_INET6 ? 16 : 0;
        return length != 0 && source.Length >= length ? new IPAddress(source[..length]).ToString() : null;
    }
}
