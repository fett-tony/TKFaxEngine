/* Native Windows self-test for pseudo_terminals.c. */

#nullable enable

using System.Runtime.InteropServices;
using TKFaxEngineTest.System;
using static TKFaxEngineTest.System.TKFX_termios;
using static TKFaxEngineTest.System.pseudo_terminals;

namespace TKFaxEngineTest.Utility.Test;

public static unsafe class PseudoTerminalTests {
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        nint lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead,
        nint lpOverlapped);

    private static int write_exact(nint handle, ReadOnlySpan<byte> buffer, uint length) {
        uint remaining = length;
        fixed (byte* start = buffer) {
            byte* cursor = start;
            while (remaining > 0) {
                uint written = 0;
                if (!WriteFile(handle, cursor, remaining, out written, 0) || written == 0)
                    return -1;
                cursor += checked((int)written);
                remaining -= written;
            }
        }
        return 0;
    }

    private static int read_exact(nint handle, Span<byte> buffer, uint length) {
        uint remaining = length;
        fixed (byte* start = buffer) {
            byte* cursor = start;
            while (remaining > 0) {
                uint got = 0;
                if (!ReadFile(handle, cursor, remaining, out got, 0) || got == 0)
                    return -1;
                cursor += checked((int)got);
                remaining -= got;
            }
        }
        return 0;
    }

    public static int tkfx_pseudo_terminal_tests_body_main(string[] testArguments) {
        _ = testArguments;
        pseudo_terminal_state_t[] pty = new pseudo_terminal_state_t[10];
        int i;
        int round;

        for (i = 0; i < 10; ++i) {
            pty[i] = new pseudo_terminal_state_t();
            if (pseudo_terminal_init(pty[i]) is null) {
                Console.Error.WriteLine("pseudo terminal {0} could not be initialized", i);
                return 2;
            }
            if (cfgetispeed(pty[i].termios) != B115200 ||
                cfgetospeed(pty[i].termios) != B115200) {
                Console.Error.WriteLine("pseudo terminal {0} has wrong initial speed", i);
                return 2;
            }
            cfsetispeed(pty[i].termios, B9600);
            cfsetospeed(pty[i].termios, B9600);
            pty[i].termios.c_cflag &= ~HUPCL;
            if (pseudo_terminal_check_termios(pty[i]) < 0) {
                Console.Error.WriteLine("pseudo terminal {0} rejected active DTR state", i);
                return 2;
            }
            Console.WriteLine(pty[i].devlink ?? "(unnamed)");
        }

        for (round = 0; round < 10; ++round) {
            for (i = 0; i < 10; ++i) {
                byte[] request = [(byte)'F', (byte)'R', (byte)'E', (byte)'D'];
                byte[] received = new byte[4];
                byte[] response = [(byte)'A', (byte)'B', (byte)('C' + i), (byte)('D' + round)];
                byte[] echoed = new byte[4];
                if (write_exact(pty[i].slave_fd, request, (uint)request.Length) < 0 ||
                    read_exact(pty[i].master_fd, received, (uint)received.Length) < 0 ||
                    !received.AsSpan().SequenceEqual(request)) {
                    Console.Error.WriteLine(
                        "pseudo terminal {0} master receive mismatch in round {1}",
                        i,
                        round);
                    return 2;
                }
                if (write_exact(pty[i].master_fd, response, (uint)response.Length) < 0 ||
                    read_exact(pty[i].slave_fd, echoed, (uint)echoed.Length) < 0 ||
                    !echoed.AsSpan().SequenceEqual(response)) {
                    Console.Error.WriteLine(
                        "pseudo terminal {0} slave receive mismatch in round {1}",
                        i,
                        round);
                    return 2;
                }
            }
        }

        for (i = 0; i < 10; ++i) {
            cfsetispeed(pty[i].termios, B0);
            cfsetospeed(pty[i].termios, B0);
            pty[i].termios.c_cflag |= HUPCL;
            if (pseudo_terminal_check_termios(pty[i]) >= 0 || pty[i].dtr) {
                Console.Error.WriteLine("pseudo terminal {0} did not drop DTR at B0", i);
                return 2;
            }
            if (pseudo_terminal_release(pty[i]) != 0) {
                Console.Error.WriteLine("pseudo terminal {0} release failed", i);
                return 2;
            }
        }

        Console.WriteLine("Tests passed");
        return 0;
    }
}
