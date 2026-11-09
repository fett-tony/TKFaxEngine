/* Native Windows pseudo-terminal compatibility for the restored spanDSP tests. */

#nullable enable

using System.Runtime.InteropServices;
using TKFaxEngine;
using static TKFaxEngine.LoggingApi;
using static TKFaxEngineTest.System.TKFX_termios;

namespace TKFaxEngineTest.System;

public sealed class pseudo_terminal_state_t {
    public int slot;
    public nint master_fd;
    public nint slave_fd;
    public nint threadAbort;
    public nint connectThread;
    public int connected;
    public string? stty;
    public string? devlink;
    public int block_read;
    public int block_write;
    public bool dtr;
    public termios termios = new();
    public SpanLogState logging = new();
}

public static unsafe class pseudo_terminals {
    public const string pseudo_terminal_device_root_name = @"\\.\pipe\TKFaxEngineFX";

    private static readonly nint INVALID_HANDLE_VALUE = new(-1);
    private const uint PIPE_ACCESS_DUPLEX = 0x00000003;
    private const uint PIPE_TYPE_BYTE = 0x00000000;
    private const uint PIPE_READMODE_BYTE = 0x00000000;
    private const uint PIPE_WAIT = 0x00000000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint OPEN_EXISTING = 3;
    private const int ERROR_PIPE_CONNECTED = 535;
    private const int ERROR_PIPE_NOT_CONNECTED = 233;
    private const uint TRUE = 1;
    private const uint WAIT_OBJECT_0 = 0;
    private static int next_slot;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint LPTHREAD_START_ROUTINE(nint user_data);

    private static readonly LPTHREAD_START_ROUTINE external_connect_thread_proc =
        pseudo_terminal_external_connect_thread;

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint CreateNamedPipeA(
        string lpName,
        uint dwOpenMode,
        uint dwPipeMode,
        uint nMaxInstances,
        uint nOutBufferSize,
        uint nInBufferSize,
        uint nDefaultTimeOut,
        nint lpSecurityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint CreateFileA(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConnectNamedPipe(nint hNamedPipe, nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DisconnectNamedPipe(nint hNamedPipe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    private static extern nint CreateEventA(
        nint lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetEvent(nint hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ResetEvent(nint hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateThread(
        nint lpThreadAttributes,
        nuint dwStackSize,
        LPTHREAD_START_ROUTINE lpStartAddress,
        nint lpParameter,
        uint dwCreationFlags,
        nint lpThreadId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelSynchronousIo(nint hThread);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    private static string? duplicate_string(string? text) {
        if (text is null)
            return null;
        return new string(text.AsSpan());
    }

    private static int create_pipe_pair(pseudo_terminal_state_t s) {
        string pipe_name = string.Format(
            global::System.Globalization.CultureInfo.InvariantCulture,
            @"\\.\pipe\TKFaxEngineFX_pty_{0}_{1}_{2}",
            GetCurrentProcessId(),
            s.slot,
            Interlocked.Increment(ref next_slot));

        nint server = CreateNamedPipeA(
            pipe_name,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT,
            1,
            65536,
            65536,
            0,
            0);
        if (server == INVALID_HANDLE_VALUE)
            return -1;

        nint client = CreateFileA(
            pipe_name,
            GENERIC_READ | GENERIC_WRITE,
            0,
            0,
            OPEN_EXISTING,
            0,
            0);
        if (client == INVALID_HANDLE_VALUE) {
            CloseHandle(server);
            return -1;
        }

        bool connected = ConnectNamedPipe(server, 0);
        if (!connected) {
            int error = Marshal.GetLastWin32Error();
            if (error != ERROR_PIPE_CONNECTED) {
                CloseHandle(client);
                CloseHandle(server);
                return -1;
            }
        }

        s.master_fd = server;
        s.slave_fd = client;
        Interlocked.Exchange(ref s.connected, 1);
        s.devlink = duplicate_string(pipe_name);
        s.stty = duplicate_string(pipe_name);
        if (s.devlink is null || s.stty is null) {
            pseudo_terminal_release(s);
            return -1;
        }

        s.threadAbort = CreateEventA(0, true, false, null);
        if (s.threadAbort == 0) {
            pseudo_terminal_release(s);
            return -1;
        }

        s.termios = new termios();
        cfsetispeed(s.termios, B115200);
        cfsetospeed(s.termios, B115200);
        s.dtr = true;
        return 0;
    }

    private static uint pseudo_terminal_external_connect_thread(nint user_data) {
        GCHandle handle = GCHandle.FromIntPtr(user_data);
        try {
            if (handle.Target is not pseudo_terminal_state_t s || s.master_fd == INVALID_HANDLE_VALUE)
                return 1;
            bool connected = ConnectNamedPipe(s.master_fd, 0);
            if (!connected) {
                int error = Marshal.GetLastWin32Error();
                if (error != ERROR_PIPE_CONNECTED)
                    return unchecked((uint)error);
            }
            Interlocked.Exchange(ref s.connected, 1);
            return 0;
        } finally {
            handle.Free();
        }
    }

    public static int pseudo_terminal_listen_external(pseudo_terminal_state_t? s) {
        if (s is null || s.master_fd == INVALID_HANDLE_VALUE)
            return -1;
        if (s.connectThread != 0)
            return -1;
        if (s.slave_fd != 0 && s.slave_fd != INVALID_HANDLE_VALUE) {
            CloseHandle(s.slave_fd);
            s.slave_fd = INVALID_HANDLE_VALUE;
        }
        if (!DisconnectNamedPipe(s.master_fd)) {
            int error = Marshal.GetLastWin32Error();
            if (error != ERROR_PIPE_NOT_CONNECTED)
                return -1;
        }
        Interlocked.Exchange(ref s.connected, 0);
        if (s.threadAbort != 0)
            ResetEvent(s.threadAbort);
        GCHandle handle = GCHandle.Alloc(s);
        nint parameter = GCHandle.ToIntPtr(handle);
        s.connectThread = CreateThread(0, 0, external_connect_thread_proc, parameter, 0, 0);
        if (s.connectThread == 0) {
            handle.Free();
            return -1;
        }
        return 0;
    }

    public static SpanLogState? pseudo_terminal_get_logging_state(pseudo_terminal_state_t? s) {
        return s?.logging;
    }

    public static int pseudo_terminal_check_termios(pseudo_terminal_state_t? s) {
        if (s is null)
            return -1;
        if (cfgetispeed(s.termios) == B0 || cfgetospeed(s.termios) == B0) {
            s.dtr = false;
            return -1;
        }
        s.dtr = true;
        return 0;
    }

    public static int pseudo_terminal_release(pseudo_terminal_state_t? s) {
        if (s is null)
            return 0;
        if (s.threadAbort != 0 && s.threadAbort != INVALID_HANDLE_VALUE)
            SetEvent(s.threadAbort);
        if (s.connectThread != 0 && s.connectThread != INVALID_HANDLE_VALUE)
            CancelSynchronousIo(s.connectThread);
        if (s.slave_fd != 0 && s.slave_fd != INVALID_HANDLE_VALUE) {
            CloseHandle(s.slave_fd);
            s.slave_fd = INVALID_HANDLE_VALUE;
        }
        if (s.master_fd != 0 && s.master_fd != INVALID_HANDLE_VALUE) {
            DisconnectNamedPipe(s.master_fd);
            CloseHandle(s.master_fd);
            s.master_fd = INVALID_HANDLE_VALUE;
        }
        if (s.connectThread != 0 && s.connectThread != INVALID_HANDLE_VALUE) {
            _ = WaitForSingleObject(s.connectThread, 1000);
            CloseHandle(s.connectThread);
            s.connectThread = 0;
        }
        if (s.threadAbort != 0 && s.threadAbort != INVALID_HANDLE_VALUE) {
            CloseHandle(s.threadAbort);
            s.threadAbort = 0;
        }
        Interlocked.Exchange(ref s.connected, 0);
        s.devlink = null;
        s.stty = null;
        return 0;
    }

    public static int pseudo_terminal_free(pseudo_terminal_state_t? s) {
        if (s is null)
            return 0;
        pseudo_terminal_release(s);
        return 0;
    }

    public static int pseudo_terminal_restart(pseudo_terminal_state_t? s) {
        if (s is null)
            return -1;
        pseudo_terminal_release(s);
        s.master_fd = INVALID_HANDLE_VALUE;
        s.slave_fd = INVALID_HANDLE_VALUE;
        return create_pipe_pair(s);
    }

    public static pseudo_terminal_state_t? pseudo_terminal_init(pseudo_terminal_state_t? s) {
        s ??= new pseudo_terminal_state_t();

        s.slot = 0;
        s.master_fd = INVALID_HANDLE_VALUE;
        s.slave_fd = INVALID_HANDLE_VALUE;
        s.threadAbort = 0;
        s.connectThread = 0;
        s.connected = 0;
        s.stty = null;
        s.devlink = null;
        s.block_read = 0;
        s.block_write = 0;
        s.dtr = false;
        s.termios = new termios();
        s.logging = span_log_init(null, SPAN_LOG_NONE, null!);
        s.slot = Interlocked.Increment(ref next_slot) - 1;
        span_log_set_protocol(s.logging, "PTY");
        if (create_pipe_pair(s) < 0) {
            pseudo_terminal_release(s);
            return null;
        }
        return s;
    }
}
