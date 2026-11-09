/*
 * Managed C# equivalents of time.h and sys/time.h for TKFaxEngine tests.
 * The former time.cs and sys_time.cs implementations are intentionally
 * combined here so that timeval has exactly one definition.
 */

#nullable enable

using System.Runtime.InteropServices;

namespace TKFaxEngineTest.System;

[StructLayout(LayoutKind.Sequential)]
public struct timeval
{
    public long tv_sec;
    public long tv_usec;

    public timeval(long tvSec, long tvUsec)
    {
        tv_sec = tvSec;
        tv_usec = tvUsec;
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct timezone
{
    public int tz_minuteswest;
    public int tz_dsttime;
}

public static class time
{
    public static timeval gettimeofday()
    {
        _ = sys_time.gettimeofday(out timeval value);
        return value;
    }

    public static void gettimeofday(out timeval value, object? timezone)
    {
        _ = sys_time.gettimeofday(out value, timezone);
    }
}

public static class sys_time
{
    private static readonly global::System.DateTimeOffset UnixEpoch = global::System.DateTimeOffset.UnixEpoch;

    public static int gettimeofday(out timeval tv, object? tz = null)
    {
        global::System.DateTimeOffset now = global::System.DateTimeOffset.UtcNow;
        long ticks = (now - UnixEpoch).Ticks;

        tv = new timeval(
            ticks / global::System.TimeSpan.TicksPerSecond,
            (ticks % global::System.TimeSpan.TicksPerSecond) / 10L);

        _ = tz;
        return 0;
    }

    public static long GetUnixTimeMicroseconds()
    {
        _ = gettimeofday(out timeval value);
        return checked(value.tv_sec * 1_000_000L + value.tv_usec);
    }
}
