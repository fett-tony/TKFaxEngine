/*
 * TKFaxEngineFX - a series of DSP components for telephony
 *
 * data_modems.cs - direct C# conversion of data_modems.h and data_modems.c
 *
 * Written by Steve Underwood <steveu@coppice.org>
 *
 * Copyright (C) 2003, 2005, 2006, 2008, 2011, 2013 Steve Underwood
 *
 * This file preserves the GNU Lesser General Public License version 2.1
 * terms of the original source files.
 */

#nullable enable

using global::TKFaxEngine.Audio;
using global::TKFaxEngine.Modem.V22;
using global::TKFaxEngine.Modem.V34;
using global::TKFaxEngine.Modem.V42;
using global::TKFaxEngine.Modem.V8;
using global::TKFaxEngine.Modem.V32;


namespace TKFaxEngine.Modem;

public delegate int data_modems_control_handler_t(
    data_modems_state_t s,
    object? user_data,
    int op,
    string? num);

public delegate int span_put_msg_func_t(
    object? user_data,
    byte[]? msg,
    int len);

public delegate int span_get_msg_func_t(
    object? user_data,
    byte[] msg,
    int len);

public delegate int data_modems_rx_handler_t(
    object? user_data,
    ReadOnlySpan<short> amp);

public delegate int data_modems_rx_fillin_handler_t(
    object? user_data,
    int len);

public delegate int data_modems_tx_handler_t(
    object? user_data,
    Span<short> amp);

public sealed class data_modems_tones_state_t {
    public ModemConnectTonesTxState? tx;
    public ModemConnectTonesRxState? rx;
}

public sealed class data_modems_fsk_state_t {
    public FskTxState? tx;
    public FskRxState? rx;
}

public sealed class data_modems_modem_state_t {
    public V8State? v8;
    public data_modems_tones_state_t tones = new();
    public data_modems_fsk_state_t fsk = new();
    public V22BisState? v22bis;
    public v32bis_state_t? v32bis;
    public v34_state_t? v34;
    public SilenceGenState silence_gen = new();
}

public sealed class data_modems_state_t {
    public bool calling_party;
    public bool use_tep;
    public bool transmit_on_idle;

    public short data_bits;
    public short parity;
    public short stop_bits;

    public AtInterpreterState? at_state;
    public data_modems_control_handler_t? modem_control_handler;
    public object? modem_control_user_data;

    public SpanGetBitDelegate? get_bit;
    public object? get_user_data;
    public SpanPutBitDelegate? put_bit;
    public object? put_user_data;

    public object? user_data;

    public span_put_msg_func_t? put_msg;
    public span_get_msg_func_t? get_msg;

    public v42_state_t? v42;
    public v42bis_state_t? v42bis;

    public int use_v14;
    public AsyncTransmitter? async_tx;
    public AsyncReceiver? async_rx;

    public long call_samples;

    public data_modems_modem_state_t modems = new();
    public DcRestoreState dc_restore = new();

    public int current_modem;
    public int queued_modem;
    public int queued_baud_rate;
    public int queued_bit_rate;

    public int current_rx_type;
    public int current_tx_type;

    public bool rx_signal_present;
    public bool rx_trained;
    public bool rx_frame_received;

    public data_modems_rx_handler_t? rx_handler;
    public data_modems_rx_fillin_handler_t? rx_fillin_handler;
    public object? rx_user_data;

    public data_modems_tx_handler_t? tx_handler;
    public object? tx_user_data;

    public int audio_rx_log;
    public int audio_tx_log;
    public SpanLogState logging = new();
}

public static class data_modems {
    public const int DATA_MODEM_NONE = -1;
    public const int DATA_MODEM_FLUSH = 0;
    public const int DATA_MODEM_SILENCE = 1;
    public const int DATA_MODEM_CED_TONE = 2;
    public const int DATA_MODEM_CNG_TONE = 3;
    public const int DATA_MODEM_V8 = 4;
    public const int DATA_MODEM_BELL103 = 5;
    public const int DATA_MODEM_BELL202 = 6;
    public const int DATA_MODEM_V21 = 7;
    public const int DATA_MODEM_V23 = 8;
    public const int DATA_MODEM_V22BIS = 9;
    public const int DATA_MODEM_V32BIS = 10;
    public const int DATA_MODEM_V34 = 11;

    public static string data_modems_modulation_to_str(int modulation_scheme) {
        switch (modulation_scheme) {
            case DATA_MODEM_NONE:
                return "None";
            case DATA_MODEM_FLUSH:
                return "Flush";
            case DATA_MODEM_SILENCE:
                return "Silence";
            case DATA_MODEM_CED_TONE:
                return "CED";
            case DATA_MODEM_CNG_TONE:
                return "CNG";
            case DATA_MODEM_V8:
                return "V.8";
            case DATA_MODEM_BELL103:
                return "B103 duplex";
            case DATA_MODEM_BELL202:
                return "B202 duplex";
            case DATA_MODEM_V21:
                return "V.21 duplex";
            case DATA_MODEM_V23:
                return "V.23 duplex";
            case DATA_MODEM_V22BIS:
                return "V.22/V.22bis duplex";
            case DATA_MODEM_V32BIS:
                return "V.32/V.32bis duplex";
            case DATA_MODEM_V34:
                return "V.34 duplex";
        }

        return "???";
    }

    public static void data_modems_set_tep_mode(data_modems_state_t s, int use_tep) {
        s.use_tep = use_tep != 0;
    }

    public static SpanLogState data_modems_get_logging_state(data_modems_state_t s) {
        return s.logging;
    }

    public static void data_modems_call_event(data_modems_state_t s, int @event) {
        AtInterpreterState at_state = s.at_state!;

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "Call event %s (%d) received\n",
            AtInterpreterApi.at_call_state_to_str(@event),
            @event);

        AtInterpreterApi.at_call_event(at_state, @event);
    }

    private static int async_get_byte(object? user_data) {
        data_modems_state_t s = (data_modems_state_t) user_data!;
        byte[] msg = new byte[1];

        s.get_msg!(s.user_data, msg, 1);
        return msg[0];
    }

    private static void async_put_byte(object? user_data, int @byte) {
        data_modems_state_t s = (data_modems_state_t) user_data!;

        if (@byte < 0) {
            s.put_msg!(s.user_data, null, @byte);
            return;
        }

        byte[] msg = { unchecked((byte)@byte) };
        s.put_msg!(s.user_data, msg, 1);
    }

    private static void tone_callback(
        object? user_data,
        ModemConnectTone tone,
        int level,
        int delay) {
        _ = user_data;
        _ = delay;
        Console.WriteLine(
            "{0} declared ({1}dBm0)",
            ModemConnectTones.ToneToString(tone),
            level);
    }

    private static void log_supported_modulations(
        data_modems_state_t s,
        V8Modulation modulation_schemes) {
        string comma = string.Empty;

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "    ");

        uint schemes = (uint)modulation_schemes;
        for (int i = 0; i < 32; i++) {
            uint bit = 1u << i;
            if ((schemes & bit) != 0) {
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW |
                    LoggingApi.SPAN_LOG_SUPPRESS_LABELLING,
                    "%s%s",
                    comma,
                    V8Api.v8_modulation_to_str(unchecked((int)bit)));
                comma = ", ";
            }
        }

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW |
            LoggingApi.SPAN_LOG_SUPPRESS_LABELLING,
            " supported\n");
    }

    private static void v8_handler(object? user_data, V8Parameters result) {
        data_modems_state_t s = (data_modems_state_t) user_data!;

        switch (result.Status) {
            case V8Status.InProgress:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "V.8 negotiation in progress\n");
                return;

            case V8Status.V8Offered:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "V.8 offered by the other party\n");
                break;

            case V8Status.V8Call:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "V.8 call negotiation successful\n");
                break;

            case V8Status.NonV8Call:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "Non-V.8 call negotiation successful\n");
                break;

            case V8Status.Failed:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "V.8 call negotiation failed\n");
                return;

            default:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "Unexpected V.8 status %d\n",
                    (int)result.Status);
                break;
        }

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  Modem connect tone '%s' (%d)\n",
            ModemConnectTones.ToneToString(result.ModemConnectTone),
            (int)result.ModemConnectTone);

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  Call function '%s' (%d)\n",
            V8Api.v8_call_function_to_str((int)result.JmCm.CallFunction),
            (int)result.JmCm.CallFunction);

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  Far end modulations 0x%X\n",
            (uint)result.JmCm.Modulations);

        log_supported_modulations(s, result.JmCm.Modulations);

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  Protocol '%s' (%d)\n",
            V8Api.v8_protocol_to_str((int)result.JmCm.Protocols),
            (int)result.JmCm.Protocols);

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  PSTN access '%s' (%d)\n",
            V8Api.v8_pstn_access_to_str((int)result.JmCm.PstnAccess),
            (int)result.JmCm.PstnAccess);

        LoggingApi.span_log(
            s.logging,
            LoggingApi.SPAN_LOG_FLOW,
            "  PCM modem availability '%s' (%d)\n",
            V8Api.v8_pcm_modem_availability_to_str(
                (int)result.JmCm.PcmModemAvailability),
            (int)result.JmCm.PcmModemAvailability);

        if (result.JmCm.T66 >= 0) {
            LoggingApi.span_log(
                s.logging,
                LoggingApi.SPAN_LOG_FLOW,
                "  T.66 '%s' (%d)\n",
                V8Api.v8_t66_to_str(result.JmCm.T66),
                result.JmCm.T66);
        }

        if (result.JmCm.Nsf >= 0) {
            LoggingApi.span_log(
                s.logging,
                LoggingApi.SPAN_LOG_FLOW,
                "  NSF %d\n",
                result.JmCm.Nsf);
        }

        switch (result.Status) {
            case V8Status.V8Offered:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "  Offered\n");

                result.JmCm.Modulations &=
                    V8Modulation.V21 |
                    V8Modulation.V22 |
                    V8Modulation.V23HalfDuplex |
                    V8Modulation.V23
                    | V8Modulation.V32
                    | V8Modulation.V34
                    ;

                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "  Mutual modulations 0x%X\n",
                    (uint)result.JmCm.Modulations);

                log_supported_modulations(s, result.JmCm.Modulations);
                break;

            case V8Status.V8Call:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "  Call\n");

                if (result.JmCm.CallFunction == V8CallFunction.VSeriesModem) {
                    if (result.JmCm.Protocols == V8Protocol.LapmV42) {
                    }


                    if ((result.JmCm.Modulations & V8Modulation.V34) != 0) {
                        s.queued_baud_rate = 2400;
                        s.queued_bit_rate = 21600;
                        s.queued_modem = DATA_MODEM_V34;
                    } else if ((result.JmCm.Modulations & V8Modulation.V32) != 0) {
                        s.queued_baud_rate = 2400;
                        s.queued_bit_rate = 14400;
                        s.queued_modem = DATA_MODEM_V32BIS;
                    } else if ((result.JmCm.Modulations & V8Modulation.V22) != 0) {
                        s.queued_baud_rate = 600;
                        s.queued_bit_rate = 2400;
                        s.queued_modem = DATA_MODEM_V22BIS;
                    } else if ((result.JmCm.Modulations & V8Modulation.V21) != 0) {
                        s.queued_baud_rate = 300;
                        s.queued_bit_rate = 300;
                        s.queued_modem = DATA_MODEM_V21;
                    } else {
                        s.queued_modem = DATA_MODEM_NONE;
                    }

                    LoggingApi.span_log(
                        s.logging,
                        LoggingApi.SPAN_LOG_FLOW,
                        "  Negotiated modulation '%s' %d\n",
                        data_modems_modulation_to_str(s.queued_modem),
                        s.queued_modem);
                }
                break;

            case V8Status.NonV8Call:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "  Non-V.8 call\n");
                s.queued_modem = DATA_MODEM_V22BIS;
                break;

            default:
                LoggingApi.span_log(
                    s.logging,
                    LoggingApi.SPAN_LOG_FLOW,
                    "  Huh? %d\n",
                    (int)result.Status);
                break;
        }
    }

    public static void data_modems_set_async_mode(
        data_modems_state_t s,
        int data_bits,
        int parity_bits,
        int stop_bits) {

        s.data_bits = unchecked((short)data_bits);
        s.parity = unchecked((short)parity_bits);
        s.stop_bits = unchecked((short)stop_bits);

        s.async_tx = AsyncApi.async_tx_init(
            s.async_tx,
            s.data_bits,
            s.parity,
            s.stop_bits,
            s.use_v14 != 0,
            async_get_byte,
            s);

        s.async_rx = AsyncApi.async_rx_init(
            s.async_rx,
            s.data_bits,
            s.parity,
            s.stop_bits,
            s.use_v14 != 0,
            async_put_byte,
            s);

        switch (s.current_modem) {
            case DATA_MODEM_BELL103:
            case DATA_MODEM_V21:
            case DATA_MODEM_BELL202:
            case DATA_MODEM_V23:
                if (s.modems.fsk.rx is not null) {
                    FskApi.fsk_rx_set_frame_parameters(
                        s.modems.fsk.rx,
                        s.data_bits,
                        s.parity,
                        s.stop_bits);
                }
                break;
        }
    }

    public static void data_modems_set_modem_type(
        data_modems_state_t s,
        int which,
        int baud_rate,
        int bit_rate) {

        FskSpec? fsk_rx_spec;
        FskSpec? fsk_tx_spec;

        switch (which) {
            case DATA_MODEM_SILENCE:
                s.rx_handler = static (_, _) => 0;
                s.rx_fillin_handler = static (_, _) => 0;
                s.rx_user_data = null;
                s.tx_handler = static (user_data, amp) =>
                    SilenceGen.silence_gen(
                        (SilenceGenState)user_data!,
                        amp,
                        amp.Length);
                s.tx_user_data = s.modems.silence_gen;
                s.modems.silence_gen = SilenceGen.silence_gen_init(
                    s.modems.silence_gen,
                    0);
                break;

            case DATA_MODEM_CNG_TONE:
                s.modems.tones.rx?.Dispose();
                s.modems.tones.tx?.Dispose();

                s.modems.tones.rx = ModemConnectTones.ReceiveInit(
                    ModemConnectTone.FaxCng,
                    tone_callback,
                    s);

                s.modems.tones.tx = ModemConnectTones.TransmitInit(
                    ModemConnectTone.FaxCng);

                s.rx_handler = static (user_data, amp) =>
                    ModemConnectTones.Receive(
                        (ModemConnectTonesRxState)user_data!,
                        amp);
                s.rx_fillin_handler = static (_, _) => 0;
                s.rx_user_data = s.modems.tones.rx;

                s.tx_handler = static (user_data, amp) =>
                    ModemConnectTones.Transmit(
                        (ModemConnectTonesTxState)user_data!,
                        amp);
                s.tx_user_data = s.modems.tones.tx;
                break;

            case DATA_MODEM_V8: {
                    V8Parameters v8_parms = new();

                    s.rx_handler = static (user_data, amp) =>
                        ((V8State)user_data!).Receive(amp);
                    s.rx_fillin_handler = static (_, _) => 0;

                    s.tx_handler = static (user_data, amp) =>
                        ((V8State)user_data!).Transmit(amp);

                    if (s.calling_party)
                        v8_parms.ModemConnectTone = ModemConnectTone.None;
                    else
                        v8_parms.ModemConnectTone = ModemConnectTone.AnsamWithPhaseReversals;

                    v8_parms.SendCi = false;
                    v8_parms.V92 = -1;
                    v8_parms.JmCm.CallFunction = V8CallFunction.VSeriesModem;
                    v8_parms.JmCm.Modulations =
                        V8Modulation.V21 |
                        V8Modulation.V22 |
                        V8Modulation.V23HalfDuplex |
                        V8Modulation.V23
                        | V8Modulation.V32
                        | V8Modulation.V34
                        ;
                    v8_parms.JmCm.Protocols = V8Protocol.LapmV42;
                    v8_parms.JmCm.PcmModemAvailability = V8PcmModemAvailability.None;
                    v8_parms.JmCm.PstnAccess = V8PstnAccess.None;
                    v8_parms.JmCm.Nsf = -1;
                    v8_parms.JmCm.T66 = -1;

                    s.modems.v8 = V8Api.v8_init(
                        s.modems.v8,
                        s.calling_party,
                        v8_parms,
                        v8_handler,
                        s);

                    s.modems.v8.LogHandler = message =>
                        LoggingApi.span_log(
                            s.logging,
                            LoggingApi.SPAN_LOG_FLOW,
                            "%s\n",
                            message);

                    s.rx_user_data = s.modems.v8;
                    s.tx_user_data = s.modems.v8;
                    break;
                }

            case DATA_MODEM_BELL103:
                if (s.calling_party) {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL103CH2];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL103CH1];
                } else {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL103CH1];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL103CH2];
                }
                s.modems.fsk.rx = FskApi.fsk_rx_init(
                    s.modems.fsk.rx,
                    fsk_rx_spec!,
                    FskApi.FSK_FRAME_MODE_FRAMED,
                    s.put_bit!.Invoke,
                    s.put_user_data);
                FskApi.fsk_rx_set_frame_parameters(
                    s.modems.fsk.rx,
                    s.data_bits,
                    s.parity,
                    s.stop_bits);
                s.modems.fsk.tx = FskApi.fsk_tx_init(
                    s.modems.fsk.tx,
                    fsk_tx_spec!,
                    s.get_bit!.Invoke,
                    s.get_user_data);
                s.rx_handler = static (user_data, amp) =>
                    FskApi.fsk_rx((FskRxState) user_data!, amp);
                s.rx_fillin_handler = static (user_data, len) =>
                    FskApi.fsk_rx_fillin((FskRxState) user_data!, len);
                s.rx_user_data = s.modems.fsk.rx;
                s.tx_handler = static (user_data, amp) =>
                    FskApi.fsk_tx((FskTxState) user_data!, amp);
                s.tx_user_data = s.modems.fsk.tx;
                break;

            case DATA_MODEM_V21:
                if (s.calling_party) {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V21CH2];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V21CH1];
                } else {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V21CH1];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V21CH2];
                }
                s.modems.fsk.rx = FskApi.fsk_rx_init(
                    s.modems.fsk.rx,
                    fsk_rx_spec!,
                    FskApi.FSK_FRAME_MODE_FRAMED,
                    s.put_bit!.Invoke,
                    s.put_user_data);
                FskApi.fsk_rx_set_frame_parameters(
                    s.modems.fsk.rx,
                    s.data_bits,
                    s.parity,
                    s.stop_bits);
                s.modems.fsk.tx = FskApi.fsk_tx_init(
                    s.modems.fsk.tx,
                    fsk_tx_spec!,
                    s.get_bit!.Invoke,
                    s.get_user_data);
                s.rx_handler = static (user_data, amp) =>
                    FskApi.fsk_rx((FskRxState) user_data!, amp);
                s.rx_fillin_handler = static (user_data, len) =>
                    FskApi.fsk_rx_fillin((FskRxState) user_data!, len);
                s.rx_user_data = s.modems.fsk.rx;
                s.tx_handler = static (user_data, amp) =>
                    FskApi.fsk_tx((FskTxState) user_data!, amp);
                s.tx_user_data = s.modems.fsk.tx;
                break;

            case DATA_MODEM_BELL202:
                fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL202];
                fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_BELL202];
                s.modems.fsk.rx = FskApi.fsk_rx_init(
                    s.modems.fsk.rx,
                    fsk_rx_spec!,
                    FskApi.FSK_FRAME_MODE_FRAMED,
                    s.put_bit!.Invoke,
                    s.put_user_data);
                FskApi.fsk_rx_set_frame_parameters(
                    s.modems.fsk.rx,
                    s.data_bits,
                    s.parity,
                    s.stop_bits);
                s.modems.fsk.tx = FskApi.fsk_tx_init(
                    s.modems.fsk.tx,
                    fsk_tx_spec!,
                    s.get_bit!.Invoke,
                    s.get_user_data);
                s.rx_handler = static (user_data, amp) =>
                    FskApi.fsk_rx((FskRxState) user_data!, amp);
                s.rx_fillin_handler = static (user_data, len) =>
                    FskApi.fsk_rx_fillin((FskRxState) user_data!, len);
                s.rx_user_data = s.modems.fsk.rx;
                s.tx_handler = static (user_data, amp) =>
                    FskApi.fsk_tx((FskTxState) user_data!, amp);
                s.tx_user_data = s.modems.fsk.tx;
                break;

            case DATA_MODEM_V23:
                if (s.calling_party) {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V23CH2];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V23CH1];
                } else {
                    fsk_rx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V23CH1];
                    fsk_tx_spec = FskApi.preset_fsk_specs[FskApi.FSK_V23CH2];
                }
                s.modems.fsk.rx = FskApi.fsk_rx_init(
                    s.modems.fsk.rx,
                    fsk_rx_spec!,
                    FskApi.FSK_FRAME_MODE_FRAMED,
                    s.put_bit!.Invoke,
                    s.put_user_data);
                FskApi.fsk_rx_set_frame_parameters(
                    s.modems.fsk.rx,
                    s.data_bits,
                    s.parity,
                    s.stop_bits);
                s.modems.fsk.tx = FskApi.fsk_tx_init(
                    s.modems.fsk.tx,
                    fsk_tx_spec!,
                    s.get_bit!.Invoke,
                    s.get_user_data);
                s.rx_handler = static (user_data, amp) =>
                    FskApi.fsk_rx((FskRxState) user_data!, amp);
                s.rx_fillin_handler = static (user_data, len) =>
                    FskApi.fsk_rx_fillin((FskRxState) user_data!, len);
                s.rx_user_data = s.modems.fsk.rx;
                s.tx_handler = static (user_data, amp) =>
                    FskApi.fsk_tx((FskTxState) user_data!, amp);
                s.tx_user_data = s.modems.fsk.tx;
                break;

            case DATA_MODEM_V22BIS:
                s.modems.v22bis = V22BisApi.v22bis_init(
                    s.modems.v22bis,
                    bit_rate,
                    0,
                    s.calling_party,
                    s.get_bit!.Invoke,
                    s.get_user_data,
                    s.put_bit!.Invoke,
                    s.put_user_data);

                if (s.modems.v22bis is not null) {
                    s.rx_handler = static (user_data, amp) =>
                        ((V22BisState)user_data!).Receive(amp);
                    s.rx_fillin_handler = static (user_data, len) =>
                        ((V22BisState)user_data!).ReceiveFillIn(len);
                    s.rx_user_data = s.modems.v22bis;

                    s.tx_handler = static (user_data, amp) =>
                        ((V22BisState)user_data!).Transmit(amp);
                    s.tx_user_data = s.modems.v22bis;

                    s.modems.v22bis.Logging.Handler = message =>
                        LoggingApi.span_log(
                            s.logging,
                            LoggingApi.SPAN_LOG_FLOW,
                            "%s\n",
                            message);
                }
                break;

            case DATA_MODEM_V32BIS:
                s.modems.v32bis = V32Bis.v32bis_init(
                    s.modems.v32bis,
                    bit_rate,
                    s.calling_party,
                    s.get_bit!.Invoke,
                    s.get_user_data,
                    s.put_bit!.Invoke,
                    s.put_user_data);

                s.rx_handler = static (user_data, amp) =>
                    V32Bis.v32bis_rx((v32bis_state_t)user_data!, amp);
                s.rx_fillin_handler = static (user_data, len) =>
                    V32Bis.v32bis_rx_fillin((v32bis_state_t)user_data!, len);
                s.rx_user_data = s.modems.v32bis;

                s.tx_handler = static (user_data, amp) =>
                    V32Bis.v32bis_tx((v32bis_state_t)user_data!, amp);
                s.tx_user_data = s.modems.v32bis;

                SpanLogState v32bis_logging =
                    V32Bis.v32bis_get_logging_state(s.modems.v32bis!);
                int v32bis_logging_level =
                    LoggingApi.span_log_get_level(s.logging);
                LoggingApi.span_log_set_level(
                    v32bis_logging,
                    v32bis_logging_level);
                LoggingApi.span_log_set_tag(
                    v32bis_logging,
                    "V.32bis");
                break;

            case DATA_MODEM_V34: {

                    v34_state_t? v34_state = v34.v34_init(
                        s.modems.v34,
                        baud_rate,
                        bit_rate,
                        s.calling_party,
                        true,
                        s.get_bit!.Invoke,
                        s.get_user_data,
                        s.put_bit!.Invoke,
                        s.put_user_data);

                    s.modems.v34 = v34_state;

                    s.rx_handler = static (user_data, amp) =>
                        v34.v34_rx((v34_state_t)user_data!, amp, amp.Length);
                    s.rx_fillin_handler = static (user_data, len) =>
                        v34.v34_rx_fillin((v34_state_t)user_data!, len);
                    s.rx_user_data = v34_state;

                    s.tx_handler = static (user_data, amp) =>
                        v34.v34_tx((v34_state_t)user_data!, amp, amp.Length);
                    s.tx_user_data = v34_state;

                    SpanLogState v34_logging =
                        v34.v34_get_logging_state(v34_state!);
                    int v34_logging_level =
                        LoggingApi.span_log_get_level(s.logging);
                    LoggingApi.span_log_set_level(
                        v34_logging,
                        v34_logging_level);
                    LoggingApi.span_log_set_tag(
                        v34_logging,
                        "V.34");
                    break;
                }
        }

        s.current_modem = which;
    }

    public static int data_modems_rx(
        data_modems_state_t s,
        short[] amp,
        int len) {

        if (s.rx_handler is null)
            return len;

        int res = s.rx_handler(
            s.rx_user_data,
            amp.AsSpan(0, len));

        if (s.current_modem != s.queued_modem) {
            data_modems_set_modem_type(
                s,
                s.queued_modem,
                s.queued_baud_rate,
                s.queued_bit_rate);
        }

        return res;
    }

    public static int data_modems_rx_fillin(data_modems_state_t s, int len) {

        if (s.rx_fillin_handler is null)
            return len;

        return s.rx_fillin_handler(s.rx_user_data, len);
    }

    public static int data_modems_tx(
        data_modems_state_t s,
        short[] amp,
        int max_len) {

        int len = 0;
        while (len < max_len) {
            if (s.tx_handler is null)
                break;

            int produced = s.tx_handler(
                s.tx_user_data,
                amp.AsSpan(len, max_len - len));

            if (produced <= 0)
                break;

            if (produced > max_len - len)
                produced = max_len - len;

            len += produced;
        }

        return len;
    }

    private static int data_modems_control_handler(
        object? user_data,
        int op,
        string? num) {
        data_modems_state_t s = (data_modems_state_t) user_data!;

        switch ((AtModemControlOperation)op) {
            case AtModemControlOperation.Call:
                s.call_samples = 0;
                break;

            case AtModemControlOperation.Answer:
                s.call_samples = 0;
                break;

            case AtModemControlOperation.OnHook:
                if (s.at_state!.ReceiveSignalPresent)
                    s.at_state.rx_data_bytes = 0;
                break;

            case AtModemControlOperation.Restart:
                return 0;

            case AtModemControlOperation.DteTimeout:
                return 0;
        }

        return s.modem_control_handler!(
            s,
            s.modem_control_user_data,
            op,
            num);
    }

    public static void data_modems_set_at_tx_handler(
        data_modems_state_t s,
        AtTransmitHandler at_tx_handler,
        object? at_tx_user_data) {

        AtInterpreterApi.at_set_at_tx_handler(
            s.at_state!,
            at_tx_handler,
            at_tx_user_data);
    }

    public static int data_modems_restart(data_modems_state_t s) {
        return 0;
    }

    public static data_modems_state_t? data_modems_init(
        data_modems_state_t? s,
        bool calling_party,
        AtTransmitHandler at_tx_handler,
        object? at_tx_user_data,
        data_modems_control_handler_t modem_control_handler,
        object? modem_control_user_data,
        span_put_msg_func_t put_msg,
        span_get_msg_func_t get_msg,
        object? user_data) {
        if (at_tx_handler is null || modem_control_handler is null)
            return null;

        if (s is null)
            s = new data_modems_state_t();
        else {
            s.calling_party = false;
            s.use_tep = false;
            s.transmit_on_idle = false;
            s.data_bits = 0;
            s.parity = 0;
            s.stop_bits = 0;
            s.at_state = null;
            s.modem_control_handler = null;
            s.modem_control_user_data = null;
            s.get_bit = null;
            s.get_user_data = null;
            s.put_bit = null;
            s.put_user_data = null;
            s.user_data = null;
            s.put_msg = null;
            s.get_msg = null;
            s.v42 = null;
            s.v42bis = null;
            s.use_v14 = 0;
            s.async_tx = null;
            s.async_rx = null;
            s.call_samples = 0;
            s.modems = new data_modems_modem_state_t();
            s.dc_restore = new DcRestoreState();
            s.current_modem = 0;
            s.queued_modem = 0;
            s.queued_baud_rate = 0;
            s.queued_bit_rate = 0;
            s.current_rx_type = 0;
            s.current_tx_type = 0;
            s.rx_signal_present = false;
            s.rx_trained = false;
            s.rx_frame_received = false;
            s.rx_handler = null;
            s.rx_fillin_handler = null;
            s.rx_user_data = null;
            s.tx_handler = null;
            s.tx_user_data = null;
            s.audio_rx_log = 0;
            s.audio_tx_log = 0;
            s.logging = new SpanLogState();
        }

        s.logging = LoggingApi.span_log_init(
            s.logging,
            LoggingApi.SPAN_LOG_NONE,
            null);

        LoggingApi.span_log_set_protocol(s.logging, "Modem");

        DcRestore.dc_restore_init(s.dc_restore);

        s.modem_control_handler = modem_control_handler;
        s.modem_control_user_data = modem_control_user_data;

        s.put_msg = put_msg;
        s.get_msg = get_msg;
        s.user_data = user_data;

        s.v42bis = v42bis.v42bis_init(
            s.v42bis,
            3,
            512,
            6,
            null,
            s,
            512,
            put_msg,
            s,
            512);

        s.v42 = v42.v42_init(
            s.v42,
            true,
            true,
            null,
            v42bis.v42bis_decompress,
            s.v42bis);

        data_modems_set_async_mode(
            s,
            8,
            (int)AsyncParity.None,
            1);

        s.at_state = AtInterpreterApi.at_init(
            s.at_state,
            at_tx_handler,
            at_tx_user_data,
            data_modems_control_handler,
            s);

        s.get_bit = AsyncApi.async_tx_get_bit;
        s.get_user_data = s.async_tx;
        s.put_bit = AsyncApi.async_rx_put_bit;
        s.put_user_data = s.async_rx;

        s.calling_party = calling_party;

        data_modems_set_modem_type(s, DATA_MODEM_V8, 0, 0);
        s.queued_modem = s.current_modem;

        s.rx_signal_present = false;
        return s;
    }

    public static int data_modems_release(data_modems_state_t s) {
        return 0;
    }

    public static int data_modems_free(data_modems_state_t? s) {
        return 0;
    }

}
