/* Native Windows/MSVC datagram socket harness for the restored spanDSP tests. */

#nullable enable

using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngine;
using static TKFaxEngineTest.System.pseudo_terminals;

namespace TKFaxEngineTest.System;

public sealed class socket_dgram_harness_state_t {
    public object? user_data;
    public span_put_msg_func_t? terminal_callback;
    public termio_update_func_t? termios_callback;
    public span_modem_status_func_t? hangup_callback;
    public put_msg_free_space_func_t? terminal_free_space_callback;
    public span_put_msg_func_t? rx_callback;
    public span_get_msg_func_t? tx_callback;
    public span_timer_handler_t? timer_callback;
    public nuint net_fd;
    public nint pty_fd;
    public SpanLogState logging = new();
    public termios termios = new();
    public sockaddr_in local_addr;
    public int local_addr_len;
    public sockaddr_in far_addr;
    public int far_addr_len;
    public uint delay;
    public uint started;
    public uint pty_closed;
    public uint close_count;
    public pseudo_terminal_state_t pty = new();
}

public static unsafe class socket_dgram_harness {
    private const int AF_INET = 2;
    private const int SOCK_DGRAM = 2;
    private const int IPPROTO_UDP = 17;
    private const int SOL_SOCKET = 0xFFFF;
    private const int SO_REUSEADDR = 0x0004;
    private const int SOCKET_ERROR = -1;
    private const int WSAEWOULDBLOCK = 10035;
    private const uint INADDR_LOOPBACK = 0x7F000001U;
    private static readonly nuint INVALID_SOCKET = nuint.MaxValue;
    private static readonly nint INVALID_HANDLE_VALUE = new(-1);

    private static readonly byte[] inbuf = new byte[4096];
    private static int keep_running = 1;
    private static int winsock_state;

    public static ulong socket_dgram_harness_timer;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct timeval {
        public int tv_sec;
        public int tv_usec;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct fd_set {
        public uint fd_count;
        public fixed ulong fd_array[64];
    }

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int WSAStartup(ushort wVersionRequested, nint lpWSAData);

    [DllImport("ws2_32.dll")]
    private static extern int WSAGetLastError();

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern nuint socket(int af, int type, int protocol);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int bind(nuint s, ref sockaddr_in name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int setsockopt(nuint s, int level, int optname, ref int optval, int optlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int recvfrom(
        nuint s,
        byte* buf,
        int len,
        int flags,
        ref sockaddr_in from,
        ref int fromlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int select(int nfds, ref fd_set readfds, nint writefds, nint exceptfds, ref timeval timeout);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int closesocket(nuint s);

    [DllImport("kernel32.dll")]
    private static extern void GetSystemTimeAsFileTime(out FILETIME lpSystemTimeAsFileTime);

    [DllImport("kernel32.dll")]
    private static extern void Sleep(uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        nint lpOverlapped);

    private static int ensure_winsock() {
        int observed = Interlocked.CompareExchange(ref winsock_state, 1, 0);
        if (observed == 0) {
            byte* data = stackalloc byte[512];
            if (WSAStartup(0x0202, (nint)data) != 0) {
                Interlocked.Exchange(ref winsock_state, -1);
                return -1;
            }
            Interlocked.Exchange(ref winsock_state, 2);
            return 0;
        }
        while ((observed = Volatile.Read(ref winsock_state)) == 1)
            Sleep(1);
        return observed == 2 ? 0 : -1;
    }

    private static ushort socket_name_port(string? name) {
        uint hash = 2166136261U;
        byte[] text = Encoding.UTF8.GetBytes(name ?? "tkfaxenginefx");
        foreach (byte value in text) {
            hash ^= value;
            hash = unchecked(hash * 16777619U);
        }
        return unchecked((ushort)(22000U + hash % 18000U));
    }

    public static ulong now_us() {
        GetSystemTimeAsFileTime(out FILETIME ft);
        ulong value = ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        return (value - 116444736000000000UL) / 10UL;
    }

    public static int socket_dgram_harness_terminal_write(object? user_data, ReadOnlySpan<byte> buf) {
        socket_dgram_harness_state_t? s = user_data as socket_dgram_harness_state_t;
        uint written = 0;
        if (s is null || s.pty_fd == 0 || s.pty_fd == INVALID_HANDLE_VALUE)
            return -1;
        fixed (byte* raw = buf) {
            if (!WriteFile(s.pty_fd, raw, unchecked((uint)buf.Length), out written, 0))
                return -1;
        }
        return unchecked((int)written);
    }

    public static int socket_dgram_harness_run(socket_dgram_harness_state_t? s) {
        if (s is null || s.net_fd == INVALID_SOCKET)
            return -1;
        Interlocked.Exchange(ref keep_running, 1);
        while (Volatile.Read(ref keep_running) != 0) {
            ulong now = now_us();
            ulong waiter = 100000;
            if (socket_dgram_harness_timer != 0)
                waiter = socket_dgram_harness_timer > now ? socket_dgram_harness_timer - now : 1;
            if (waiter > 100000)
                waiter = 100000;
            timeval tmo = new() {
                tv_sec = unchecked((int)(waiter / 1000000)),
                tv_usec = unchecked((int)(waiter % 1000000))
            };
            fd_set rset = default;
            rset.fd_count = 1;
            rset.fd_array[0] = unchecked((ulong)s.net_fd);
            int ret = select(0, ref rset, 0, 0, ref tmo);
            if (ret == SOCKET_ERROR)
                return -1;
            if (ret == 0) {
                if (s.timer_callback is not null)
                    s.timer_callback(s.user_data);
                continue;
            }
            if (rset.fd_count > 0) {
                sockaddr_in source = default;
                int source_len = sizeof(sockaddr_in);
                int len;
                fixed (byte* raw = inbuf) {
                    len = recvfrom(s.net_fd, raw, inbuf.Length, 0, ref source, ref source_len);
                }
                if (len == SOCKET_ERROR) {
                    int error = WSAGetLastError();
                    if (error != WSAEWOULDBLOCK)
                        return -1;
                } else if (len > 0 && s.rx_callback is not null) {
                    s.rx_callback(s.user_data, inbuf.AsSpan(0, len));
                }
            }

            if (Volatile.Read(ref keep_running) != 0 &&
                socket_dgram_harness_timer != 0 &&
                now_us() >= socket_dgram_harness_timer &&
                s.timer_callback is not null) {
                s.timer_callback(s.user_data);
            }
        }
        return 0;
    }

    public static void socket_dgram_harness_request_stop() {
        Interlocked.Exchange(ref keep_running, 0);
    }

    public static int socket_dgram_harness_set_user_data(socket_dgram_harness_state_t? s, object? user_data) {
        if (s is null)
            return -1;
        s.user_data = user_data;
        return 0;
    }

    public static socket_dgram_harness_state_t? socket_dgram_harness_init(
        socket_dgram_harness_state_t? s,
        string? local_socket_name,
        string? far_socket_name,
        string? tag,
        int caller,
        span_put_msg_func_t? terminal_callback,
        termio_update_func_t? termios_callback,
        span_modem_status_func_t? hangup_callback,
        put_msg_free_space_func_t? terminal_free_space_callback,
        span_put_msg_func_t? rx_callback,
        span_get_msg_func_t? tx_callback,
        span_timer_handler_t? timer_callback,
        object? user_data) {
        int reuse = 1;
        _ = tag;
        _ = caller;
        if (ensure_winsock() < 0)
            return null;

        s ??= new socket_dgram_harness_state_t();
        s.user_data = null;
        s.terminal_callback = null;
        s.termios_callback = null;
        s.hangup_callback = null;
        s.terminal_free_space_callback = null;
        s.rx_callback = null;
        s.tx_callback = null;
        s.timer_callback = null;
        s.net_fd = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
        s.pty_fd = INVALID_HANDLE_VALUE;
        s.logging = new SpanLogState();
        s.termios = new termios();
        s.local_addr = default;
        s.local_addr_len = 0;
        s.far_addr = default;
        s.far_addr_len = 0;
        s.delay = 0;
        s.started = 0;
        s.pty_closed = 0;
        s.close_count = 0;
        s.pty = new pseudo_terminal_state_t();

        if (s.net_fd == INVALID_SOCKET)
            goto fail;
        _ = setsockopt(s.net_fd, SOL_SOCKET, SO_REUSEADDR, ref reuse, sizeof(int));
        s.local_addr.sin_family = AF_INET;
        s.local_addr.sin_addr = htonl(INADDR_LOOPBACK);
        s.local_addr.sin_port = htons(socket_name_port(local_socket_name));
        s.local_addr_len = sizeof(sockaddr_in);
        s.far_addr.sin_family = AF_INET;
        s.far_addr.sin_addr = htonl(INADDR_LOOPBACK);
        s.far_addr.sin_port = htons(socket_name_port(far_socket_name));
        s.far_addr_len = sizeof(sockaddr_in);
        if (bind(s.net_fd, ref s.local_addr, s.local_addr_len) == SOCKET_ERROR)
            goto fail;

        s.terminal_callback = terminal_callback;
        s.termios_callback = termios_callback;
        s.hangup_callback = hangup_callback;
        s.terminal_free_space_callback = terminal_free_space_callback;
        s.rx_callback = rx_callback;
        s.tx_callback = tx_callback;
        s.timer_callback = timer_callback;
        s.user_data = user_data;
        if (pseudo_terminal_init(s.pty) is not null)
            s.pty_fd = s.pty.master_fd;
        return s;

    fail:
        if (s.net_fd != INVALID_SOCKET)
            closesocket(s.net_fd);
        pseudo_terminal_release(s.pty);
        return null;
    }

    public static int socket_dgram_harness_release(socket_dgram_harness_state_t? s) {
        if (s is null)
            return 0;
        if (s.net_fd != INVALID_SOCKET) {
            closesocket(s.net_fd);
            s.net_fd = INVALID_SOCKET;
        }
        pseudo_terminal_release(s.pty);
        s.pty_fd = INVALID_HANDLE_VALUE;
        return 0;
    }

    public static int socket_dgram_harness_free(socket_dgram_harness_state_t? s) {
        if (s is null)
            return 0;
        socket_dgram_harness_release(s);
        return 0;
    }

    private static ushort htons(ushort value) {
        return unchecked((ushort)((value << 8) | (value >> 8)));
    }

    private static uint htonl(uint value) {
        return ((value & 0x000000FFU) << 24) |
               ((value & 0x0000FF00U) << 8) |
               ((value & 0x00FF0000U) >> 8) |
               ((value & 0xFF000000U) >> 24);
    }
}
