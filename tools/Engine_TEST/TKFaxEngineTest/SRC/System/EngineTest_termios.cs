/*
 * Direct C# port of TKFX_termios.h.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public sealed class termios {
    public uint c_iflag;
    public uint c_oflag;
    public uint c_cflag;
    public uint c_lflag;
    public uint c_ispeed;
    public uint c_ospeed;
}

public static class TKFX_termios {
    public const uint B0 = 0U;
    public const uint B9600 = 9600U;
    public const uint B115200 = 115200U;
    public const uint HUPCL = 0x00000400U;
    public const int TCSANOW = 0;
    public const int TCOFLUSH = 1;

    public static uint cfgetispeed(termios? t) {
        return t is not null ? t.c_ispeed : B0;
    }

    public static uint cfgetospeed(termios? t) {
        return t is not null ? t.c_ospeed : B0;
    }

    public static int cfsetispeed(termios? t, uint speed) {
        if (t is null)
            return -1;
        t.c_ispeed = speed;
        return 0;
    }

    public static int cfsetospeed(termios? t, uint speed) {
        if (t is null)
            return -1;
        t.c_ospeed = speed;
        return 0;
    }

    public static void cfmakeraw(termios? t) {
        if (t is null)
            return;
        t.c_iflag = 0;
        t.c_oflag = 0;
        t.c_lflag = 0;
    }

    public static int tcgetattr(int fd, termios? t) {
        _ = fd;
        if (t is null)
            return -1;
        return 0;
    }

    public static int tcsetattr(int fd, int actions, termios? t) {
        _ = fd;
        _ = actions;
        return t is not null ? 0 : -1;
    }

    public static int tcflush(int fd, int queue_selector) {
        _ = fd;
        _ = queue_selector;
        return 0;
    }
}
