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
    public int acct;
    public int mt;
    public int q;
    public int xyz;
    public int gg;
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

    public int Step => step;

    public int RemainingSamples => remaining_samples;

    public SpanLogState Logging => logging;

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

    public int Step => step;

    public int RemainingSamples => remaining_samples;

    public int Tries => tries;

    public bool Busy => busy;

    public bool ClearToSend => clear_to_send;

    public SpanLogState Logging => logging;

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
    public const int ADEMCO_CONTACTID_MESSAGE_TYPE_18 = 0x18;
    public const int ADEMCO_CONTACTID_MESSAGE_TYPE_98 = 0x98;
    public const int ADEMCO_CONTACTID_QUALIFIER_NEW_EVENT = 1;
    public const int ADEMCO_CONTACTID_QUALIFIER_NEW_RESTORE = 3;
    public const int ADEMCO_CONTACTID_QUALIFIER_STATUS_REPORT = 6;
    public const int ADEMCO_CONTACTID_DATA_IS_ZONE = 0;
    public const int ADEMCO_CONTACTID_DATA_IS_USER = 1;
    public const int ADEMCO_CONTACTID_MEDICAL = 0x100;
    public const int ADEMCO_CONTACTID_PERSONAL_EMERGENCY = 0x101;
    public const int ADEMCO_CONTACTID_FAIL_TO_REPORT_IN = 0x102;
    public const int ADEMCO_CONTACTID_FIRE = 0x110;
    public const int ADEMCO_CONTACTID_SMOKE = 0x111;
    public const int ADEMCO_CONTACTID_COMBUSTION = 0x112;
    public const int ADEMCO_CONTACTID_WATER_FLOW = 0x113;
    public const int ADEMCO_CONTACTID_HEAT = 0x114;
    public const int ADEMCO_CONTACTID_PULL_STATION = 0x115;
    public const int ADEMCO_CONTACTID_DUCT = 0x116;
    public const int ADEMCO_CONTACTID_FLAME = 0x117;
    public const int ADEMCO_CONTACTID_NEAR_ALARM_A = 0x118;
    public const int ADEMCO_CONTACTID_PANIC = 0x120;
    public const int ADEMCO_CONTACTID_DURESS = 0x121;
    public const int ADEMCO_CONTACTID_SILENT = 0x122;
    public const int ADEMCO_CONTACTID_AUDIBLE = 0x123;
    public const int ADEMCO_CONTACTID_DURESS_ACCESS_GRANTED = 0x124;
    public const int ADEMCO_CONTACTID_DURESS_EGRESS_GRANTED = 0x125;
    public const int ADEMCO_CONTACTID_BURGLARY = 0x130;
    public const int ADEMCO_CONTACTID_PERIMETER = 0x131;
    public const int ADEMCO_CONTACTID_INTERIOR = 0x132;
    public const int ADEMCO_CONTACTID_24_HOUR_SAFE = 0x133;
    public const int ADEMCO_CONTACTID_ENTRY_EXIT = 0x134;
    public const int ADEMCO_CONTACTID_DAY_NIGHT = 0x135;
    public const int ADEMCO_CONTACTID_OUTDOOR = 0x136;
    public const int ADEMCO_CONTACTID_TAMPER = 0x137;
    public const int ADEMCO_CONTACTID_NEAR_ALARM_B = 0x138;
    public const int ADEMCO_CONTACTID_INTRUSION_VERIFIER = 0x139;
    public const int ADEMCO_CONTACTID_GENERAL_ALARM = 0x140;
    public const int ADEMCO_CONTACTID_POLLING_LOOP_OPEN_A = 0x141;
    public const int ADEMCO_CONTACTID_POLLING_LOOP_SHORT_A = 0x142;
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_A = 0x143;
    public const int ADEMCO_CONTACTID_SENSOR_TAMPER_A = 0x144;
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_TAMPER = 0x145;
    public const int ADEMCO_CONTACTID_SILENT_BURGLARY = 0x146;
    public const int ADEMCO_CONTACTID_SENSOR_SUPERVISION_FAILURE = 0x147;
    public const int ADEMCO_CONTACTID_24_HOUR_NONBURGLARY = 0x150;
    public const int ADEMCO_CONTACTID_GAS_DETECTED = 0x151;
    public const int ADEMCO_CONTACTID_REFRIGERATION = 0x152;
    public const int ADEMCO_CONTACTID_LOSS_OF_HEAT = 0x153;
    public const int ADEMCO_CONTACTID_WATER_LEAKAGE = 0x154;
    public const int ADEMCO_CONTACTID_FOIL_BREAK = 0x155;
    public const int ADEMCO_CONTACTID_DAY_TROUBLE = 0x156;
    public const int ADEMCO_CONTACTID_LOW_BOTTLED_GAS_LEVEL = 0x157;
    public const int ADEMCO_CONTACTID_HIGH_TEMP = 0x158;
    public const int ADEMCO_CONTACTID_LOW_TEMP = 0x159;
    public const int ADEMCO_CONTACTID_LOSS_OF_AIR_FLOW = 0x161;
    public const int ADEMCO_CONTACTID_CARBON_MONOXIDE_DETECTED = 0x162;
    public const int ADEMCO_CONTACTID_TANK_LEVEL = 0x163;
    public const int ADEMCO_CONTACTID_FIRE_SUPERVISORY = 0x200;
    public const int ADEMCO_CONTACTID_LOW_WATER_PRESSURE = 0x201;
    public const int ADEMCO_CONTACTID_LOW_CO2 = 0x202;
    public const int ADEMCO_CONTACTID_GATE_VALVE_SENSOR = 0x203;
    public const int ADEMCO_CONTACTID_LOW_WATER_LEVEL = 0x204;
    public const int ADEMCO_CONTACTID_PUMP_ACTIVATED = 0x205;
    public const int ADEMCO_CONTACTID_PUMP_FAILURE = 0x206;
    public const int ADEMCO_CONTACTID_SYSTEM_TROUBLE = 0x300;
    public const int ADEMCO_CONTACTID_AC_LOSS = 0x301;
    public const int ADEMCO_CONTACTID_LOW_SYSTEM_BATTERY = 0x302;
    public const int ADEMCO_CONTACTID_RAM_CHECKSUM_BAD = 0x303;
    public const int ADEMCO_CONTACTID_ROM_CHECKSUM_BAD = 0x304;
    public const int ADEMCO_CONTACTID_SYSTEM_RESET = 0x305;
    public const int ADEMCO_CONTACTID_PANEL_PROGRAMMING_CHANGED = 0x306;
    public const int ADEMCO_CONTACTID_SELFTEST_FAILURE = 0x307;
    public const int ADEMCO_CONTACTID_SYSTEM_SHUTDOWN = 0x308;
    public const int ADEMCO_CONTACTID_BATTERY_TEST_FAILURE = 0x309;
    public const int ADEMCO_CONTACTID_GROUND_FAULT = 0x310;
    public const int ADEMCO_CONTACTID_BATTERY_MISSING_DEAD = 0x311;
    public const int ADEMCO_CONTACTID_POWER_SUPPLY_OVERCURRENT = 0x312;
    public const int ADEMCO_CONTACTID_ENGINEER_RESET = 0x313;
    public const int ADEMCO_CONTACTID_SOUNDER_RELAY = 0x320;
    public const int ADEMCO_CONTACTID_BELL_1 = 0x321;
    public const int ADEMCO_CONTACTID_BELL_2 = 0x322;
    public const int ADEMCO_CONTACTID_ALARM_RELAY = 0x323;
    public const int ADEMCO_CONTACTID_TROUBLE_RELAY = 0x324;
    public const int ADEMCO_CONTACTID_REVERSING_RELAY = 0x325;
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3 = 0x326;
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4 = 0x327;
    public const int ADEMCO_CONTACTID_SYSTEM_PERIPHERAL_TROUBLE = 0x330;
    public const int ADEMCO_CONTACTID_POLLING_LOOP_OPEN_B = 0x331;
    public const int ADEMCO_CONTACTID_POLLING_LOOP_SHORT_B = 0x332;
    public const int ADEMCO_CONTACTID_EXPANSION_MODULE_FAILURE_B = 0x333;
    public const int ADEMCO_CONTACTID_REPEATER_FAILURE = 0x334;
    public const int ADEMCO_CONTACTID_LOCAL_PRINTER_OUT_OF_PAPER = 0x335;
    public const int ADEMCO_CONTACTID_LOCAL_PRINTER_FAILURE = 0x336;
    public const int ADEMCO_CONTACTID_EXP_MODULE_DC_LOSS = 0x337;
    public const int ADEMCO_CONTACTID_EXP_MODULE_LOW_BATTERY = 0x338;
    public const int ADEMCO_CONTACTID_EXP_MODULE_RESET = 0x339;
    public const int ADEMCO_CONTACTID_EXP_MODULE_TAMPER = 0x341;
    public const int ADEMCO_CONTACTID_EXP_MODULE_AC_LOSS = 0x342;
    public const int ADEMCO_CONTACTID_EXP_MODULE_SELFTEST_FAIL = 0x343;
    public const int ADEMCO_CONTACTID_RF_RECEIVER_JAM_DETECT = 0x344;
    public const int ADEMCO_CONTACTID_COMMUNICATION_TROUBLE = 0x350;
    public const int ADEMCO_CONTACTID_TELCO_1_FAULT = 0x351;
    public const int ADEMCO_CONTACTID_TELCO_2_FAULT = 0x352;
    public const int ADEMCO_CONTACTID_LONG_RANGE_RADIO_TRANSMITTER_FAULT = 0x353;
    public const int ADEMCO_CONTACTID_FAILURE_TO_COMMUNICATE_EVENT = 0x354;
    public const int ADEMCO_CONTACTID_LOSS_OF_RADIO_SUPERVISION = 0x355;
    public const int ADEMCO_CONTACTID_LOSS_OF_CENTRAL_POLLING = 0x356;
    public const int ADEMCO_CONTACTID_LONG_RANGE_RADIO_VSWR_PROBLEM = 0x357;
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP = 0x370;
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP_OPEN = 0x371;
    public const int ADEMCO_CONTACTID_PROTECTION_LOOP_SHORT = 0x372;
    public const int ADEMCO_CONTACTID_FIRE_TROUBLE = 0x373;
    public const int ADEMCO_CONTACTID_EXIT_ERROR_ALARM_ZONE = 0x374;
    public const int ADEMCO_CONTACTID_PANIC_ZONE_TROUBLE = 0x375;
    public const int ADEMCO_CONTACTID_HOLDUP_ZONE_TROUBLE = 0x376;
    public const int ADEMCO_CONTACTID_SWINGER_TROUBLE = 0x377;
    public const int ADEMCO_CONTACTID_CROSSZONE_TROUBLE = 0x378;
    public const int ADEMCO_CONTACTID_SENSOR_TROUBLE = 0x380;
    public const int ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RF = 0x381;
    public const int ADEMCO_CONTACTID_LOSS_OF_SUPERVISION__RPM = 0x382;
    public const int ADEMCO_CONTACTID_SENSOR_TAMPER_B = 0x383;
    public const int ADEMCO_CONTACTID_RF_LOW_BATTERY = 0x384;
    public const int ADEMCO_CONTACTID_SMOKE_DETECTOR_HIGH_SENSITIVITY = 0x385;
    public const int ADEMCO_CONTACTID_SMOKE_DETECTOR_LOW_SENSITIVITY = 0x386;
    public const int ADEMCO_CONTACTID_INTRUSION_DETECTOR_HIGH_SENSITIVITY = 0x387;
    public const int ADEMCO_CONTACTID_INTRUSION_DETECTOR_LOW_SENSITIVITY = 0x388;
    public const int ADEMCO_CONTACTID_SENSOR_SELFTEST_FAILURE = 0x389;
    public const int ADEMCO_CONTACTID_SENSOR_WATCH_TROUBLE = 0x391;
    public const int ADEMCO_CONTACTID_DRIFT_COMPENSATION_ERROR = 0x392;
    public const int ADEMCO_CONTACTID_MAINTENANCE_ALERT = 0x393;
    public const int ADEMCO_CONTACTID_OPEN_CLOSE = 0x400;
    public const int ADEMCO_CONTACTID_OC_BY_USER = 0x401;
    public const int ADEMCO_CONTACTID_GROUP_OC = 0x402;
    public const int ADEMCO_CONTACTID_AUTOMATIC_OC = 0x403;
    public const int ADEMCO_CONTACTID_LATE_TO_OC = 0x404;
    public const int ADEMCO_CONTACTID_DEFERRED_OC = 0x405;
    public const int ADEMCO_CONTACTID_CANCEL = 0x406;
    public const int ADEMCO_CONTACTID_REMOTE_ARM_DISARM = 0x407;
    public const int ADEMCO_CONTACTID_QUICK_ARM = 0x408;
    public const int ADEMCO_CONTACTID_KEYSWITCH_OC = 0x409;
    public const int ADEMCO_CONTACTID_ARMED_STAY = 0x441;
    public const int ADEMCO_CONTACTID_KEYSWITCH_ARMED_STAY = 0x442;
    public const int ADEMCO_CONTACTID_EXCEPTION_OC = 0x450;
    public const int ADEMCO_CONTACTID_EARLY_OC = 0x451;
    public const int ADEMCO_CONTACTID_LATE_OC = 0x452;
    public const int ADEMCO_CONTACTID_FAILED_TO_OPEN = 0x453;
    public const int ADEMCO_CONTACTID_FAILED_TO_CLOSE = 0x454;
    public const int ADEMCO_CONTACTID_AUTOARM_FAILED = 0x455;
    public const int ADEMCO_CONTACTID_PARTIAL_ARM = 0x456;
    public const int ADEMCO_CONTACTID_EXIT_ERROR_USER = 0x457;
    public const int ADEMCO_CONTACTID_USER_ON_PREMISES = 0x458;
    public const int ADEMCO_CONTACTID_RECENT_CLOSE = 0x459;
    public const int ADEMCO_CONTACTID_WRONG_CODE_ENTRY = 0x461;
    public const int ADEMCO_CONTACTID_LEGAL_CODE_ENTRY = 0x462;
    public const int ADEMCO_CONTACTID_REARM_AFTER_ALARM = 0x463;
    public const int ADEMCO_CONTACTID_AUTOARM_TIME_EXTENDED = 0x464;
    public const int ADEMCO_CONTACTID_PANIC_ALARM_RESET = 0x465;
    public const int ADEMCO_CONTACTID_SERVICE_ON_OFF_PREMISES = 0x466;
    public const int ADEMCO_CONTACTID_CALLBACK_REQUEST_MADE = 0x411;
    public const int ADEMCO_CONTACTID_SUCCESSFUL_DOWNLOAD_ACCESS = 0x412;
    public const int ADEMCO_CONTACTID_UNSUCCESSFUL_ACCESS = 0x413;
    public const int ADEMCO_CONTACTID_SYSTEM_SHUTDOWN_COMMAND_RECEIVED = 0x414;
    public const int ADEMCO_CONTACTID_DIALER_SHUTDOWN_COMMAND_RECEIVED = 0x415;
    public const int ADEMCO_CONTACTID_SUCCESSFUL_UPLOAD = 0x416;
    public const int ADEMCO_CONTACTID_ACCESS_DENIED = 0x421;
    public const int ADEMCO_CONTACTID_ACCESS_REPORT_BY_USER = 0x422;
    public const int ADEMCO_CONTACTID_FORCED_ACCESS = 0x423;
    public const int ADEMCO_CONTACTID_EGRESS_DENIED = 0x424;
    public const int ADEMCO_CONTACTID_EGRESS_GRANTED = 0x425;
    public const int ADEMCO_CONTACTID_ACCESS_DOOR_PROPPED_OPEN = 0x426;
    public const int ADEMCO_CONTACTID_ACCESS_POINT_DOOR_STATUS_MONITOR_TROUBLE = 0x427;
    public const int ADEMCO_CONTACTID_ACCESS_POINT_REQUEST_TO_EXIT_TROUBLE = 0x428;
    public const int ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_ENTRY = 0x429;
    public const int ADEMCO_CONTACTID_ACCESS_PROGRAM_MODE_EXIT = 0x430;
    public const int ADEMCO_CONTACTID_ACCESS_THREAT_LEVEL_CHANGE = 0x431;
    public const int ADEMCO_CONTACTID_ACCESS_RELAY_TRIGGER_FAIL = 0x432;
    public const int ADEMCO_CONTACTID_ACCESS_RTE_SHUNT = 0x433;
    public const int ADEMCO_CONTACTID_ACCESS_DSM_SHUNT = 0x434;
    public const int ADEMCO_CONTACTID_ACCESS_READER_DISABLE = 0x501;
    public const int ADEMCO_CONTACTID_SOUNDER_RELAY_DISABLE = 0x520;
    public const int ADEMCO_CONTACTID_BELL_1_DISABLE = 0x521;
    public const int ADEMCO_CONTACTID_BELL_2_DISABLE = 0x522;
    public const int ADEMCO_CONTACTID_ALARM_RELAY_DISABLE = 0x523;
    public const int ADEMCO_CONTACTID_TROUBLE_RELAY_DISABLE = 0x524;
    public const int ADEMCO_CONTACTID_REVERSING_RELAY_DISABLE = 0x525;
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_3_DISABLE = 0x526;
    public const int ADEMCO_CONTACTID_NOTIFICATION_APPLIANCE_CKT_4_DISABLE = 0x527;
    public const int ADEMCO_CONTACTID_MODULE_ADDED = 0x531;
    public const int ADEMCO_CONTACTID_MODULE_REMOVED = 0x532;
    public const int ADEMCO_CONTACTID_DIALER_DISABLED = 0x551;
    public const int ADEMCO_CONTACTID_RADIO_TRANSMITTER_DISABLED = 0x552;
    public const int ADEMCO_CONTACTID_REMOTE_UPLOAD_DOWNLOAD_DISABLED = 0x553;
    public const int ADEMCO_CONTACTID_ZONE_SENSOR_BYPASS = 0x570;
    public const int ADEMCO_CONTACTID_FIRE_BYPASS = 0x571;
    public const int ADEMCO_CONTACTID_24_HOUR_ZONE_BYPASS = 0x572;
    public const int ADEMCO_CONTACTID_BURG_BYPASS = 0x573;
    public const int ADEMCO_CONTACTID_GROUP_BYPASS = 0x574;
    public const int ADEMCO_CONTACTID_SWINGER_BYPASS = 0x575;
    public const int ADEMCO_CONTACTID_ACCESS_ZONE_SHUNT = 0x576;
    public const int ADEMCO_CONTACTID_ACCESS_POINT_BYPASS = 0x577;
    public const int ADEMCO_CONTACTID_MANUAL_TRIGGER_TEST_REPORT = 0x601;
    public const int ADEMCO_CONTACTID_PERIODIC_TEST_REPORT = 0x602;
    public const int ADEMCO_CONTACTID_PERIODIC_RF_TRANSMISSION = 0x603;
    public const int ADEMCO_CONTACTID_FIRE_TEST = 0x604;
    public const int ADEMCO_CONTACTID_STATUS_REPORT_TO_FOLLOW = 0x605;
    public const int ADEMCO_CONTACTID_LISTENIN_TO_FOLLOW = 0x606;
    public const int ADEMCO_CONTACTID_WALK_TEST_MODE = 0x607;
    public const int ADEMCO_CONTACTID_PERIODIC_TEST__SYSTEM_TROUBLE_PRESENT = 0x608;
    public const int ADEMCO_CONTACTID_VIDEO_TRANSMITTER_ACTIVE = 0x609;
    public const int ADEMCO_CONTACTID_POINT_TESTED_OK = 0x611;
    public const int ADEMCO_CONTACTID_POINT_NOT_TESTED = 0x612;
    public const int ADEMCO_CONTACTID_INTRUSION_ZONE_WALK_TESTED = 0x613;
    public const int ADEMCO_CONTACTID_FIRE_ZONE_WALK_TESTED = 0x614;
    public const int ADEMCO_CONTACTID_PANIC_ZONE_WALK_TESTED = 0x615;
    public const int ADEMCO_CONTACTID_SERVICE_REQUEST = 0x616;
    public const int ADEMCO_CONTACTID_EVENT_LOG_RESET = 0x621;
    public const int ADEMCO_CONTACTID_EVENT_LOG_50PC_FULL = 0x622;
    public const int ADEMCO_CONTACTID_EVENT_LOG_90PC_FULL = 0x623;
    public const int ADEMCO_CONTACTID_EVENT_LOG_OVERFLOW = 0x624;
    public const int ADEMCO_CONTACTID_TIME_DATE_RESET = 0x625;
    public const int ADEMCO_CONTACTID_TIME_DATE_INACCURATE = 0x626;
    public const int ADEMCO_CONTACTID_PROGRAM_MODE_ENTRY = 0x627;
    public const int ADEMCO_CONTACTID_PROGRAM_MODE_EXIT = 0x628;
    public const int ADEMCO_CONTACTID_32_HOUR_EVENT_LOG_MARKER = 0x629;
    public const int ADEMCO_CONTACTID_SCHEDULE_CHANGE = 0x630;
    public const int ADEMCO_CONTACTID_EXCEPTION_SCHEDULE_CHANGE = 0x631;
    public const int ADEMCO_CONTACTID_ACCESS_SCHEDULE_CHANGE = 0x632;
    public const int ADEMCO_CONTACTID_SENIOR_WATCH_TROUBLE = 0x641;
    public const int ADEMCO_CONTACTID_LATCHKEY_SUPERVISION = 0x642;
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_1 = 0x651;
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_2 = 0x652;
    public const int ADEMCO_CONTACTID_RESERVED_FOR_ADEMCO_USE_3 = 0x653;
    public const int ADEMCO_CONTACTID_SYSTEM_INACTIVITY = 0x654;
    public const int ADEMCO_CONTACTID_DOWNLOAD_ABORT = 0x900;
    public const int ADEMCO_CONTACTID_DOWNLOAD_START_END = 0x901;
    public const int ADEMCO_CONTACTID_DOWNLOAD_INTERRUPTED = 0x902;
    public const int ADEMCO_CONTACTID_AUTOCLOSE_WITH_BYPASS = 0x910;
    public const int ADEMCO_CONTACTID_BYPASS_CLOSING = 0x911;
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

    public static string ademco_contactid_msg_qualifier_to_str(int q) {
        return q switch {
            ADEMCO_CONTACTID_QUALIFIER_NEW_EVENT => "New event",
            ADEMCO_CONTACTID_QUALIFIER_NEW_RESTORE => "New restore",
            ADEMCO_CONTACTID_QUALIFIER_STATUS_REPORT => "Status report",
            _ => "???"
        };
    }

    public static string ademco_contactid_event_to_str(int xyz) {
        for (int entry = 0; entry < AdemcoCodes.Length; entry++) {
            if (xyz == AdemcoCodes[entry].Code)
                return AdemcoCodes[entry].Name;
        }

        return "???";
    }

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

    public static int encode_msg(
        char[] buffer,
        AdemcoContactIdReport report) {
        ArgumentNullException.ThrowIfNull(buffer);
        return encode_msg(buffer.AsSpan(), report);
    }

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

    public static int decode_msg(
        AdemcoContactIdReport report,
        string buffer) {
        ArgumentNullException.ThrowIfNull(buffer);
        return decode_msg(report, buffer.AsSpan());
    }

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

    public static int ademco_contactid_receiver_fillin(
        AdemcoContactIdReceiverState state,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return Dtmf.dtmf_rx_fillin(state.dtmf, samples);
    }

    public static SpanLogState ademco_contactid_receiver_get_logging_state(
        AdemcoContactIdReceiverState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.logging;
    }

    public static void ademco_contactid_receiver_set_realtime_callback(
        AdemcoContactIdReceiverState state,
        AdemcoContactIdReportHandler? callback,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.callback = callback;
        state.callback_user_data = userData;
    }

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

    public static int ademco_contactid_receiver_release(
        AdemcoContactIdReceiverState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

    public static int ademco_contactid_receiver_free(
        AdemcoContactIdReceiverState? state) {
        state?.Dispose();
        return 0;
    }

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

    public static SpanLogState ademco_contactid_sender_get_logging_state(
        AdemcoContactIdSenderState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return state.logging;
    }

    public static void ademco_contactid_sender_set_realtime_callback(
        AdemcoContactIdSenderState state,
        AdemcoContactIdToneReportHandler? callback,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        state.callback = callback;
        state.callback_user_data = userData;
    }

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

    public static int ademco_contactid_sender_release(
        AdemcoContactIdSenderState state) {
        ArgumentNullException.ThrowIfNull(state);
        state.ThrowIfDisposed();
        return 0;
    }

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
