/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * timezone_tests.c - Timezone handling for time interpretation
 *
 * Managed C# port preserving the supplied spanDSP timezone test sequence.
 */

#nullable enable
using TKFaxEngine;

using static TKFaxEngine.TimezoneApi;

namespace TKFaxEngineTest.Core.Test;

public static class TimezoneTests {
    public sealed class ExitException : Exception {
        public ExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    public static int tkfx_timezone_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        long localTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Tm brokenDownTime = default;

        TzContext timezone = tz_init(null, "GMT0GMT0,M10.5.0,M3.5.0");
        if (tz_localtime(timezone, ref brokenDownTime, localTime) != 0)
            throw new ExitException(2);
        Console.WriteLine(
            $"Local time is {brokenDownTime.tm_hour:D2}:" +
            $"{brokenDownTime.tm_min:D2}:{brokenDownTime.tm_sec:D2}");
        Console.WriteLine($"Time zone is {tz_tzname(timezone, brokenDownTime.tm_isdst)}");

        timezone = tz_init(timezone, "CST-8CST-8,M10.5.0,M3.5.0");
        if (tz_localtime(timezone, ref brokenDownTime, localTime) != 0)
            throw new ExitException(2);
        Console.WriteLine(
            $"Local time is {brokenDownTime.tm_hour:D2}:" +
            $"{brokenDownTime.tm_min:D2}:{brokenDownTime.tm_sec:D2}");
        Console.WriteLine($"Time zone is {tz_tzname(timezone, brokenDownTime.tm_isdst)}");

        timezone = tz_init(timezone, "AEST-10AEDT-11,M10.5.0,M3.5.0");
        if (tz_localtime(timezone, ref brokenDownTime, localTime) != 0)
            throw new ExitException(2);
        Console.WriteLine(
            $"Local time is {brokenDownTime.tm_hour:D2}:" +
            $"{brokenDownTime.tm_min:D2}:{brokenDownTime.tm_sec:D2}");
        Console.WriteLine($"Time zone is {tz_tzname(timezone, brokenDownTime.tm_isdst)}");

        tz_free(timezone);
        return 0;
    }
}
