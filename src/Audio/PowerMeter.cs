/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * PowerMeter.cs - Managed C# port of power_meter.c and power_meter.h
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2003 Steve Underwood
 *
 * This file is distributed under the terms of the GNU Lesser General Public
 * License version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Audio;

/// <summary>Power-level constants and conversion helpers.</summary>
public static class PowerMeter {
    public const float Dbm0MaximumPower = 3.14f + 3.02f;

    public const float DbovMaximumPower = 0.0f;

    private const float MaximumPcmPower = 32767.0f * 32767.0f;

    public static int LevelDbm0(float level) {
        level -= Dbm0MaximumPower;
        if (level > 0.0f) {
            level = 0.0f;
        }

        float value = MathF.Pow(10.0f, level / 10.0f) * MaximumPcmPower;
        return (int)value;
    }

    public static int LevelDbov(float level) {
        level -= DbovMaximumPower;
        if (level > 0.0f) {
            level = 0.0f;
        }

        float value = MathF.Pow(10.0f, level / 10.0f) * MaximumPcmPower;
        return (int)value;
    }

    public static float ReadingToDbm0(int reading) {
        if (reading <= 0) {
            return -96.329f + Dbm0MaximumPower;
        }

        return 10.0f * MathF.Log10(
            reading / MaximumPcmPower + 1.0e-10f) +
            Dbm0MaximumPower;
    }

    public static float ReadingToDbov(int reading) {
        if (reading <= 0) {
            return -96.329f;
        }

        return 10.0f * MathF.Log10(
            reading / MaximumPcmPower + 1.0e-10f) +
            DbovMaximumPower;
    }

    internal static float DbToPowerRatio(float decibels) =>
        MathF.Pow(10.0f, decibels / 10.0f);

}

/// <summary>State of a simple first-order IIR running power meter.</summary>
public sealed class PowerMeterState {
    public PowerMeterState(int shift = 4) {
        Initialize(shift);
    }

    public int Shift { get; private set; }

    public int Reading { get; internal set; }

    public void Initialize(int shift) {
        ValidateShift(shift);
        Shift = shift;
        Reading = 0;
    }

    public PowerMeterState SetDamping(int shift) {
        ValidateShift(shift);
        Shift = shift;
        return this;
    }

    public int Update(short amplitude) {
        long samplePower = (long)amplitude * amplitude;
        long delta = samplePower - Reading;
        long updated = Reading + (delta >> Shift);

        if (updated > int.MaxValue) {
            Reading = int.MaxValue;
        } else if (updated < int.MinValue) {
            Reading = int.MinValue;
        } else {
            Reading = (int)updated;
        }

        return Reading;
    }

    public int Receive(ReadOnlySpan<short> amplitudes) {
        foreach (short amplitude in amplitudes) {
            Update(amplitude);
        }

        // The native power_meter_rx() routine returns zero.
        return 0;
    }

    public float CurrentDbm0 => PowerMeter.ReadingToDbm0(Reading);

    public float CurrentDbov => PowerMeter.ReadingToDbov(Reading);

    private static void ValidateShift(int shift) {
        if ((uint)shift > 30U) {
            throw new ArgumentOutOfRangeException(
                nameof(shift),
                "The IIR shift must be between 0 and 30.");
        }
    }
}

/// <summary>
/// Detects rapid signal-power increases and decreases by comparing short- and
/// medium-term running power meters.
/// </summary>
public sealed class PowerSurgeDetectorState {
    public PowerSurgeDetectorState(float minimumDbm0 = -50.0f, float surgeDb = 6.0f) {
        ShortTerm = new PowerMeterState(4);
        MediumTerm = new PowerMeterState(7);
        Initialize(minimumDbm0, surgeDb);
    }

    public PowerMeterState ShortTerm { get; }

    public PowerMeterState MediumTerm { get; }

    public bool SignalPresent { get; private set; }

    public int Surge { get; private set; }

    public int Sag { get; private set; }

    public int Minimum { get; private set; }

    public void Initialize(float minimumDbm0, float surgeDb) {
        ShortTerm.Initialize(4);
        MediumTerm.Initialize(7);

        float ratio = PowerMeter.DbToPowerRatio(surgeDb);
        if (!(ratio > 0.0f) || float.IsInfinity(ratio) || float.IsNaN(ratio)) {
            throw new ArgumentOutOfRangeException(nameof(surgeDb));
        }

        Surge = SaturateToInt32(1024.0f * ratio);
        Sag = SaturateToInt32(1024.0f / ratio);
        Minimum = PowerMeter.LevelDbm0(minimumDbm0);
        MediumTerm.Reading = Minimum == int.MaxValue ? int.MaxValue : Minimum + 1;
        SignalPresent = false;
    }

    public int Detect(short amplitude) {
        int shortPower = ShortTerm.Update(amplitude);
        int mediumPower = MediumTerm.Update(amplitude);

        if (mediumPower < Minimum) {
            return 0;
        }

        if (!SignalPresent) {
            long threshold = (long)Surge * (mediumPower >> 10);
            if (shortPower <= threshold) {
                return 0;
            }

            SignalPresent = true;
            MediumTerm.Reading = ShortTerm.Reading;
        } else {
            long threshold = (long)Sag * (mediumPower >> 10);
            if (shortPower < threshold) {
                SignalPresent = false;
                MediumTerm.Reading = ShortTerm.Reading;
                return 0;
            }
        }

        return shortPower;
    }

    public float CurrentDbm0 => ShortTerm.CurrentDbm0;

    public float CurrentDbov => ShortTerm.CurrentDbov;

    private static int SaturateToInt32(float value) {
        if (value >= int.MaxValue) {
            return int.MaxValue;
        }

        if (value <= int.MinValue) {
            return int.MinValue;
        }

        return (int)value;
    }
}

/// <summary>Native-name-compatible entry points for power measurement.</summary>
public static class PowerMeterApi {
    public static PowerMeterState? power_meter_init(
        PowerMeterState? state,
        int shift) {
        try {
            state ??= new PowerMeterState(shift);
            state.Initialize(shift);
            return state;
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

    public static int power_meter_release(PowerMeterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int power_meter_free(PowerMeterState? state) {
        _ = state;
        return 0;
    }

    public static PowerMeterState power_meter_damping(
        PowerMeterState state,
        int shift) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetDamping(shift);
    }

    public static int power_meter_update(PowerMeterState state, short amplitude) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Update(amplitude);
    }

    public static int power_meter_rx(
        PowerMeterState state,
        short[] amplitudes,
        int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(amplitudes);

        if (length < 0 || length > amplitudes.Length) {
            return -1;
        }

        return state.Receive(amplitudes.AsSpan(0, length));
    }

    public static int power_meter_current(PowerMeterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Reading;
    }

    public static float power_meter_current_dbm0(PowerMeterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentDbm0;
    }

    public static float power_meter_current_dbov(PowerMeterState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentDbov;
    }

    public static int power_meter_level_dbm0(float level) =>
        PowerMeter.LevelDbm0(level);

    public static int power_meter_level_dbov(float level) =>
        PowerMeter.LevelDbov(level);

    public static int power_surge_detector(
        PowerSurgeDetectorState state,
        short amplitude) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Detect(amplitude);
    }

    public static float power_surge_detector_current_dbm0(
        PowerSurgeDetectorState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentDbm0;
    }

    public static float power_surge_detector_current_dbov(
        PowerSurgeDetectorState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.CurrentDbov;
    }

    public static PowerSurgeDetectorState? power_surge_detector_init(
        PowerSurgeDetectorState? state,
        float minimum,
        float surge) {
        try {
            state ??= new PowerSurgeDetectorState(minimum, surge);
            state.Initialize(minimum, surge);
            return state;
        } catch (ArgumentOutOfRangeException) {
            return null;
        }
    }

    public static int power_surge_detector_release(
        PowerSurgeDetectorState state) {
        ArgumentNullException.ThrowIfNull(state);
        return 0;
    }

    public static int power_surge_detector_free(
        PowerSurgeDetectorState? state) {
        _ = state;
        return 0;
    }
}
