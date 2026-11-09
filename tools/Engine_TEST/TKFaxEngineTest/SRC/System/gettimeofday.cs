/* Managed C# port of the supplied Windows gettimeofday.c. */

#nullable enable

namespace TKFaxEngineTest.System;

public struct Timeval {
    public long tv_sec;
    public long tv_usec;
}

public static class GetTimeOfDayCompat {
    public const ulong DELTA_EPOCH_IN_MICROSECS = 11_644_473_600_000_000UL;

    public static void gettimeofday(ref Timeval tv, object? tz) {
        _ = tz;

        DateTime utcNow = DateTime.UtcNow;
        long fileTimeTicks = utcNow.ToFileTimeUtc();
        long highResolutionTime = fileTimeTicks / 10;

        TimeSpan localOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);
        long timezoneTimeBiasInMinutes = checked(-(long)localOffset.TotalMinutes);

        highResolutionTime -= checked(timezoneTimeBiasInMinutes * 60L * 1_000_000L);
        highResolutionTime -= checked((long)DELTA_EPOCH_IN_MICROSECS);

        tv.tv_sec = highResolutionTime / 1_000_000L;
        tv.tv_usec = highResolutionTime % 1_000_000L;
    }

    public static Timeval gettimeofday(object? tz = null) {
        Timeval tv = default;
        gettimeofday(ref tv, tz);
        return tv;
    }
}
