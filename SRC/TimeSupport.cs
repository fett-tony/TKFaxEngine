/*
 * TKFaxEngine - managed C# port
 *
 * TimeSupport.cs
 *
 * Combined port of:
 *   timezone.h / timezone.c
 *   timing.h
 *   SRC/sys/time.h
 *   gettimeofday.c
 *
 * Original timezone implementation written by Steve Underwood
 * <steveu@coppice.org> and derived from the public-domain timezone code by
 * Arthur David Olson.
 *
 * The original timing and timezone files retain their respective licensing.
 */

using System.Diagnostics;

namespace TKFaxEngine;

/// <summary>
/// Weekday values matching C <c>struct tm.tm_wday</c>.
/// Sunday is zero.
/// </summary>
public enum TmWeekday {
    Sunday = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6
}

/// <summary>
/// Month values matching C <c>struct tm.tm_mon</c>.
/// January is zero.
/// </summary>
public enum TmMonth {
    January = 0,
    February = 1,
    March = 2,
    April = 3,
    May = 4,
    June = 5,
    July = 6,
    August = 7,
    September = 8,
    October = 9,
    November = 10,
    December = 11
}

/// <summary>
/// Managed equivalent of the fields used from C <c>struct tm</c>.
/// The field names intentionally retain the original C names because other
/// converted TKFaxEngine modules can use them without translation.
/// </summary>
public struct Tm {
    public int tm_sec;
    public int tm_min;
    public int tm_hour;
    public int tm_mday;
    public int tm_mon;
    public int tm_year;
    public int tm_wday;
    public int tm_yday;
    public int tm_isdst;

    public readonly DateTime ToUnspecifiedDateTime() {
        int year = checked(tm_year + 1900);
        int month = checked(tm_mon + 1);

        int second = Math.Clamp(tm_sec, 0, 59);
        return new DateTime(
            year,
            month,
            tm_mday,
            tm_hour,
            tm_min,
            second,
            DateTimeKind.Unspecified);
    }

    public override readonly string ToString() {
        return $"{tm_year + 1900:D4}-{tm_mon + 1:D2}-{tm_mday:D2} " +
               $"{tm_hour:D2}:{tm_min:D2}:{tm_sec:D2} " +
               $"DST={tm_isdst}";
    }
}

/// <summary>
/// Managed equivalent of <c>SRC/sys/time.h::struct timeval</c>.
/// </summary>
public struct TimeVal {
    public long tv_sec;
    public long tv_usec;

    public readonly DateTimeOffset ToDateTimeOffset() {
        return DateTimeOffset
            .FromUnixTimeSeconds(tv_sec)
            .AddTicks(tv_usec * 10);
    }
}

/// <summary>
/// Managed port of the Windows implementation from <c>gettimeofday.c</c>.
/// </summary>
/// <remarks>
/// The original implementation obtains a UTC Windows FILETIME, converts it
/// to microseconds, applies the current Windows timezone and daylight-saving
/// bias, and then subtracts the Windows-to-Unix epoch delta. The resulting
/// value therefore represents the local wall clock relative to the Unix
/// epoch, matching the supplied native implementation.
/// </remarks>
public static class SystemTimeApi {
    /// <summary>
    /// Number of microseconds between 1601-01-01 and 1970-01-01.
    /// This corresponds to DELTA_EPOCH_IN_MICROSECS in gettimeofday.c.
    /// </summary>
    public const ulong DeltaEpochInMicroseconds =
        11_644_473_600_000_000UL;

    public static void gettimeofday(ref TimeVal tv, object? timezone = null) {
        _ = timezone;

        DateTime utcNow = DateTime.UtcNow;

        // GetSystemTimeAsFileTime() returns 100-nanosecond intervals since
        // 1601-01-01 UTC. Divide by ten to obtain microseconds.
        long highResolutionTime = utcNow.ToFileTimeUtc() / 10L;

        // Windows TIME_ZONE_INFORMATION.Bias uses the opposite sign of the
        // UTC offset: UTC = local time + bias.
        TimeSpan utcOffset = TimeZoneInfo.Local.GetUtcOffset(utcNow);
        long timezoneTimeBiasInMinutes =
            checked(-(long)utcOffset.TotalMinutes);

        highResolutionTime = checked(
            highResolutionTime -
            timezoneTimeBiasInMinutes * 60L * 1_000_000L -
            (long)DeltaEpochInMicroseconds);

        tv.tv_sec = Math.DivRem(
            highResolutionTime,
            1_000_000L,
            out long microseconds);

        // Keep tv_usec in the conventional range 0..999999, including for
        // timestamps before the Unix epoch.
        if (microseconds < 0) {
            tv.tv_sec--;
            microseconds += 1_000_000L;
        }

        tv.tv_usec = microseconds;
    }

    public static TimeVal gettimeofday() {
        TimeVal result = default;
        gettimeofday(ref result);
        return result;
    }

    /// <summary>
    /// UTC variant with normal POSIX gettimeofday semantics. This keeps the
    /// former managed behavior available to callers that require UTC.
    /// </summary>
    public static void gettimeofday_utc(ref TimeVal tv, object? timezone = null) {
        _ = timezone;

        DateTimeOffset now = DateTimeOffset.UtcNow;
        long seconds = now.ToUnixTimeSeconds();
        long ticksWithinSecond = now.UtcTicks % TimeSpan.TicksPerSecond;

        tv.tv_sec = seconds;
        tv.tv_usec = ticksWithinSecond / 10L;
    }

    public static TimeVal gettimeofday_utc() {
        TimeVal result = default;
        gettimeofday_utc(ref result);
        return result;
    }
}

/// <summary>
/// Managed replacement for timing.h.
/// </summary>
/// <remarks>
/// The native <c>rdtscll()</c> returns the processor timestamp counter.
/// Portable managed code cannot guarantee direct access to the hardware TSC,
/// therefore this implementation returns <see cref="Stopwatch.GetTimestamp"/>.
/// It is monotonic and high resolution, but its unit is
/// <see cref="Stopwatch.Frequency"/> ticks per second rather than CPU cycles.
/// </remarks>
public static class HighResolutionTiming {
    public static long Frequency => Stopwatch.Frequency;

    public static ulong rdtscll() {
        return unchecked((ulong)Stopwatch.GetTimestamp());
    }

    public static TimeSpan Elapsed(ulong start, ulong end) {
        ulong delta = unchecked(end - start);
        double seconds = delta / (double)Stopwatch.Frequency;
        return TimeSpan.FromSeconds(seconds);
    }
}

/// <summary>
/// Managed equivalent of <c>tz_t</c>. It supports the same POSIX-style timezone
/// strings parsed by the supplied timezone.c implementation.
/// </summary>
public sealed class TzContext : IDisposable {
    private PosixZone _zone = PosixZone.Utc;
    private bool _disposed;

    public TzContext(string? timezoneString = null) {
        Initialize(timezoneString);
    }

    public string TimezoneString { get; private set; } = string.Empty;

    public bool IsDisposed => _disposed;

    public void Initialize(string? timezoneString) {
        string value = timezoneString ?? string.Empty;

        TimezoneString = value;
        _zone = PosixZone.ParseOrUtc(value);
        _disposed = false;
    }

    /// <summary>
    /// Converts Unix time to the local broken-down representation configured
    /// for this context.
    /// </summary>
    public int LocalTime(ref Tm result, long unixSeconds) {
        ThrowIfDisposed();

        bool isDst = _zone.IsDaylightTime(unixSeconds);
        int utcOffsetSeconds = isDst
            ? _zone.DaylightUtcOffsetSeconds
            : _zone.StandardUtcOffsetSeconds;

        long adjustedSeconds;
        try {
            adjustedSeconds = checked(unixSeconds + utcOffsetSeconds);
        } catch (OverflowException) {
            return -1;
        }

        DateTime localAsUtc;
        try {
            localAsUtc = DateTimeOffset
                .FromUnixTimeSeconds(adjustedSeconds)
                .UtcDateTime;
        } catch (ArgumentOutOfRangeException) {
            return -1;
        }

        result.tm_sec = localAsUtc.Second;
        result.tm_min = localAsUtc.Minute;
        result.tm_hour = localAsUtc.Hour;
        result.tm_mday = localAsUtc.Day;
        result.tm_mon = localAsUtc.Month - 1;
        result.tm_year = localAsUtc.Year - 1900;
        result.tm_wday = (int)localAsUtc.DayOfWeek;
        result.tm_yday = localAsUtc.DayOfYear - 1;
        result.tm_isdst = isDst ? 1 : 0;

        return 0;
    }

    public Tm LocalTime(long unixSeconds) {
        Tm result = default;
        int status = LocalTime(ref result, unixSeconds);

        if (status != 0)
            throw new ArgumentOutOfRangeException(nameof(unixSeconds));

        return result;
    }

    public string GetTimezoneName(int isDst) {
        ThrowIfDisposed();
        return isDst == 0
            ? _zone.StandardName
            : _zone.DaylightName;
    }

    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _zone = PosixZone.Utc;
        TimezoneString = string.Empty;
        _disposed = true;
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}

/// <summary>
/// Compatibility facade retaining the original C timezone function names.
/// </summary>
public static class TimezoneApi {
    public static TzContext tz_init(
        TzContext? timezone,
        string? timezoneString) {
        if (timezone is null)
            return new TzContext(timezoneString);

        timezone.Initialize(timezoneString);
        return timezone;
    }

    public static int tz_release(TzContext timezone) {
        ArgumentNullException.ThrowIfNull(timezone);
        return timezone.Release();
    }

    public static int tz_free(TzContext? timezone) {
        timezone?.Dispose();
        return 0;
    }

    public static int tz_localtime(
        TzContext timezone,
        ref Tm result,
        long unixSeconds) {
        ArgumentNullException.ThrowIfNull(timezone);
        return timezone.LocalTime(ref result, unixSeconds);
    }

    public static string tz_tzname(
        TzContext timezone,
        int isDst) {
        ArgumentNullException.ThrowIfNull(timezone);
        return timezone.GetTimezoneName(isDst);
    }
}

internal sealed class PosixZone {
    private const int EpochYear = 1970;
    private const int LastGeneratedYear = 2037;
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int SecondsPerHour = SecondsPerMinute * MinutesPerHour;
    private const int SecondsPerDay = SecondsPerHour * HoursPerDay;

    private const string DefaultRuleString = "M4.1.0,M10.5.0";

    private readonly Transition[] _transitions;

    private PosixZone(
        string standardName,
        string daylightName,
        int standardUtcOffsetSeconds,
        int daylightUtcOffsetSeconds,
        Transition[] transitions) {
        StandardName = standardName;
        DaylightName = daylightName;
        StandardUtcOffsetSeconds = standardUtcOffsetSeconds;
        DaylightUtcOffsetSeconds = daylightUtcOffsetSeconds;
        _transitions = transitions;
    }

    public static PosixZone Utc { get; } = new(
        "GMT",
        "GMT",
        0,
        0,
        Array.Empty<Transition>());

    public string StandardName { get; }

    public string DaylightName { get; }

    public int StandardUtcOffsetSeconds { get; }

    public int DaylightUtcOffsetSeconds { get; }

    public static PosixZone ParseOrUtc(string value) {
        if (string.IsNullOrEmpty(value))
            return Utc;

        if (value[0] == ':')
            return Utc;

        return TryParse(value, out PosixZone? zone)
            ? zone
            : Utc;
    }

    public bool IsDaylightTime(long unixSeconds) {
        if (_transitions.Length == 0)
            return false;

        if (unixSeconds < _transitions[0].UnixSeconds)
            return false;

        int low = 0;
        int high = _transitions.Length - 1;

        while (low <= high) {
            int middle = low + ((high - low) >> 1);

            if (_transitions[middle].UnixSeconds <= unixSeconds)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return high >= 0 && _transitions[high].IsDaylightAfter;
    }

    private static bool TryParse(
        string value,
        out PosixZone? zone) {
        zone = null;
        PosixParser parser = new(value);

        if (!parser.TryReadName(out string standardName) ||
            standardName.Length < 3) {
            return false;
        }

        if (!parser.TryReadOffset(out int standardPosixOffset))
            return false;

        int standardUtcOffset = -standardPosixOffset;

        if (parser.AtEnd) {
            zone = new PosixZone(
                standardName,
                "   ",
                standardUtcOffset,
                standardUtcOffset,
                Array.Empty<Transition>());
            return true;
        }

        if (!parser.TryReadName(out string daylightName) ||
            daylightName.Length < 3) {
            return false;
        }

        int daylightPosixOffset;
        if (!parser.AtEnd &&
            parser.Current is not ',' and not ';') {
            if (!parser.TryReadOffset(out daylightPosixOffset))
                return false;
        } else {
            daylightPosixOffset = standardPosixOffset - SecondsPerHour;
        }

        Rule startRule;
        Rule endRule;

        if (parser.AtEnd) {
            PosixParser defaultRules = new(DefaultRuleString);

            if (!defaultRules.TryReadRule(out startRule) ||
                !defaultRules.TryRead(',') ||
                !defaultRules.TryReadRule(out endRule) ||
                !defaultRules.AtEnd) {
                return false;
            }
        } else {
            if (!parser.TryReadOneOf(',', ';'))
                return false;

            if (!parser.TryReadRule(out startRule))
                return false;

            if (!parser.TryRead(','))
                return false;

            if (!parser.TryReadRule(out endRule) ||
                !parser.AtEnd) {
                return false;
            }
        }

        Transition[] transitions = BuildTransitions(
            standardPosixOffset,
            daylightPosixOffset,
            startRule,
            endRule);

        zone = new PosixZone(
            standardName,
            daylightName,
            standardUtcOffset,
            -daylightPosixOffset,
            transitions);

        return true;
    }

    private static Transition[] BuildTransitions(
        int standardPosixOffset,
        int daylightPosixOffset,
        Rule startRule,
        Rule endRule) {
        List<Transition> transitions =
            new((LastGeneratedYear - EpochYear + 1) * 2);

        for (int year = EpochYear; year <= LastGeneratedYear; year++) {
            long start = GetTransitionUnixTime(
                year,
                startRule,
                standardPosixOffset);

            long end = GetTransitionUnixTime(
                year,
                endRule,
                daylightPosixOffset);

            if (start > end) {
                transitions.Add(new Transition(end, false));
                transitions.Add(new Transition(start, true));
            } else {
                transitions.Add(new Transition(start, true));
                transitions.Add(new Transition(end, false));
            }
        }

        return transitions.ToArray();
    }

    private static long GetTransitionUnixTime(
        int year,
        Rule rule,
        int currentPosixOffset) {
        DateTime day = rule.Type switch {
            RuleType.JulianDay =>
                GetJulianRuleDate(year, rule.Day),

            RuleType.DayOfYear =>
                new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddDays(rule.Day),

            RuleType.MonthNthDayOfWeek =>
                GetMonthWeekdayRuleDate(
                    year,
                    rule.Month,
                    rule.Week,
                    rule.Day),

            _ => throw new InvalidOperationException(
                $"Unsupported timezone rule type: {rule.Type}.")
        };

        long dayUnix = new DateTimeOffset(day).ToUnixTimeSeconds();

        return checked(
            dayUnix +
            rule.TransitionSeconds +
            currentPosixOffset);
    }

    private static DateTime GetJulianRuleDate(
        int year,
        int julianDay) {
        int dayOffset = julianDay - 1;

        if (DateTime.IsLeapYear(year) && julianDay >= 60)
            dayOffset++;

        return new DateTime(
            year,
            1,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc).AddDays(dayOffset);
    }

    private static DateTime GetMonthWeekdayRuleDate(
        int year,
        int month,
        int week,
        int weekday) {
        DateTime first = new(
            year,
            month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        int firstWeekday = (int)first.DayOfWeek;
        int delta = (weekday - firstWeekday + 7) % 7;
        int day = 1 + delta + (week - 1) * 7;
        int daysInMonth = DateTime.DaysInMonth(year, month);

        if (day > daysInMonth)
            day -= 7;

        return new DateTime(
            year,
            month,
            day,
            0,
            0,
            0,
            DateTimeKind.Utc);
    }

    private readonly record struct Transition(
        long UnixSeconds,
        bool IsDaylightAfter);

    private enum RuleType {
        JulianDay = 0,
        DayOfYear = 1,
        MonthNthDayOfWeek = 2
    }

    private readonly record struct Rule(
        RuleType Type,
        int Day,
        int Week,
        int Month,
        int TransitionSeconds);

    private sealed class PosixParser {
        private readonly string _text;
        private int _position;

        public PosixParser(string text) {
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }

        public bool AtEnd => _position >= _text.Length;

        public char Current => AtEnd ? '\0' : _text[_position];

        public bool TryReadName(out string name) {
            int start = _position;

            while (!AtEnd) {
                char value = Current;

                if (char.IsAsciiDigit(value) ||
                    value is ',' or ';' or '-' or '+') {
                    break;
                }

                _position++;
            }

            name = _text[start.._position];
            return name.Length != 0;
        }

        public bool TryReadOffset(out int seconds) {
            seconds = 0;
            int sign = 1;

            if (Current == '-') {
                sign = -1;
                _position++;
            } else if (Current == '+') {
                _position++;
            }

            if (!TryReadSeconds(out int unsignedSeconds))
                return false;

            seconds = checked(sign * unsignedSeconds);
            return true;
        }

        public bool TryReadRule(out Rule rule) {
            rule = default;

            RuleType type;
            int day = 0;
            int week = 0;
            int month = 0;

            if (TryRead('J')) {
                type = RuleType.JulianDay;

                if (!TryReadNumber(1, 365, out day))
                    return false;
            } else if (TryRead('M')) {
                type = RuleType.MonthNthDayOfWeek;

                if (!TryReadNumber(1, 12, out month) ||
                    !TryRead('.') ||
                    !TryReadNumber(1, 5, out week) ||
                    !TryRead('.') ||
                    !TryReadNumber(0, 6, out day)) {
                    return false;
                }
            } else if (char.IsAsciiDigit(Current)) {
                type = RuleType.DayOfYear;

                if (!TryReadNumber(0, 365, out day))
                    return false;
            } else {
                return false;
            }

            int transitionSeconds = 2 * SecondsPerHour;

            if (TryRead('/')) {
                if (!TryReadSeconds(out transitionSeconds))
                    return false;
            }

            rule = new Rule(
                type,
                day,
                week,
                month,
                transitionSeconds);

            return true;
        }

        public bool TryRead(char expected) {
            if (Current != expected)
                return false;

            _position++;
            return true;
        }

        public bool TryReadOneOf(char first, char second) {
            if (Current != first && Current != second)
                return false;

            _position++;
            return true;
        }

        private bool TryReadSeconds(out int seconds) {
            seconds = 0;

            if (!TryReadNumber(
                    0,
                    HoursPerDay * 7 - 1,
                    out int hours)) {
                return false;
            }

            seconds = checked(hours * SecondsPerHour);

            if (TryRead(':')) {
                if (!TryReadNumber(
                        0,
                        MinutesPerHour - 1,
                        out int minutes)) {
                    return false;
                }

                seconds = checked(
                    seconds +
                    minutes * SecondsPerMinute);

                if (TryRead(':')) {
                    if (!TryReadNumber(
                            0,
                            SecondsPerMinute,
                            out int secondPart)) {
                        return false;
                    }

                    seconds = checked(seconds + secondPart);
                }
            }

            return true;
        }

        private bool TryReadNumber(
            int minimum,
            int maximum,
            out int value) {
            value = 0;

            if (!char.IsAsciiDigit(Current))
                return false;

            int parsed = 0;

            while (char.IsAsciiDigit(Current)) {
                int digit = Current - '0';

                try {
                    parsed = checked(parsed * 10 + digit);
                } catch (OverflowException) {
                    return false;
                }

                if (parsed > maximum)
                    return false;

                _position++;
            }

            if (parsed < minimum)
                return false;

            value = parsed;
            return true;
        }
    }
}
