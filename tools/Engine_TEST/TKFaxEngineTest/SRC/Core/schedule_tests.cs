/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * schedule_tests.cs - complete managed port of schedule_tests.c test.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable

using System.Globalization;

namespace TKFaxEngineTest.Core.Test;

public static class schedule_tests {
    private static ulong _when1;
    private static ulong _when2;

    public static int main(string[] testArguments) {
        try {
            return tkfx_schedule_tests_body_main(testArguments);
        } catch (ScheduleTestsExitException exception) {
            return exception.ExitCode;
        }
    }

    public static int tkfx_schedule_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        TKFaxEngine.SpanScheduleState scheduler = TKFaxEngine.ScheduleApi.span_schedule_init(null);

        TKFaxEngine.ScheduleApi.span_schedule_event(scheduler, 500000, Callback1, null);
        TKFaxEngine.ScheduleApi.span_schedule_event(scheduler, 550000, Callback2, null);
        _when1 = unchecked(TKFaxEngine.ScheduleApi.span_schedule_time(scheduler) + 500000UL);
        _when2 = unchecked(TKFaxEngine.ScheduleApi.span_schedule_time(scheduler) + 550000UL);

        for (int i = 0; i < 100000000; i += 20000)
            TKFaxEngine.ScheduleApi.span_schedule_update(scheduler, 20000);

        ulong when = TKFaxEngine.ScheduleApi.span_schedule_time(scheduler);
        ulong distance1 = unchecked(_when1 - when);
        ulong distance2 = unchecked(_when2 - when);
        if (distance1 > 500000UL || distance2 > 550000UL) {
            Console.WriteLine("Callback failed to occur.");
            Exit(2);
        }

        TKFaxEngine.ScheduleApi.span_schedule_release(scheduler);

        Console.WriteLine("Tests passed.");
        return 0;
    }

    private static void Callback1(TKFaxEngine.SpanScheduleState scheduler, object? userData) {
        _ = userData;

        ulong when = TKFaxEngine.ScheduleApi.span_schedule_time(scheduler);
        ulong difference = unchecked(when - _when1);
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "1: Callback at {0:F6} {1}",
                (float)when / 1000000.0f,
                difference));

        if (difference != 0UL) {
            Console.WriteLine("Callback occured at the wrong time.");
            Exit(2);
        }

        int id = TKFaxEngine.ScheduleApi.span_schedule_event(scheduler, 500000, Callback1, null);
        _when1 = unchecked(when + 500000UL);
        when = TKFaxEngine.ScheduleApi.span_schedule_next(scheduler);
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "1: Event {0}, earliest is {1}",
                id,
                when));
    }

    private static void Callback2(TKFaxEngine.SpanScheduleState scheduler, object? userData) {
        _ = userData;

        ulong when = TKFaxEngine.ScheduleApi.span_schedule_time(scheduler);
        ulong difference = unchecked(when - _when2);
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "2: Callback at {0:F6} {1}",
                (float)when / 1000000.0f,
                difference));

        int id = TKFaxEngine.ScheduleApi.span_schedule_event(scheduler, 550000, Callback2, null);
        if (difference != 10000UL) {
            Console.WriteLine("Callback occured at the wrong time.");
            Exit(2);
        }

        _when2 = unchecked(when + 550000UL);
        when = TKFaxEngine.ScheduleApi.span_schedule_next(scheduler);
        Console.WriteLine(
            string.Format(
                CultureInfo.InvariantCulture,
                "2: Event {0}, earliest is {1}",
                id,
                when));
    }

    private static void Exit(int code) {
        throw new ScheduleTestsExitException(code);
    }

    public sealed class ScheduleTestsExitException : Exception {
        public ScheduleTestsExitException(int exitCode)
            : base("schedule_tests exited with code " + exitCode + ".") {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }
}
