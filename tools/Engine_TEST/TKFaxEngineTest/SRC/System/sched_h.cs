/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * sched_h.cs - managed port of the local sched.h compatibility header.
 *
 * The original Windows implementation maps sched_yield() to
 * SleepEx(0, FALSE) and returns zero.
 */

#nullable enable

namespace TKFaxEngineTest.System;

public static class sched_h {
    public static int sched_yield() {
        Thread.Sleep(0);
        return 0;
    }
}
