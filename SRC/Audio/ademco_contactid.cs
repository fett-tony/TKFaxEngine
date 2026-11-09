/*
 * TKFaxEngine - managed C# port
 *
 * AdemcoContactId.cs
 *
 * Combined direct port of ademco_contactid.h,
 * private/ademco_contactid.h and ademco_contactid.c.
 *
 * Ademco ContactID alarm protocol.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2012 Steve Underwood.
 *
 * The supplied public/private headers state the GNU Lesser General Public
 * License version 2.1. The supplied implementation file states the GNU
 * General Public License version 2. The combined port retains those notices.
 */

#nullable enable

using System.Globalization;

namespace TKFaxEngine.Audio;

/// <summary>Managed equivalent of <c>ademco_contactid_report_t</c>.</summary>
public sealed class AdemcoContactIdReport {
    /// <summary>
    /// Stores the <c>acct</c> value.
    /// </summary>
    public int acct;
    /// <summary>
    /// Stores the <c>mt</c> value.
    /// </summary>
    public int mt;
    /// <summary>
    /// Stores the <c>q</c> value.
    /// </summary>
    public int q;
    /// <summary>
    /// Stores the <c>xyz</c> value.
    /// </summary>
    public int xyz;
    /// <summary>
    /// Stores the <c>gg</c> value.
    /// </summary>
    public int gg;
    /// <summary>
    /// Stores the <c>ccc</c> value.
    /// </summary>
    public int ccc;
}

/// <summary>Managed equivalent of <c>ademco_contactid_report_func_t</c>.</summary>
public delegate void AdemcoContactIdReportHandler(
    object? userData,
    AdemcoContactIdReport report);

/// <summary>Managed equivalent of the native tone-report callback.</summary>
public delegate void AdemcoContactIdToneReportHandler(
    object? userData,
    int code,
    int level,
    int delay);

internal readonly record struct AdemcoContactIdCode(
    int Code,
    string Name,
    int DataType);

/// <summary>Managed equivalent of <c>ademco_contactid_receiver_state_t</c>.</summary>
public sealed class AdemcoContactIdReceiverState : IDisposable {
    private bool _disposed;

    internal AdemcoContactIdReportHandler? callback;
    internal object? callback_user_data;

    internal int step;
    internal int remaining_samples;
    internal uint tone_phase;
    internal int tone_phase_rate;
    internal short tone_level;
    internal DtmfRxState dtmf;

    internal readonly char[] rx_digits = new char[16 + 1];
    internal int rx_digits_len;

    internal SpanLogState logging;

    /// <summary>
    /// Initializes a new instance of the <c>AdemcoContactIdReceiverState</c> type.
    /// </summary>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    public AdemcoContactIdReceiverState(
        AdemcoContactIdReportHandler? callback = null,
        object? userData = null) {
        dtmf = new DtmfRxState();
        logging = new SpanLogState();
        AdemcoContactId.ademco_contactid_receiver_init(this, callback, userData);
    }

    internal AdemcoContactIdReceiverState(bool initialize) {
        dtmf = new DtmfRxState();
        logging = new SpanLogState();

        if (initialize)
            AdemcoContactId.ademco_contactid_receiver_init(this, null, null);
    }

    /// <summary>
    /// Gets the <c>Step</c> value.
    /// </summary>
    public int Step => step;

    /// <summary>
    /// Gets the <c>RemainingSamples</c> value.
    /// </summary>
    public int RemainingSamples => remaining_samples;

    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public SpanLogState Logging => logging;

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        dtmf.Dispose();
        logging.Dispose();
        callback = null;
        callback_user_data = null;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void MarkInitialized() => _disposed = false;

    internal void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdemcoContactIdReceiverState));
    }
}

/// <summary>Managed equivalent of <c>ademco_contactid_sender_state_t</c>.</summary>
public sealed class AdemcoContactIdSenderState : IDisposable {
    private bool _disposed;

    internal AdemcoContactIdToneReportHandler? callback;
    internal object? callback_user_data;

    internal int step;
    internal int remaining_samples;

    internal DtmfTxState dtmf;

#if TKFAXENGINE_USE_FIXED_POINT
    internal int threshold;
    internal int energy;
#else
    internal float threshold;
    internal float energy;
#endif

    internal GoertzelState tone_1400;
    internal GoertzelState tone_2300;
    internal int current_sample;

    internal readonly char[] tx_digits = new char[16 + 1];
    internal int tx_digits_len;
    internal int tries;

    internal int tone_state;
    internal int duration;
    internal int last_hit;
    internal int in_tone;
    internal bool clear_to_send;
    internal int timer;

    internal bool busy;

    internal SpanLogState logging;

    /// <summary>
    /// Initializes a new instance of the <c>AdemcoContactIdSenderState</c> type.
    /// </summary>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    public AdemcoContactIdSenderState(
        AdemcoContactIdToneReportHandler? callback = null,
        object? userData = null) {
        dtmf = new DtmfTxState();
        tone_1400 = new GoertzelState(AdemcoContactId.Tone1400Descriptor);
        tone_2300 = new GoertzelState(AdemcoContactId.Tone2300Descriptor);
        logging = new SpanLogState();
        AdemcoContactId.ademco_contactid_sender_init(this, callback, userData);
    }

    internal AdemcoContactIdSenderState(bool initialize) {
        dtmf = new DtmfTxState();
        tone_1400 = new GoertzelState(AdemcoContactId.Tone1400Descriptor);
        tone_2300 = new GoertzelState(AdemcoContactId.Tone2300Descriptor);
        logging = new SpanLogState();

        if (initialize)
            AdemcoContactId.ademco_contactid_sender_init(this, null, null);
    }

    /// <summary>
    /// Gets the <c>Step</c> value.
    /// </summary>
    public int Step => step;

    /// <summary>
    /// Gets the <c>RemainingSamples</c> value.
    /// </summary>
    public int RemainingSamples => remaining_samples;

    /// <summary>
    /// Gets the <c>Tries</c> value.
    /// </summary>
    public int Tries => tries;

    /// <summary>
    /// Gets the <c>Busy</c> value.
    /// </summary>
    public bool Busy => busy;

    /// <summary>
    /// Gets the <c>ClearToSend</c> value.
    /// </summary>
    public bool ClearToSend => clear_to_send;

    /// <summary>
    /// Gets the <c>Logging</c> value.
    /// </summary>
    public SpanLogState Logging => logging;

    /// <summary>
    /// Performs the <c>Dispose</c> operation.
    /// </summary>
    public void Dispose() {
        if (_disposed)
            return;

        dtmf.Dispose();
        tone_1400.Dispose();
        tone_2300.Dispose();
        logging.Dispose();
        callback = null;
        callback_user_data = null;
        Array.Clear(tx_digits);
        tx_digits_len = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    internal void MarkInitialized() => _disposed = false;

    internal void ThrowIfDisposed() {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AdemcoContactIdSenderState));
    }
}

/// <summary>
/// Direct managed port of the spanDSP Ademco ContactID encoder, decoder,
/// receiver and sender state machines.
/// </summary>
public static class AdemcoContactId {
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MESSAGE_TYPE_18</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MESSAGE_TYPE_18 = 0x18;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MESSAGE_TYPE_98</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MESSAGE_TYPE_98 = 0x98;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_QUALIFIER_NEW_EVENT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_QUALIFIER_NEW_EVENT = 1;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_QUALIFIER_NEW_RESTORE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_QUALIFIER_NEW_RESTORE = 3;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_QUALIFIER_STATUS_REPORT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_QUALIFIER_STATUS_REPORT = 6;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DATA_IS_ZONE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DATA_IS_ZONE = 0;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DATA_IS_USER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DATA_IS_USER = 1;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MEDICAL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MEDICAL = 0x100;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PERSONAL_EMERGENCY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PERSONAL_EMERGENCY = 0x101;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FAIL_TO_REPORT_IN</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FAIL_TO_REPORT_IN = 0x102;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE = 0x110;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SMOKE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SMOKE = 0x111;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_COMBUSTION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_COMBUSTION = 0x112;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_WATER_FLOW</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_WATER_FLOW = 0x113;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_HEAT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_HEAT = 0x114;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PULL_STATION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PULL_STATION = 0x115;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DUCT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DUCT = 0x116;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FLAME</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FLAME = 0x117;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NEAR_ALARM_A</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NEAR_ALARM_A = 0x118;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PANIC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PANIC = 0x120;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DURESS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DURESS = 0x121;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SILENT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SILENT = 0x122;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AUDIBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AUDIBLE = 0x123;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DURESS_ACCESS_GRANTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DURESS_ACCESS_GRANTED = 0x124;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DURESS_EGRESS_GRANTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DURESS_EGRESS_GRANTED = 0x125;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BURGLARY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BURGLARY = 0x130;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PERIMETER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PERIMETER = 0x131;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_INTERIOR</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_INTERIOR = 0x132;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_24_HOUR_SAFE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_24_HOUR_SAFE = 0x133;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ENTRY_EXIT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ENTRY_EXIT = 0x134;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DAY_NIGHT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DAY_NIGHT = 0x135;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_OUTDOOR</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_OUTDOOR = 0x136;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TAMPER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TAMPER = 0x137;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NEAR_ALARM_B</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NEAR_ALARM_B = 0x138;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_INTRUSION_VERIFIER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_INTRUSION_VERIFIER = 0x139;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GENERAL_ALARM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GENERAL_ALARM = 0x140;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POLLING_LOOP_OPEN_A</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POLLING_LOOP_OPEN_A = 0x141;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POLLING_LOOP_SHORT_A</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POLLING_LOOP_SHORT_A = 0x142;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_A</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_A = 0x143;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_TAMPER_A</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_TAMPER_A = 0x144;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXPANSION_MODULE_TAMPER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_TAMPER = 0x145;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SILENT_BURGLARY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SILENT_BURGLARY = 0x146;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_SUPERVISION_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_SUPERVISION_FAILURE = 0x147;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_24_HOUR_NONBURGLARY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_24_HOUR_NONBURGLARY = 0x150;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GAS_DETECTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GAS_DETECTED = 0x151;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REFRIGERATION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REFRIGERATION = 0x152;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_HEAT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_HEAT = 0x153;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_WATER_LEAKAGE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_WATER_LEAKAGE = 0x154;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FOIL_BREAK</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FOIL_BREAK = 0x155;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DAY_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DAY_TROUBLE = 0x156;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_BOTTLED_GAS_LEVEL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_BOTTLED_GAS_LEVEL = 0x157;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_HIGH_TEMP</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_HIGH_TEMP = 0x158;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_TEMP</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_TEMP = 0x159;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_AIR_FLOW</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_AIR_FLOW = 0x161;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_CARBON_MONOXIDE_DETECTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_CARBON_MONOXIDE_DETECTED = 0x162;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TANK_LEVEL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TANK_LEVEL = 0x163;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE_SUPERVISORY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE_SUPERVISORY = 0x200;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_WATER_PRESSURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_WATER_PRESSURE = 0x201;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_CO2</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_CO2 = 0x202;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GATE_VALVE_SENSOR</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GATE_VALVE_SENSOR = 0x203;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_WATER_LEVEL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_WATER_LEVEL = 0x204;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PUMP_ACTIVATED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PUMP_ACTIVATED = 0x205;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PUMP_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PUMP_FAILURE = 0x206;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_TROUBLE = 0x300;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AC_LOSS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AC_LOSS = 0x301;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOW_SYSTEM_BATTERY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOW_SYSTEM_BATTERY = 0x302;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RAM_CHECKSUM_BAD</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RAM_CHECKSUM_BAD = 0x303;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ROM_CHECKSUM_BAD</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ROM_CHECKSUM_BAD = 0x304;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_RESET = 0x305;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PANEL_PROGRAMMING_CHANGED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PANEL_PROGRAMMING_CHANGED = 0x306;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SELFTEST_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SELFTEST_FAILURE = 0x307;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_SHUTDOWN</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_SHUTDOWN = 0x308;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BATTERY_TEST_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BATTERY_TEST_FAILURE = 0x309;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GROUND_FAULT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GROUND_FAULT = 0x310;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BATTERY_MISSING_DEAD</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BATTERY_MISSING_DEAD = 0x311;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POWER_SUPPLY_OVERCURRENT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POWER_SUPPLY_OVERCURRENT = 0x312;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ENGINEER_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ENGINEER_RESET = 0x313;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SOUNDER_RELAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SOUNDER_RELAY = 0x320;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BELL_1</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BELL_1 = 0x321;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BELL_2</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BELL_2 = 0x322;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ALARM_RELAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ALARM_RELAY = 0x323;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TROUBLE_RELAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TROUBLE_RELAY = 0x324;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REVERSING_RELAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REVERSING_RELAY = 0x325;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3 = 0x326;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4 = 0x327;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_PERIPHERAL_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_PERIPHERAL_TROUBLE = 0x330;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POLLING_LOOP_OPEN_B</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POLLING_LOOP_OPEN_B = 0x331;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POLLING_LOOP_SHORT_B</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POLLING_LOOP_SHORT_B = 0x332;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_B</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_B = 0x333;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REPEATER_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REPEATER_FAILURE = 0x334;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOCAL_PRINTER_OUT_OF_PAPER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOCAL_PRINTER_OUT_OF_PAPER = 0x335;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOCAL_PRINTER_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOCAL_PRINTER_FAILURE = 0x336;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_DC_LOSS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_DC_LOSS = 0x337;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_LOW_BATTERY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_LOW_BATTERY = 0x338;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_RESET = 0x339;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_TAMPER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_TAMPER = 0x341;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_AC_LOSS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_AC_LOSS = 0x342;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXP_MODULE_SELFTEST_FAIL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXP_MODULE_SELFTEST_FAIL = 0x343;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RF_RECEIVER_JAM_DETECT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RF_RECEIVER_JAM_DETECT = 0x344;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_COMMUNICATION_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_COMMUNICATION_TROUBLE = 0x350;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TELCO_1_FAULT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TELCO_1_FAULT = 0x351;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TELCO_2_FAULT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TELCO_2_FAULT = 0x352;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LONG_RANGE_RADIO_TRANSMITTER_FAULT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LONG_RANGE_RADIO_TRANSMITTER_FAULT = 0x353;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FAILURE_TO_COMMUNICATE_EVENT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FAILURE_TO_COMMUNICATE_EVENT = 0x354;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_RADIO_SUPERVISION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_RADIO_SUPERVISION = 0x355;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_CENTRAL_POLLING</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_CENTRAL_POLLING = 0x356;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LONG_RANGE_RADIO_VSWR_PROBLEM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LONG_RANGE_RADIO_VSWR_PROBLEM = 0x357;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PROTECTION_LOOP</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP = 0x370;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PROTECTION_LOOP_OPEN</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP_OPEN = 0x371;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PROTECTION_LOOP_SHORT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP_SHORT = 0x372;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE_TROUBLE = 0x373;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXIT_ERROR_ALARM_ZONE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXIT_ERROR_ALARM_ZONE = 0x374;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PANIC_ZONE_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PANIC_ZONE_TROUBLE = 0x375;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_HOLDUP_ZONE_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_HOLDUP_ZONE_TROUBLE = 0x376;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SWINGER_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SWINGER_TROUBLE = 0x377;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_CROSSZONE_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_CROSSZONE_TROUBLE = 0x378;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_TROUBLE = 0x380;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RF</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RF = 0x381;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RPM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RPM = 0x382;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_TAMPER_B</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_TAMPER_B = 0x383;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RF_LOW_BATTERY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RF_LOW_BATTERY = 0x384;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SMOKE_DETECTOR_HIGH_SENSITIVITY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SMOKE_DETECTOR_HIGH_SENSITIVITY = 0x385;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SMOKE_DETECTOR_LOW_SENSITIVITY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SMOKE_DETECTOR_LOW_SENSITIVITY = 0x386;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_INTRUSION_DETECTOR_HIGH_SENSITIVITY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_INTRUSION_DETECTOR_HIGH_SENSITIVITY = 0x387;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_INTRUSION_DETECTOR_LOW_SENSITIVITY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_INTRUSION_DETECTOR_LOW_SENSITIVITY = 0x388;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_SELFTEST_FAILURE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_SELFTEST_FAILURE = 0x389;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENSOR_WATCH_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENSOR_WATCH_TROUBLE = 0x391;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DRIFT_COMPENSATION_ERROR</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DRIFT_COMPENSATION_ERROR = 0x392;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MAINTENANCE_ALERT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MAINTENANCE_ALERT = 0x393;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_OPEN_CLOSE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_OPEN_CLOSE = 0x400;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_OC_BY_USER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_OC_BY_USER = 0x401;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GROUP_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GROUP_OC = 0x402;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AUTOMATIC_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AUTOMATIC_OC = 0x403;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LATE_TO_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LATE_TO_OC = 0x404;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DEFERRED_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DEFERRED_OC = 0x405;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_CANCEL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_CANCEL = 0x406;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REMOTE_ARM_DISARM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REMOTE_ARM_DISARM = 0x407;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_QUICK_ARM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_QUICK_ARM = 0x408;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_KEYSWITCH_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_KEYSWITCH_OC = 0x409;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ARMED_STAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ARMED_STAY = 0x441;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_KEYSWITCH_ARMED_STAY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_KEYSWITCH_ARMED_STAY = 0x442;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXCEPTION_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXCEPTION_OC = 0x450;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EARLY_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EARLY_OC = 0x451;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LATE_OC</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LATE_OC = 0x452;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FAILED_TO_OPEN</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FAILED_TO_OPEN = 0x453;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FAILED_TO_CLOSE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FAILED_TO_CLOSE = 0x454;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AUTOARM_FAILED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AUTOARM_FAILED = 0x455;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PARTIAL_ARM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PARTIAL_ARM = 0x456;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXIT_ERROR_USER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXIT_ERROR_USER = 0x457;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_USER_ON_PREMISES</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_USER_ON_PREMISES = 0x458;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RECENT_CLOSE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RECENT_CLOSE = 0x459;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_WRONG_CODE_ENTRY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_WRONG_CODE_ENTRY = 0x461;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LEGAL_CODE_ENTRY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LEGAL_CODE_ENTRY = 0x462;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REARM_AFTER_ALARM</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REARM_AFTER_ALARM = 0x463;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AUTOARM_TIME_EXTENDED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AUTOARM_TIME_EXTENDED = 0x464;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PANIC_ALARM_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PANIC_ALARM_RESET = 0x465;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SERVICE_ON_OFF_PREMISES</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SERVICE_ON_OFF_PREMISES = 0x466;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_CALLBACK_REQUEST_MADE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_CALLBACK_REQUEST_MADE = 0x411;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SUCCESSFUL_DOWNLOAD_ACCESS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SUCCESSFUL_DOWNLOAD_ACCESS = 0x412;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_UNSUCCESSFUL_ACCESS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_UNSUCCESSFUL_ACCESS = 0x413;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_SHUTDOWN_COMMAND_RECEIVED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_SHUTDOWN_COMMAND_RECEIVED = 0x414;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DIALER_SHUTDOWN_COMMAND_RECEIVED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DIALER_SHUTDOWN_COMMAND_RECEIVED = 0x415;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SUCCESSFUL_UPLOAD</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SUCCESSFUL_UPLOAD = 0x416;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_DENIED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_DENIED = 0x421;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_REPORT_BY_USER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_REPORT_BY_USER = 0x422;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FORCED_ACCESS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FORCED_ACCESS = 0x423;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EGRESS_DENIED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EGRESS_DENIED = 0x424;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EGRESS_GRANTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EGRESS_GRANTED = 0x425;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_DOOR_PROPPED_OPEN</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_DOOR_PROPPED_OPEN = 0x426;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_POINT_DOOR_STATUS_MONITOR_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_POINT_DOOR_STATUS_MONITOR_TROUBLE = 0x427;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_POINT_REQUEST_TO_EXIT_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_POINT_REQUEST_TO_EXIT_TROUBLE = 0x428;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_ENTRY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_ENTRY = 0x429;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_EXIT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_EXIT = 0x430;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_THREAT_LEVEL_CHANGE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_THREAT_LEVEL_CHANGE = 0x431;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_RELAY_TRIGGER_FAIL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_RELAY_TRIGGER_FAIL = 0x432;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_RTE_SHUNT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_RTE_SHUNT = 0x433;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_DSM_SHUNT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_DSM_SHUNT = 0x434;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_READER_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_READER_DISABLE = 0x501;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SOUNDER_RELAY_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SOUNDER_RELAY_DISABLE = 0x520;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BELL_1_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BELL_1_DISABLE = 0x521;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BELL_2_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BELL_2_DISABLE = 0x522;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ALARM_RELAY_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ALARM_RELAY_DISABLE = 0x523;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TROUBLE_RELAY_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TROUBLE_RELAY_DISABLE = 0x524;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REVERSING_RELAY_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REVERSING_RELAY_DISABLE = 0x525;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3_DISABLE = 0x526;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4_DISABLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4_DISABLE = 0x527;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MODULE_ADDED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MODULE_ADDED = 0x531;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MODULE_REMOVED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MODULE_REMOVED = 0x532;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DIALER_DISABLED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DIALER_DISABLED = 0x551;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RADIO_TRANSMITTER_DISABLED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RADIO_TRANSMITTER_DISABLED = 0x552;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_REMOTE_UPLOAD_DOWNLOAD_DISABLED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_REMOTE_UPLOAD_DOWNLOAD_DISABLED = 0x553;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ZONE_SENSOR_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ZONE_SENSOR_BYPASS = 0x570;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE_BYPASS = 0x571;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_24_HOUR_ZONE_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_24_HOUR_ZONE_BYPASS = 0x572;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BURG_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BURG_BYPASS = 0x573;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_GROUP_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_GROUP_BYPASS = 0x574;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SWINGER_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SWINGER_BYPASS = 0x575;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_ZONE_SHUNT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_ZONE_SHUNT = 0x576;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_POINT_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_POINT_BYPASS = 0x577;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_MANUAL_TRIGGER_TEST_REPORT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_MANUAL_TRIGGER_TEST_REPORT = 0x601;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PERIODIC_TEST_REPORT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PERIODIC_TEST_REPORT = 0x602;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PERIODIC_RF_TRANSMISSION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PERIODIC_RF_TRANSMISSION = 0x603;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE_TEST</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE_TEST = 0x604;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_STATUS_REPORT_TO_FOLLOW</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_STATUS_REPORT_TO_FOLLOW = 0x605;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LISTENIN_TO_FOLLOW</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LISTENIN_TO_FOLLOW = 0x606;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_WALK_TEST_MODE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_WALK_TEST_MODE = 0x607;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PERIODIC_TEST__SYSTEM_TROUBLE_PRESENT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PERIODIC_TEST__SYSTEM_TROUBLE_PRESENT = 0x608;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_VIDEO_TRANSMITTER_ACTIVE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_VIDEO_TRANSMITTER_ACTIVE = 0x609;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POINT_TESTED_OK</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POINT_TESTED_OK = 0x611;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_POINT_NOT_TESTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_POINT_NOT_TESTED = 0x612;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_INTRUSION_ZONE_WALK_TESTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_INTRUSION_ZONE_WALK_TESTED = 0x613;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_FIRE_ZONE_WALK_TESTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_FIRE_ZONE_WALK_TESTED = 0x614;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PANIC_ZONE_WALK_TESTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PANIC_ZONE_WALK_TESTED = 0x615;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SERVICE_REQUEST</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SERVICE_REQUEST = 0x616;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EVENT_LOG_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EVENT_LOG_RESET = 0x621;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EVENT_LOG_50PC_FULL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EVENT_LOG_50PC_FULL = 0x622;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EVENT_LOG_90PC_FULL</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EVENT_LOG_90PC_FULL = 0x623;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EVENT_LOG_OVERFLOW</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EVENT_LOG_OVERFLOW = 0x624;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TIME_DATE_RESET</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TIME_DATE_RESET = 0x625;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_TIME_DATE_INACCURATE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_TIME_DATE_INACCURATE = 0x626;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PROGRAM_MODE_ENTRY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PROGRAM_MODE_ENTRY = 0x627;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_PROGRAM_MODE_EXIT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_PROGRAM_MODE_EXIT = 0x628;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_32_HOUR_EVENT_LOG_MARKER</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_32_HOUR_EVENT_LOG_MARKER = 0x629;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SCHEDULE_CHANGE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SCHEDULE_CHANGE = 0x630;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_EXCEPTION_SCHEDULE_CHANGE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_EXCEPTION_SCHEDULE_CHANGE = 0x631;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_ACCESS_SCHEDULE_CHANGE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_ACCESS_SCHEDULE_CHANGE = 0x632;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SENIOR_WATCH_TROUBLE</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SENIOR_WATCH_TROUBLE = 0x641;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_LATCHKEY_SUPERVISION</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_LATCHKEY_SUPERVISION = 0x642;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_1</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_1 = 0x651;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_2</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_2 = 0x652;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_3</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_3 = 0x653;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_SYSTEM_INACTIVITY</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_SYSTEM_INACTIVITY = 0x654;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DOWNLOAD_ABORT</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DOWNLOAD_ABORT = 0x900;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DOWNLOAD_START_END</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DOWNLOAD_START_END = 0x901;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_DOWNLOAD_INTERRUPTED</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_DOWNLOAD_INTERRUPTED = 0x902;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_AUTOCLOSE_WITH_BYPASS</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_AUTOCLOSE_WITH_BYPASS = 0x910;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_BYPASS_CLOSING</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_BYPASS_CLOSING = 0x911;
    /// <summary>
    /// Defines the <c>ADEMCO_CONTACTID_32_HOUR_NO_READ_OF_EVENT_LOG</c> constant.
    /// </summary>
    public const int ADEMCO_CONTACTID_32_HOUR_NO_READ_OF_EVENT_LOG = 0x999;

    private const int GOERTZEL_SAMPLES_PER_BLOCK = 55;

#if TKFAXENGINE_USE_FIXED_POINT
    private const int DetectionThreshold = 3035;
#else
    private const float DetectionThreshold = 49728296.6f;
#endif

    private const float ToneToTotalEnergy = 45.2233f;

    internal static readonly GoertzelDescriptor Tone1400Descriptor =
        ToneDetect.MakeGoertzelDescriptor(1400.0f, GOERTZEL_SAMPLES_PER_BLOCK);

    internal static readonly GoertzelDescriptor Tone2300Descriptor =
        ToneDetect.MakeGoertzelDescriptor(2300.0f, GOERTZEL_SAMPLES_PER_BLOCK);

    private static readonly AdemcoContactIdCode[] AdemcoCodes =
    [
        new(0x100, "Medical", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x101, "Personal emergency", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x102, "Fail to report in", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x110, "Fire", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x111, "Smoke", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x112, "Combustion", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x113, "Water flow", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x114, "Heat", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x115, "Pull station", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x116, "Duct", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x117, "Flame", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x118, "Near alarm", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x120, "Panic", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x121, "Duress", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x122, "Silent", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x123, "Audible", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x124, "Duress - Access granted", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x125, "Duress - Egress granted", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x130, "Burglary", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x131, "Perimeter", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x132, "Interior", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x133, "24 hour (safe)", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x134, "Entry/Exit", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x135, "Day/Night", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x136, "Outdoor", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x137, "Tamper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x138, "Near alarm", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x139, "Intrusion verifier", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x140, "General alarm", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x141, "Polling loop open", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x142, "Polling loop short", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x143, "Expansion module failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x144, "Sensor tamper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x145, "Expansion module tamper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x146, "Silent burglary", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x147, "Sensor supervision failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x150, "24 hour non-burglary", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x151, "Gas detected", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x152, "Refrigeration", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x153, "Loss of heat", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x154, "Water leakage", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x155, "Foil break", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x156, "Day trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x157, "Low bottled gas level", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x158, "High temp", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x159, "Low temp", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x161, "Loss of air flow", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x162, "Carbon monoxide detected", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x163, "Tank level", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x200, "Fire supervisory", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x201, "Low water pressure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x202, "Low CO2", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x203, "Gate valve sensor", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x204, "Low water level", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x205, "Pump activated", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x206, "Pump failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x300, "System trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x301, "AC loss", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x302, "Low system battery", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x303, "RAM checksum bad", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x304, "ROM checksum bad", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x305, "System reset", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x306, "Panel programming changed", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x307, "Self-test failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x308, "System shutdown", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x309, "Battery test failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x310, "Ground fault", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x311, "Battery missing/dead", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x312, "Power supply overcurrent", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x313, "Engineer reset", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x320, "Sounder/relay", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x321, "Bell 1", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x322, "Bell 2", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x323, "Alarm relay", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x324, "Trouble relay", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x325, "Reversing relay", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x326, "Notification appliance ckt. #3", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x327, "Notification appliance ckt. #4", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x330, "System peripheral trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x331, "Polling loop open", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x332, "Polling loop short", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x333, "Expansion module failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x334, "Repeater failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x335, "Local printer out of paper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x336, "Local printer failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x337, "Exp. module DC loss", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x338, "Exp. module low battery", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x339, "Exp. module reset", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x341, "Exp. module tamper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x342, "Exp. module AC loss", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x343, "Exp. module self-test fail", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x344, "RF receiver jam detect", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x350, "Communication trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x351, "Telco 1 fault", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x352, "Telco 2 fault", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x353, "Long range radio transmitter fault", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x354, "Failure to communicate event", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x355, "Loss of radio supervision", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x356, "Loss of central polling", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x357, "Long range radio VSWR problem", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x370, "Protection loop", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x371, "Protection loop open", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x372, "Protection loop short", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x373, "Fire trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x374, "Exit error alarm (zone)", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x375, "Panic zone trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x376, "Hold-up zone trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x377, "Swinger trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x378, "Cross-zone trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x380, "Sensor trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x381, "Loss of supervision - RF", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x382, "Loss of supervision - RPM", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x383, "Sensor tamper", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x384, "RF low battery", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x385, "Smoke detector high sensitivity", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x386, "Smoke detector low sensitivity", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x387, "Intrusion detector high sensitivity", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x388, "Intrusion detector low sensitivity", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x389, "Sensor self-test failure", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x391, "Sensor Watch trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x392, "Drift compensation error", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x393, "Maintenance alert", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x400, "Open/Close", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x401, "O/C by user", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x402, "Group O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x403, "Automatic O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x404, "Late to O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x405, "Deferred O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x406, "Cancel", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x407, "Remote arm/disarm", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x408, "Quick arm", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x409, "Keyswitch O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x441, "Armed STAY", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x442, "Keyswitch Armed STAY", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x450, "Exception O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x451, "Early O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x452, "Late O/C", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x453, "Failed to open", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x454, "Failed to close", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x455, "Auto-arm failed", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x456, "Partial arm", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x457, "Exit error (user)", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x458, "User on Premises", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x459, "Recent close", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x461, "Wrong code entry", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x462, "Legal code entry", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x463, "Re-arm after alarm", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x464, "Auto-arm time extended", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x465, "Panic alarm reset", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x466, "Service on/off premises", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x411, "Callback request made", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x412, "Successful download/access", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x413, "Unsuccessful access", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x414, "System shutdown command received", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x415, "Dialer shutdown command received", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x416, "Successful Upload", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x421, "Access denied", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x422, "Access report by user", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x423, "Forced Access", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x424, "Egress Denied", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x425, "Egress Granted", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x426, "Access Door propped open", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x427, "Access point door status monitor trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x428, "Access point request to exit trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x429, "Access program mode entry", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x430, "Access program mode exit", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x431, "Access threat level change", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x432, "Access relay/trigger fail", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x433, "Access RTE shunt", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x434, "Access DSM shunt", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x501, "Access reader disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x520, "Sounder/Relay disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x521, "Bell 1 disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x522, "Bell 2 disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x523, "Alarm relay disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x524, "Trouble relay disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x525, "Reversing relay disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x526, "Notification appliance ckt. #3 disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x527, "Notification appliance ckt. #4 disable", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x531, "Module added", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x532, "Module removed", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x551, "Dialer disabled", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x552, "Radio transmitter disabled", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x553, "Remote upload/download disabled", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x570, "Zone/Sensor bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x571, "Fire bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x572, "24 hour zone bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x573, "Burg. bypass", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x574, "Group bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x575, "Swinger bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x576, "Access zone shunt", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x577, "Access point bypass", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x601, "Manual trigger test report", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x602, "Periodic test report", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x603, "Periodic RF transmission", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x604, "Fire test", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x605, "Status report to follow", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x606, "Listen-in to follow", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x607, "Walk test mode", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x608, "Periodic test - system trouble present", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x609, "Video transmitter active", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x611, "Point tested OK", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x612, "Point not tested", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x613, "Intrusion zone walk tested", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x614, "Fire zone walk tested", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x615, "Panic zone walk tested", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x616, "Service request", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x621, "Event log reset", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x622, "Event log 50% full", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x623, "Event log 90% full", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x624, "Event log overflow", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x625, "Time/Date reset", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x626, "Time/Date inaccurate", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x627, "Program mode entry", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x628, "Program mode exit", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x629, "32 hour event log marker", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x630, "Schedule change", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x631, "Exception schedule change", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x632, "Access schedule change", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x641, "Senior watch trouble", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x642, "Latch-key supervision", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x651, "Reserved for Ademco use", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x652, "Reserved for Ademco use", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x653, "Reserved for Ademco use", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x654, "System inactivity", ADEMCO_CONTACTID_DATA_IS_ZONE),
        new(0x900, "Download abort", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x901, "Download start/end", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x902, "Download interrupted", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x910, "Auto-close with bypass", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x911, "Bypass closing", ADEMCO_CONTACTID_DATA_IS_USER),
        new(0x999, "32 hour no read of event log", ADEMCO_CONTACTID_DATA_IS_USER),
    ];

    /// <summary>
    /// Performs the <c>ademco_contactid_msg_qualifier_to_str</c> operation.
    /// </summary>
    /// <param name="q">The q value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ademco_contactid_msg_qualifier_to_str(int q) {
        return q switch {
            ADEMCO_CONTACTID_QUALIFIER_NEW_EVENT => "New event",
            ADEMCO_CONTACTID_QUALIFIER_NEW_RESTORE => "New restore",
            ADEMCO_CONTACTID_QUALIFIER_STATUS_REPORT => "Status report",
            _ => "???"
        };
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_event_to_str</c> operation.
    /// </summary>
    /// <param name="xyz">The xyz value.</param>
    /// <returns>The result of the operation.</returns>
    public static string ademco_contactid_event_to_str(int xyz) {
        for (int entry = 0; entry < AdemcoCodes.Length; entry++) {
            if (xyz == AdemcoCodes[entry].Code)
                return AdemcoCodes[entry].Name;
        }

        return "???";
    }

    /// <summary>
    /// Performs the <c>encode_msg</c> operation.
    /// </summary>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static int encode_msg(
        Span<char> buffer,
        AdemcoContactIdReport report) {
        ArgumentNullException.ThrowIfNull(report);

        if (!TryEncodeMessage(report, out string message))
            return -1;

        if (buffer.Length < message.Length + 1)
            return -1;

        message.AsSpan().CopyTo(buffer);
        buffer[message.Length] = '\0';
        return message.Length;
    }

    /// <summary>
    /// Performs the <c>encode_msg</c> operation.
    /// </summary>
    /// <param name="buffer">The buffer value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static int encode_msg(
        char[] buffer,
        AdemcoContactIdReport report) {
        ArgumentNullException.ThrowIfNull(buffer);
        return encode_msg(buffer.AsSpan(), report);
    }

    /// <summary>
    /// Performs the <c>encode_msg</c> operation.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <param name="message">The message value.</param>
    /// <returns>The result of the operation.</returns>
    public static int encode_msg(
        AdemcoContactIdReport report,
        out string message) {
        ArgumentNullException.ThrowIfNull(report);

        if (!TryEncodeMessage(report, out message)) {
            message = string.Empty;
            return -1;
        }

        return message.Length;
    }

    /// <summary>
    /// Performs the <c>decode_msg</c> operation.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <returns>The result of the operation.</returns>
    public static int decode_msg(
        AdemcoContactIdReport report,
        string buffer) {
        ArgumentNullException.ThrowIfNull(buffer);
        return decode_msg(report, buffer.AsSpan());
    }

    /// <summary>
    /// Performs the <c>decode_msg</c> operation.
    /// </summary>
    /// <param name="report">The report value.</param>
    /// <param name="buffer">The buffer value.</param>
    /// <returns>The result of the operation.</returns>
    public static int decode_msg(
        AdemcoContactIdReport report,
        ReadOnlySpan<char> buffer) {
        ArgumentNullException.ThrowIfNull(report);

        int terminator = buffer.IndexOf('\0');
        if (terminator >= 0)
            buffer = buffer[..terminator];

        Span<char> remapped = buffer.Length <= 64
            ? stackalloc char[buffer.Length]
            : new char[buffer.Length];

        int sum = 0;

        for (int index = 0; index < buffer.Length; index++) {
            int value = buffer[index];

            value = value switch {
                '*' => 'B',
                '#' => 'C',
                'A' => 'D',
                'B' => 'E',
                'C' => 'F',
                'D' => 'A',
                _ => value
            };

            remapped[index] = (char)value;

            if (value > '9') {
                value -= 'B' - 11;
            } else {
                if (value == '0')
                    value = 10;
                else
                    value -= '0';
            }

            sum += value;
        }

        if (sum % 15 != 0)
            return -1;

        if (remapped.Length < 15)
            return -1;

        if (!TryParseHex(remapped.Slice(0, 4), out int acct) ||
            !TryParseHex(remapped.Slice(4, 2), out int mt) ||
            !TryParseHex(remapped.Slice(6, 1), out int q) ||
            !TryParseHex(remapped.Slice(7, 3), out int xyz) ||
            !TryParseHex(remapped.Slice(10, 2), out int gg) ||
            !TryParseHex(remapped.Slice(12, 3), out int ccc)) {
            return -1;
        }

        report.acct = acct;
        report.mt = mt;
        report.q = q;
        report.xyz = xyz;
        report.gg = gg;
        report.ccc = ccc;
        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_log_msg</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_log_msg(
        AdemcoContactIdReceiverState state,
        AdemcoContactIdReport report) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(report);
        state.ThrowIfDisposed();

        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "Ademco Contact ID message:\n");

        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    Account %X\n",
            report.acct);

        string messageType = report.mt switch {
            ADEMCO_CONTACTID_MESSAGE_TYPE_18 => "Contact ID",
            ADEMCO_CONTACTID_MESSAGE_TYPE_98 => "Contact ID",
            _ => "???"
        };

        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    Message type %s (%X)\n",
            messageType,
            report.mt);

        string text = ademco_contactid_msg_qualifier_to_str(report.q);
        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    Qualifier %s (%X)\n",
            text,
            report.q);

        text = ademco_contactid_event_to_str(report.xyz);
        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    Event %s (%X)\n",
            text,
            report.xyz);

        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    Group/partition %X\n",
            report.gg);

        LoggingApi.span_log(
            state.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    User/Zone information %X\n",
            report.ccc);

        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="maxSamples">The max samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_tx(
        AdemcoContactIdReceiverState state,
        Span<short> amplitude,
        int maxSamples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (maxSamples < 0 || maxSamples > amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(maxSamples));

        int samples;

        switch (state.step) {
            case 0:
                samples = Math.Min(state.remaining_samples, maxSamples);
                amplitude[..samples].Clear();
                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "Initial silence finished\n");

                state.step++;
                state.tone_phase_rate = Dds.PhaseRate(1400.0f);
                state.tone_level = Dds.ScalingDbm0(-11.0f);
                state.tone_phase = 0;
                state.remaining_samples = Telephony.milliseconds_to_samples(100);
                return samples;

            case 1:
                samples = Math.Min(state.remaining_samples, maxSamples);
                GenerateTone(
                    amplitude[..samples],
                    ref state.tone_phase,
                    state.tone_phase_rate,
                    state.tone_level);

                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "1400Hz tone finished\n");

                state.step++;
                state.remaining_samples = Telephony.milliseconds_to_samples(100);
                return samples;

            case 2:
                samples = Math.Min(state.remaining_samples, maxSamples);
                amplitude[..samples].Clear();
                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "Second silence finished\n");

                state.step++;
                state.tone_phase_rate = Dds.PhaseRate(2300.0f);
                state.tone_level = Dds.ScalingDbm0(-11.0f);
                state.tone_phase = 0;
                state.remaining_samples = Telephony.milliseconds_to_samples(100);
                return samples;

            case 3:
                samples = Math.Min(state.remaining_samples, maxSamples);
                GenerateTone(
                    amplitude[..samples],
                    ref state.tone_phase,
                    state.tone_phase_rate,
                    state.tone_level);

                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "2300Hz tone finished\n");

                state.step++;
                state.remaining_samples = Telephony.milliseconds_to_samples(100);
                return samples;

            case 4:
                return 0;

            case 5:
                samples = Math.Min(state.remaining_samples, maxSamples);
                amplitude[..samples].Clear();
                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "Sending kissoff\n");

                state.step++;
                state.tone_phase_rate = Dds.PhaseRate(1400.0f);
                state.tone_level = Dds.ScalingDbm0(-11.0f);
                state.tone_phase = 0;
                state.remaining_samples = Telephony.milliseconds_to_samples(850);
                return samples;

            case 6:
                samples = Math.Min(state.remaining_samples, maxSamples);
                GenerateTone(
                    amplitude[..samples],
                    ref state.tone_phase,
                    state.tone_phase_rate,
                    state.tone_level);

                state.remaining_samples -= samples;

                if (state.remaining_samples > 0)
                    return samples;

                LoggingApi.span_log(
                    state.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "1400Hz tone finished\n");

                state.step = 4;
                state.remaining_samples = Telephony.milliseconds_to_samples(100);
                return samples;

            default:
                return maxSamples;
        }
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="maxSamples">The max samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_tx(
        AdemcoContactIdReceiverState state,
        short[] amplitude,
        int maxSamples) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return ademco_contactid_receiver_tx(
            state,
            amplitude.AsSpan(),
            maxSamples);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_rx(
        AdemcoContactIdReceiverState state,
        ReadOnlySpan<short> amplitude,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (samples < 0 || samples > amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(samples));

        return Dtmf.dtmf_rx(
            state.dtmf,
            amplitude[..samples]);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_rx(
        AdemcoContactIdReceiverState state,
        short[] amplitude,
        int samples) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return ademco_contactid_receiver_rx(
            state,
            amplitude.AsSpan(),
            samples);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_fillin</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_fillin(
        AdemcoContactIdReceiverState state,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return Dtmf.dtmf_rx_fillin(state.dtmf, samples);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static SpanLogState ademco_contactid_receiver_get_logging_state(
        AdemcoContactIdReceiverState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.logging;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_set_realtime_callback</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    public static void ademco_contactid_receiver_set_realtime_callback(
        AdemcoContactIdReceiverState state,
        AdemcoContactIdReportHandler? callback,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.callback = callback;
        state.callback_user_data = userData;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static AdemcoContactIdReceiverState ademco_contactid_receiver_init(
        AdemcoContactIdReceiverState? state,
        AdemcoContactIdReportHandler? callback,
        object? userData) {
        state ??= new AdemcoContactIdReceiverState(false);

        state.dtmf.Dispose();
        state.logging.Dispose();

        state.callback = callback;
        state.callback_user_data = userData;

        state.step = 0;
        state.remaining_samples = Telephony.milliseconds_to_samples(500);
        state.tone_phase = 0;
        state.tone_phase_rate = 0;
        state.tone_level = 0;
        Array.Clear(state.rx_digits);
        state.rx_digits_len = 0;

        state.logging = LoggingApi.span_log_init(
            null,
            LoggingApi.SPAN_LOG_NONE,
            null);

        LoggingApi.span_log_set_protocol(
            state.logging,
            "Ademco");

        state.dtmf = Dtmf.dtmf_rx_init(
            null,
            DtmfDigitDelivery,
            state);

        state.MarkInitialized();
        return state;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_release(
        AdemcoContactIdReceiverState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_receiver_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_receiver_free(
        AdemcoContactIdReceiverState? state) {
        state?.Dispose();
        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="maxSamples">The max samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_tx(
        AdemcoContactIdSenderState state,
        Span<short> amplitude,
        int maxSamples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (maxSamples < 0 || maxSamples > amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(maxSamples));

        int sample = 0;

        while (sample < maxSamples) {
            int samples;

            switch (state.step) {
                case 0:
                    if (!state.clear_to_send)
                        return 0;

                    state.clear_to_send = false;
                    state.step++;
                    state.remaining_samples = Telephony.milliseconds_to_samples(250);
                    goto case 1;

                case 1:
                    samples = Math.Min(
                        state.remaining_samples,
                        maxSamples - sample);

                    amplitude.Slice(sample, samples).Clear();
                    state.remaining_samples -= samples;

                    if (state.remaining_samples > 0)
                        return sample + samples;

                    LoggingApi.span_log(
                        state.logging,
                        LoggingApi.SPAN_LOG_FLOW,
                        "Pre-send silence finished\n");

                    state.step++;
                    sample += samples;
                    break;

                case 2:
                    samples = Dtmf.dtmf_tx(
                        state.dtmf,
                        amplitude.Slice(sample, maxSamples - sample));

                    if (samples == 0) {
                        state.clear_to_send = false;
                        state.step = 0;
                        return sample;
                    }

                    sample += samples;
                    break;

                default:
                    return sample;
            }
        }

        return sample;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_tx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="maxSamples">The max samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_tx(
        AdemcoContactIdSenderState state,
        short[] amplitude,
        int maxSamples) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return ademco_contactid_sender_tx(
            state,
            amplitude.AsSpan(),
            maxSamples);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_rx(
        AdemcoContactIdSenderState state,
        ReadOnlySpan<short> amplitude,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();

        if (samples < 0 || samples > amplitude.Length)
            throw new ArgumentOutOfRangeException(nameof(samples));

        int sample = 0;

        while (sample < samples) {
            int limit;

            if (samples - sample >= GOERTZEL_SAMPLES_PER_BLOCK - state.current_sample)
                limit = sample + GOERTZEL_SAMPLES_PER_BLOCK - state.current_sample;
            else
                limit = samples;

            for (int j = sample; j < limit; j++) {
#if TKFAXENGINE_USE_FIXED_POINT
                short adjusted = ToneDetect.PreadjustAmplitude(amplitude[j]);
                state.energy = unchecked(
                    state.energy + adjusted * adjusted);
                state.tone_1400.SampleAdjusted(adjusted);
                state.tone_2300.SampleAdjusted(adjusted);
#else
                float adjusted = ToneDetect.PreadjustAmplitude(amplitude[j]);
                state.energy += adjusted * adjusted;
                state.tone_1400.SampleAdjusted(adjusted);
                state.tone_2300.SampleAdjusted(adjusted);
#endif
            }

            state.current_sample += limit - sample;
            sample = limit;

            if (state.current_sample < GOERTZEL_SAMPLES_PER_BLOCK)
                continue;

#if TKFAXENGINE_USE_FIXED_POINT
            int energy1400 = state.tone_1400.Result();
            int energy2300 = state.tone_2300.Result();
#else
            float energy1400 = state.tone_1400.Result();
            float energy2300 = state.tone_2300.Result();
#endif

            int hit = 0;

            if (energy1400 > DetectionThreshold ||
                energy2300 > DetectionThreshold) {
                if (energy1400 > energy2300) {
                    if (energy1400 > ToneToTotalEnergy * state.energy)
                        hit = 1;
                } else {
                    if (energy2300 > ToneToTotalEnergy * state.energy)
                        hit = 2;
                }
            }

            if (hit != state.in_tone &&
                hit == state.last_hit) {
                switch (state.tone_state) {
                    case 0:
                        if (hit == 1) {
                            LoggingApi.span_log(
                                state.logging,
                                LoggingApi.SPAN_LOG_FLOW,
                                "Receiving initial 1400Hz\n");

                            state.in_tone = hit;
                            state.tone_state = 1;
                            state.duration = 0;
                        }
                        break;

                    case 1:
                        if (hit == 0) {
                            if (state.duration < Telephony.milliseconds_to_samples(70) ||
                                state.duration > Telephony.milliseconds_to_samples(130)) {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Bad initial 1400Hz tone duration\n");

                                state.tone_state = 0;
                            } else {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Received 1400Hz tone\n");

                                state.tone_state = 2;
                            }

                            state.in_tone = hit;
                            state.duration = 0;
                        }
                        break;

                    case 2:
                        if (state.duration < Telephony.milliseconds_to_samples(70) ||
                            state.duration > Telephony.milliseconds_to_samples(130)) {
                            LoggingApi.span_log(
                                state.logging,
                                LoggingApi.SPAN_LOG_FLOW,
                                "Bad silence length\n");

                            state.tone_state = 0;
                            state.in_tone = hit;
                        } else if (hit == 2) {
                            LoggingApi.span_log(
                                state.logging,
                                LoggingApi.SPAN_LOG_FLOW,
                                "Received silence\n");

                            state.tone_state = 3;
                            state.in_tone = hit;
                        } else {
                            state.tone_state = 0;
                            state.in_tone = 0;
                        }

                        state.duration = 0;
                        break;

                    case 3:
                        if (hit == 0) {
                            if (state.duration < Telephony.milliseconds_to_samples(70) ||
                                state.duration > Telephony.milliseconds_to_samples(130)) {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Bad initial 2300Hz tone duration\n");

                                state.tone_state = 0;
                            } else {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Received 2300Hz\n");

                                state.callback?.Invoke(
                                    state.callback_user_data,
                                    -1,
                                    0,
                                    0);

                                state.tone_state = 4;
                                state.clear_to_send = true;
                                state.tries = 0;

                                if (state.tx_digits_len != 0)
                                    state.timer = Telephony.milliseconds_to_samples(3000);
                            }

                            state.in_tone = hit;
                            state.duration = 0;
                        }
                        break;

                    case 4:
                        if (hit == 1) {
                            LoggingApi.span_log(
                                state.logging,
                                LoggingApi.SPAN_LOG_FLOW,
                                "Receiving kissoff\n");

                            state.tone_state = 5;
                            state.in_tone = hit;
                            state.duration = 0;
                        }
                        break;

                    case 5:
                        if (hit == 0) {
                            state.busy = false;

                            if (state.duration < Telephony.milliseconds_to_samples(400) ||
                                state.duration > Telephony.milliseconds_to_samples(1500)) {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Bad kissoff duration %d\n",
                                    state.duration);

                                if (++state.tries < 4) {
                                    Dtmf.dtmf_tx_put(
                                        state.dtmf,
                                        new string(
                                            state.tx_digits,
                                            0,
                                            state.tx_digits_len),
                                        state.tx_digits_len);

                                    state.timer = Telephony.milliseconds_to_samples(3000);
                                    state.tone_state = 4;
                                } else {
                                    state.timer = 0;
                                    state.callback?.Invoke(
                                        state.callback_user_data,
                                        0,
                                        0,
                                        0);
                                }
                            } else {
                                LoggingApi.span_log(
                                    state.logging,
                                    LoggingApi.SPAN_LOG_FLOW,
                                    "Received good kissoff\n");

                                state.clear_to_send = true;
                                state.tx_digits_len = 0;
                                state.callback?.Invoke(
                                    state.callback_user_data,
                                    1,
                                    0,
                                    0);

                                state.tone_state = 4;
                                state.clear_to_send = true;
                                state.tries = 0;

                                if (state.tx_digits_len != 0)
                                    state.timer = Telephony.milliseconds_to_samples(3000);
                            }

                            state.in_tone = hit;
                            state.duration = 0;
                        }
                        break;
                }
            }

            state.last_hit = hit;
            state.duration += GOERTZEL_SAMPLES_PER_BLOCK;

            if (state.timer > 0) {
                state.timer -= GOERTZEL_SAMPLES_PER_BLOCK;

                if (state.timer <= 0) {
                    LoggingApi.span_log(
                        state.logging,
                        LoggingApi.SPAN_LOG_FLOW,
                        "Timer expired\n");

                    if (state.tone_state == 4 &&
                        state.tx_digits_len != 0) {
                        if (++state.tries < 4) {
                            Dtmf.dtmf_tx_put(
                                state.dtmf,
                                new string(
                                    state.tx_digits,
                                    0,
                                    state.tx_digits_len),
                                state.tx_digits_len);

                            state.timer = Telephony.milliseconds_to_samples(3000);
                        } else {
                            state.timer = 0;
                            state.callback?.Invoke(
                                state.callback_user_data,
                                0,
                                0,
                                0);
                        }
                    }
                }
            }

#if TKFAXENGINE_USE_FIXED_POINT
            state.energy = 0;
#else
            state.energy = 0.0f;
#endif
            state.current_sample = 0;
        }

        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_rx</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="amplitude">The amplitude value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_rx(
        AdemcoContactIdSenderState state,
        short[] amplitude,
        int samples) {
        ArgumentNullException.ThrowIfNull(amplitude);
        return ademco_contactid_sender_rx(
            state,
            amplitude.AsSpan(),
            samples);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_fillin</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="samples">The samples value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_fillin(
        AdemcoContactIdSenderState state,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        _ = samples;

        state.tone_1400.Reset();
        state.tone_2300.Reset();

#if TKFAXENGINE_USE_FIXED_POINT
        state.energy = 0;
#else
        state.energy = 0.0f;
#endif

        state.current_sample = 0;
        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_put</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="report">The report value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_put(
        AdemcoContactIdSenderState state,
        AdemcoContactIdReport report) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(report);
        state.ThrowIfDisposed();

        if (state.busy)
            return -1;

        state.tx_digits_len = encode_msg(
            state.tx_digits,
            report);

        if (state.tx_digits_len < 0)
            return -1;

        state.busy = true;

        return Dtmf.dtmf_tx_put(
            state.dtmf,
            new string(
                state.tx_digits,
                0,
                state.tx_digits_len),
            state.tx_digits_len);
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_get_logging_state</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static SpanLogState ademco_contactid_sender_get_logging_state(
        AdemcoContactIdSenderState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.logging;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_set_realtime_callback</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    public static void ademco_contactid_sender_set_realtime_callback(
        AdemcoContactIdSenderState state,
        AdemcoContactIdToneReportHandler? callback,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.callback = callback;
        state.callback_user_data = userData;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_init</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <param name="callback">The callback value.</param>
    /// <param name="userData">The user data value.</param>
    /// <returns>The result of the operation.</returns>
    public static AdemcoContactIdSenderState ademco_contactid_sender_init(
        AdemcoContactIdSenderState? state,
        AdemcoContactIdToneReportHandler? callback,
        object? userData) {
        state ??= new AdemcoContactIdSenderState(false);

        state.dtmf.Dispose();
        state.tone_1400.Dispose();
        state.tone_2300.Dispose();
        state.logging.Dispose();

        state.callback = callback;
        state.callback_user_data = userData;

        state.step = 0;
        state.remaining_samples = Telephony.milliseconds_to_samples(100);
        state.current_sample = 0;
        Array.Clear(state.tx_digits);
        state.tx_digits_len = 0;
        state.tries = 0;
        state.tone_state = 0;
        state.duration = 0;
        state.last_hit = 0;
        state.in_tone = 0;
        state.clear_to_send = false;
        state.timer = 0;
        state.busy = false;

#if TKFAXENGINE_USE_FIXED_POINT
        state.threshold = DetectionThreshold;
        state.energy = 0;
#else
        state.threshold = DetectionThreshold;
        state.energy = 0.0f;
#endif

        state.logging = LoggingApi.span_log_init(
            null,
            LoggingApi.SPAN_LOG_NONE,
            null);

        LoggingApi.span_log_set_protocol(
            state.logging,
            "Ademco");

        state.tone_1400 = new GoertzelState(Tone1400Descriptor);
        state.tone_2300 = new GoertzelState(Tone2300Descriptor);

        state.dtmf = Dtmf.dtmf_tx_init(
            null,
            null,
            null);

        Dtmf.dtmf_tx_set_timing(
            state.dtmf,
            55,
            55);

        state.MarkInitialized();
        return state;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_release</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_release(
        AdemcoContactIdSenderState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

    /// <summary>
    /// Performs the <c>ademco_contactid_sender_free</c> operation.
    /// </summary>
    /// <param name="state">The state value.</param>
    /// <returns>The result of the operation.</returns>
    public static int ademco_contactid_sender_free(
        AdemcoContactIdSenderState? state) {
        state?.Dispose();
        return 0;
    }

    private static bool TryEncodeMessage(
        AdemcoContactIdReport report,
        out string message) {
        string source = string.Format(
            CultureInfo.InvariantCulture,
            "{0:X4}{1:X2}{2:X1}{3:X3}{4:X2}{5:X3}",
            report.acct,
            report.mt,
            report.q,
            report.xyz,
            report.gg,
            report.ccc);

        char[] encoded = new char[source.Length + 1];
        int sum = 0;
        int index = 0;

        for (; index < source.Length; index++) {
            char digit = source[index];
            int value;

            if (digit == 'A') {
                message = string.Empty;
                return false;
            }

            if (digit > '9') {
                value = digit - ('A' - 10);
                encoded[index] = value switch {
                    10 => 'D',
                    11 => '*',
                    12 => '#',
                    13 => 'A',
                    14 => 'B',
                    15 => 'C',
                    _ => digit
                };
            } else {
                encoded[index] = digit;
                value = digit - '0';

                if (value == 0)
                    value = 10;
            }

            sum += value;
        }

        int checksum = ((sum + 15) / 15) * 15 - sum;

        encoded[index++] = checksum switch {
            0 => 'C',
            <= 9 => (char)('0' + checksum),
            10 => 'D',
            11 => '*',
            12 => '#',
            13 => 'A',
            14 => 'B',
            15 => 'C',
            _ => throw new InvalidOperationException(
                "The Ademco checksum is outside the valid DTMF range.")
        };

        message = new string(encoded, 0, index);
        return true;
    }

    private static bool TryParseHex(
        ReadOnlySpan<char> value,
        out int result) {
        return int.TryParse(
            value,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out result);
    }

    private static void GenerateTone(
        Span<short> amplitude,
        ref uint phase,
        int phaseRate,
        short level) {
        for (int index = 0; index < amplitude.Length; index++) {
            amplitude[index] = Dds.GenerateModulated(
                ref phase,
                phaseRate,
                level,
                0);
        }
    }

    private static void DtmfDigitDelivery(
        object? userData,
        string digits,
        int length) {
        if (userData is not AdemcoContactIdReceiverState state)
            return;

        int count = Math.Min(length, digits.Length);

        for (int index = 0; index < count; index++) {
            state.rx_digits[state.rx_digits_len++] = digits[index];

            if (state.rx_digits_len != 16)
                continue;

            state.rx_digits[16] = '\0';
            AdemcoContactIdReport report = new();

            if (decode_msg(
                    report,
                    state.rx_digits.AsSpan(0, 16)) == 0) {
                ademco_contactid_receiver_log_msg(
                    state,
                    report);

                state.callback?.Invoke(
                    state.callback_user_data,
                    report);

                state.step++;
            }

            state.rx_digits_len = 0;
        }
    }

}
