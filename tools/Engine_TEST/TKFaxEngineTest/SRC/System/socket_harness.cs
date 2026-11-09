/* Native Windows/MSVC stream socket harness for the restored spanDSP tests. */

#nullable enable

using System.Runtime.InteropServices;
using System.Text;
using TKFaxEngine;
using static TKFaxEngineTest.System.pseudo_terminals;

namespace TKFaxEngineTest.System;

public delegate void span_put_msg_func_t(object? user_data, ReadOnlySpan<byte> msg);
public delegate int span_get_msg_func_t(object? user_data, Span<byte> msg, int len);
public delegate int termio_update_func_t(object? user_data, termios termios);
public delegate void span_modem_status_func_t(object? user_data, int status);
public delegate int put_msg_free_space_func_t(object? user_data);
public delegate int span_rx_handler_t(object? user_data, short[] amp, int samples);
public delegate int span_rx_fillin_handler_t(object? user_data, int samples);
public delegate int span_tx_handler_t(object? user_data, short[] amp, int samples);
public delegate void span_timer_handler_t(object? user_data);

public unsafe struct sockaddr_in {
    public ushort sin_family;
    public ushort sin_port;
    public uint sin_addr;
    public fixed byte sin_zero[8];
}

public sealed class socket_harness_state_t {
    public object? user_data;
    public span_put_msg_func_t? terminal_callback;
    public termio_update_func_t? termios_callback;
    public span_modem_status_func_t? hangup_callback;
    public put_msg_free_space_func_t? terminal_free_space_callback;
    public span_rx_handler_t? rx_callback;
    public span_rx_fillin_handler_t? rx_fillin_callback;
    public span_tx_handler_t? tx_callback;
    public span_timer_handler_t? timer_callback;
    public nuint net_fd;
    public nint pty_fd;
    public SpanLogState logging = new();
    public termios termios = new();
    public uint delay;
    public uint started;
    public uint pty_closed;
    public uint close_count;
    public pseudo_terminal_state_t pty = new();
}

public static unsafe class socket_harness {
    private const int CLOSE_COUNT_MAX = 100;
    private const int SOCKET_HARNESS_AUDIO_BLOCK_SAMPLES = 160;
    private const int SOCKET_HARNESS_AUDIO_BLOCK_BYTES = SOCKET_HARNESS_AUDIO_BLOCK_SAMPLES * 2;
    private const int SOCKET_HARNESS_AUDIO_BLOCK_MS = 20;
    private const int SOCKET_HARNESS_POLL_MS = 5;

    private const int AF_INET = 2;
    private const int SOCK_STREAM = 1;
    private const int IPPROTO_TCP = 6;
    private const int SOL_SOCKET = 0xFFFF;
    private const int SO_REUSEADDR = 0x0004;
    private const int TCP_NODELAY = 0x0001;
    private const int SD_BOTH = 2;
    private const int SOCKET_ERROR = -1;
    private const int WSAEINTR = 10004;
    private const int WSAECONNREFUSED = 10061;
    private const int WSAETIMEDOUT = 10060;
    private const uint INADDR_LOOPBACK = 0x7F000001U;
    private static readonly nuint INVALID_SOCKET = nuint.MaxValue;
    private static readonly nint INVALID_HANDLE_VALUE = new(-1);
    private const int ERROR_BROKEN_PIPE = 109;
    private const int ERROR_PIPE_NOT_CONNECTED = 233;

    private static readonly short[] inbuf = new short[4096];
    private static readonly short[] outbuf = new short[4096];
    private static readonly byte[] network_inbuf = new byte[4096 * sizeof(short) + 1];
    private static int keep_running = 1;
    private static int winsock_state;

    public static ulong socket_harness_timer;

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
    private static extern int connect(nuint s, ref sockaddr_in name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int bind(nuint s, ref sockaddr_in name, int namelen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int listen(nuint s, int backlog);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern nuint accept(nuint s, nint addr, nint addrlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int setsockopt(nuint s, int level, int optname, ref int optval, int optlen);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int send(nuint s, byte* buf, int len, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int recv(nuint s, byte* buf, int len, int flags);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int select(int nfds, ref fd_set readfds, nint writefds, nint exceptfds, ref timeval timeout);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int shutdown(nuint s, int how);

    [DllImport("ws2_32.dll", SetLastError = true)]
    private static extern int closesocket(nuint s);

    [DllImport("kernel32.dll")]
    private static extern void GetSystemTimeAsFileTime(out FILETIME lpSystemTimeAsFileTime);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [DllImport("kernel32.dll")]
    private static extern void Sleep(uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(
        nint hNamedPipe,
        nint lpBuffer,
        uint nBufferSize,
        nint lpBytesRead,
        out uint lpTotalBytesAvail,
        nint lpBytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        nint lpOverlapped);

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

    private static ulong socket_harness_now_us() {
        GetSystemTimeAsFileTime(out FILETIME ft);
        ulong value = ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
        return (value - 116444736000000000UL) / 10UL;
    }

    private static int socket_send_all(nuint socket_handle, ReadOnlySpan<byte> data, int length) {
        int offset = 0;
        fixed (byte* start = data) {
            while (offset < length) {
                int sent = send(socket_handle, start + offset, length - offset, 0);
                if (sent == SOCKET_ERROR) {
                    int error = WSAGetLastError();
                    if (error == WSAEINTR)
                        continue;
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS send failed win32={0} offset={1} length={2}",
                        error,
                        offset,
                        length);
                    return -1;
                }
                if (sent == 0) {
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS send returned zero offset={0} length={1}",
                        offset,
                        length);
                    return -1;
                }
                offset += sent;
            }
        }
        return 0;
    }

    private static int configure_stream_socket(nuint socket_handle, string role) {
        int no_delay = 1;
        if (setsockopt(socket_handle, IPPROTO_TCP, TCP_NODELAY, ref no_delay, sizeof(int)) == SOCKET_ERROR) {
            Console.Error.WriteLine(
                "ERROR SOCKET_HARNESS TCP_NODELAY failed role={0} win32={1}",
                role,
                WSAGetLastError());
            return -1;
        }
        return 0;
    }

    private static int pty_write(nint h, ReadOnlySpan<byte> buf, int len) {
        int offset = 0;
        if (h == 0 || h == INVALID_HANDLE_VALUE || (buf.IsEmpty && len != 0))
            return -1;
        fixed (byte* start = buf) {
            while (offset < len) {
                uint written = 0;
                uint remaining = unchecked((uint)(len - offset));
                if (!WriteFile(h, start + offset, remaining, out written, 0)) {
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS PTY write failed win32={0} offset={1} length={2}",
                        Marshal.GetLastWin32Error(),
                        offset,
                        len);
                    return -1;
                }
                if (written == 0) {
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS PTY write returned zero offset={0} length={1}",
                        offset,
                        len);
                    return -1;
                }
                offset += unchecked((int)written);
            }
        }
        return offset;
    }

    public static int socket_harness_terminal_write(object? user_data, ReadOnlySpan<byte> buf) {
        socket_harness_state_t s = (socket_harness_state_t)user_data!;
        return pty_write(s.pty_fd, buf, buf.Length);
    }

    public static void socket_harness_request_stop() {
        Interlocked.Exchange(ref keep_running, 0);
    }

    public static int socket_harness_run(socket_harness_state_t? s, int kick) {
        int receive_bytes = 0;
        if (s is null || s.net_fd == INVALID_SOCKET)
            return -1;

        Interlocked.Exchange(ref keep_running, 1);
        ulong next_tx_ms = GetTickCount64() + (kick != 0 ? 0UL : SOCKET_HARNESS_AUDIO_BLOCK_MS);

        while (Volatile.Read(ref keep_running) != 0) {
            if (s.pty_fd != 0 &&
                s.pty_fd != INVALID_HANDLE_VALUE &&
                s.pty_closed == 0 &&
                s.terminal_free_space_callback is not null) {
                uint available = 0;
                if (!PeekNamedPipe(s.pty_fd, 0, 0, 0, out available, 0)) {
                    int error = Marshal.GetLastWin32Error();
                    if (error != ERROR_BROKEN_PIPE && error != ERROR_PIPE_NOT_CONNECTED) {
                        Console.Error.WriteLine(
                            "ERROR SOCKET_HARNESS PeekNamedPipe failed win32={0}",
                            error);
                        return -1;
                    }
                    s.pty_closed = 1;
                } else if (available > 0) {
                    uint read_bytes = 0;
                    int free_space = s.terminal_free_space_callback(s.user_data);
                    if (free_space > inbuf.Length * sizeof(short))
                        free_space = inbuf.Length * sizeof(short);
                    if ((uint)free_space > available)
                        free_space = unchecked((int)available);
                    if (free_space > 0) {
                        fixed (short* raw = inbuf) {
                            if (!ReadFile(s.pty_fd, (byte*)raw, unchecked((uint)free_space), out read_bytes, 0)) {
                                Console.Error.WriteLine(
                                    "ERROR SOCKET_HARNESS PTY read failed win32={0}",
                                    Marshal.GetLastWin32Error());
                                return -1;
                            }
                            if (read_bytes > 0)
                                s.terminal_callback!(s.user_data, new ReadOnlySpan<byte>((byte*)raw, unchecked((int)read_bytes)));
                        }
                    }
                }
            }

            ulong now_ms = GetTickCount64();
            if (now_ms >= next_tx_ms) {
                int samples = SOCKET_HARNESS_AUDIO_BLOCK_SAMPLES;
                int tx_samples = s.tx_callback!(s.user_data, outbuf, samples);
                if (tx_samples < 0 || tx_samples > samples)
                    return -1;
                if (tx_samples < samples)
                    Array.Clear(outbuf, tx_samples, samples - tx_samples);
                fixed (short* raw = outbuf) {
                    if (socket_send_all(
                            s.net_fd,
                            new ReadOnlySpan<byte>((byte*)raw, SOCKET_HARNESS_AUDIO_BLOCK_BYTES),
                            SOCKET_HARNESS_AUDIO_BLOCK_BYTES) != 0)
                        return -1;
                }
                next_tx_ms += SOCKET_HARNESS_AUDIO_BLOCK_MS;
                if (next_tx_ms <= now_ms)
                    next_tx_ms = now_ms + SOCKET_HARNESS_AUDIO_BLOCK_MS;
            }

            now_ms = GetTickCount64();
            uint wait_ms;
            if (next_tx_ms <= now_ms) {
                wait_ms = 0;
            } else {
                ulong remaining_ms = next_tx_ms - now_ms;
                wait_ms = unchecked((uint)(remaining_ms > SOCKET_HARNESS_POLL_MS
                    ? SOCKET_HARNESS_POLL_MS
                    : remaining_ms));
            }

            if (socket_harness_timer != 0) {
                ulong now_us = socket_harness_now_us();
                if (socket_harness_timer <= now_us) {
                    wait_ms = 0;
                } else {
                    ulong timer_wait_us = socket_harness_timer - now_us;
                    uint timer_wait_ms = unchecked((uint)((timer_wait_us + 999) / 1000));
                    if (timer_wait_ms < wait_ms)
                        wait_ms = timer_wait_ms;
                }
            }

            fd_set rset = default;
            rset.fd_count = 1;
            rset.fd_array[0] = unchecked((ulong)s.net_fd);
            timeval tmo = new() {
                tv_sec = unchecked((int)(wait_ms / 1000)),
                tv_usec = unchecked((int)((wait_ms % 1000) * 1000))
            };
            int ret = select(0, ref rset, 0, 0, ref tmo);
            if (ret == SOCKET_ERROR) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS select failed win32={0}",
                    WSAGetLastError());
                return -1;
            }

            if (ret > 0 && rset.fd_count > 0) {
                int count;
                fixed (byte* raw = network_inbuf) {
                    count = recv(
                        s.net_fd,
                        raw + receive_bytes,
                        network_inbuf.Length - receive_bytes,
                        0);
                }
                if (count == 0)
                    return 0;
                if (count == SOCKET_ERROR) {
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS recv failed win32={0}",
                        WSAGetLastError());
                    return -1;
                }
                receive_bytes += count;

                while (receive_bytes >= SOCKET_HARNESS_AUDIO_BLOCK_BYTES) {
                    Buffer.BlockCopy(network_inbuf, 0, inbuf, 0, SOCKET_HARNESS_AUDIO_BLOCK_BYTES);
                    receive_bytes -= SOCKET_HARNESS_AUDIO_BLOCK_BYTES;
                    if (receive_bytes > 0)
                        Buffer.BlockCopy(network_inbuf, SOCKET_HARNESS_AUDIO_BLOCK_BYTES, network_inbuf, 0, receive_bytes);
                    if (s.rx_callback!(s.user_data, inbuf, SOCKET_HARNESS_AUDIO_BLOCK_SAMPLES) < 0)
                        return -1;
                }
            }

            if (s.timer_callback is not null && socket_harness_timer != 0) {
                ulong now_us = socket_harness_now_us();
                if (now_us >= socket_harness_timer)
                    s.timer_callback(s.user_data);
            }
        }
        return 0;
    }

    public static socket_harness_state_t? socket_harness_init(
        socket_harness_state_t? s,
        string? socket_name,
        string? tag,
        int caller,
        span_put_msg_func_t? terminal_callback,
        termio_update_func_t? termios_callback,
        span_modem_status_func_t? hangup_callback,
        put_msg_free_space_func_t? terminal_free_space_callback,
        span_rx_handler_t? rx_callback,
        span_rx_fillin_handler_t? rx_fillin_callback,
        span_tx_handler_t? tx_callback,
        object? user_data) {
        nuint sockfd = INVALID_SOCKET;
        nuint listenfd = INVALID_SOCKET;
        int reuse = 1;
        _ = tag;
        if (ensure_winsock() < 0)
            return null;

        s ??= new socket_harness_state_t();
        s.user_data = null;
        s.terminal_callback = null;
        s.termios_callback = null;
        s.hangup_callback = null;
        s.terminal_free_space_callback = null;
        s.rx_callback = null;
        s.rx_fillin_callback = null;
        s.tx_callback = null;
        s.timer_callback = null;
        s.net_fd = INVALID_SOCKET;
        s.pty_fd = INVALID_HANDLE_VALUE;
        s.logging = new SpanLogState();
        s.termios = new termios();
        s.delay = 0;
        s.started = 0;
        s.pty_closed = 0;
        s.close_count = 0;
        s.pty = new pseudo_terminal_state_t();
        s.terminal_callback = terminal_callback;
        s.termios_callback = termios_callback;
        s.hangup_callback = hangup_callback;
        s.terminal_free_space_callback = terminal_free_space_callback;
        s.rx_callback = rx_callback;
        s.rx_fillin_callback = rx_fillin_callback;
        s.tx_callback = tx_callback;
        s.user_data = user_data;

        sockaddr_in address = default;
        address.sin_family = AF_INET;
        address.sin_addr = htonl(INADDR_LOOPBACK);
        address.sin_port = htons(socket_name_port(socket_name));

        if (caller != 0) {
            sockfd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
            if (sockfd == INVALID_SOCKET) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS caller socket failed port={0} win32={1}",
                    ntohs(address.sin_port),
                    WSAGetLastError());
                goto fail;
            }
            int attempt;
            for (attempt = 0; attempt < 100; ++attempt) {
                int error;
                if (connect(sockfd, ref address, sizeof(sockaddr_in)) == 0)
                    break;
                error = WSAGetLastError();
                if (error != WSAECONNREFUSED && error != WSAETIMEDOUT) {
                    Console.Error.WriteLine(
                        "ERROR SOCKET_HARNESS caller connect failed port={0} attempt={1} win32={2}",
                        ntohs(address.sin_port),
                        attempt + 1,
                        error);
                    goto fail;
                }
                Sleep(50);
            }
            if (attempt == 100) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS caller connect timeout port={0} attempts={1}",
                    ntohs(address.sin_port),
                    attempt);
                goto fail;
            }
        } else {
            listenfd = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
            if (listenfd == INVALID_SOCKET) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS answerer socket failed port={0} win32={1}",
                    ntohs(address.sin_port),
                    WSAGetLastError());
                goto fail;
            }
            _ = setsockopt(listenfd, SOL_SOCKET, SO_REUSEADDR, ref reuse, sizeof(int));
            if (bind(listenfd, ref address, sizeof(sockaddr_in)) == SOCKET_ERROR) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS answerer bind failed port={0} win32={1}",
                    ntohs(address.sin_port),
                    WSAGetLastError());
                goto fail;
            }
            if (listen(listenfd, 1) == SOCKET_ERROR) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS answerer listen failed port={0} win32={1}",
                    ntohs(address.sin_port),
                    WSAGetLastError());
                goto fail;
            }
            sockfd = accept(listenfd, 0, 0);
            if (sockfd == INVALID_SOCKET) {
                Console.Error.WriteLine(
                    "ERROR SOCKET_HARNESS answerer accept failed port={0} win32={1}",
                    ntohs(address.sin_port),
                    WSAGetLastError());
                goto fail;
            }
            closesocket(listenfd);
            listenfd = INVALID_SOCKET;
        }

        if (configure_stream_socket(sockfd, caller != 0 ? "caller" : "answerer") != 0)
            goto fail;

        if (pseudo_terminal_init(s.pty) is null) {
            Console.Error.WriteLine(
                "ERROR SOCKET_HARNESS pseudo terminal initialisation failed role={0}",
                caller != 0 ? "caller" : "answerer");
            goto fail;
        }
        s.pty_fd = s.pty.master_fd;
        if (s.pty_fd == 0 ||
            s.pty_fd == INVALID_HANDLE_VALUE ||
            s.pty.slave_fd == 0 ||
            s.pty.slave_fd == INVALID_HANDLE_VALUE) {
            Console.Error.WriteLine(
                "ERROR SOCKET_HARNESS pseudo terminal handles invalid role={0}",
                caller != 0 ? "caller" : "answerer");
            goto fail;
        }

        s.net_fd = sockfd;
        return s;

    fail:
        if (sockfd != INVALID_SOCKET)
            closesocket(sockfd);
        if (listenfd != INVALID_SOCKET)
            closesocket(listenfd);
        pseudo_terminal_release(s.pty);
        return null;
    }

    public static int socket_harness_release(socket_harness_state_t? s) {
        if (s is null)
            return 0;
        if (s.net_fd != INVALID_SOCKET) {
            _ = shutdown(s.net_fd, SD_BOTH);
            _ = closesocket(s.net_fd);
            s.net_fd = INVALID_SOCKET;
        }
        pseudo_terminal_release(s.pty);
        s.pty_fd = INVALID_HANDLE_VALUE;
        return 0;
    }

    public static int socket_harness_free(socket_harness_state_t? s) {
        if (s is null)
            return 0;
        socket_harness_release(s);
        return 0;
    }

    private static ushort htons(ushort value) {
        return unchecked((ushort)((value << 8) | (value >> 8)));
    }

    private static ushort ntohs(ushort value) {
        return htons(value);
    }

    private static uint htonl(uint value) {
        return ((value & 0x000000FFU) << 24) |
               ((value & 0x0000FF00U) << 8) |
               ((value & 0x00FF0000U) >> 8) |
               ((value & 0xFF000000U) >> 24);
    }
}
