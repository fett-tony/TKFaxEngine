/*
 * TKFaxEngineFX - managed C# port
 *
 * Combined C# conversion of t30_api.c and t30_api.h.
 */

using System.Text;

using TKFaxEngine.FaxImage;

namespace TKFaxEngine.Daten.T30;

public enum T33FieldType {
    None = 0,
    Sst = 1,
    Ext = 2
}

public static class T30Api {
    private const int SupportedOutputCompressionMask =
        (int)(t4_image_compression_t.T4_COMPRESSION_T4_1D
            | t4_image_compression_t.T4_COMPRESSION_T4_2D
            | t4_image_compression_t.T4_COMPRESSION_T6
            | t4_image_compression_t.T4_COMPRESSION_T85
            | t4_image_compression_t.T4_COMPRESSION_T85_L0
            | t4_image_compression_t.T4_COMPRESSION_T88
            | t4_image_compression_t.T4_COMPRESSION_T42_T81
            | t4_image_compression_t.T4_COMPRESSION_SYCC_T81
            | t4_image_compression_t.T4_COMPRESSION_T43
            | t4_image_compression_t.T4_COMPRESSION_T45
            | t4_image_compression_t.T4_COMPRESSION_UNCOMPRESSED
            | t4_image_compression_t.T4_COMPRESSION_JPEG);

    private const int SupportedCompressionMask =
        (int)(t4_image_compression_t.T4_COMPRESSION_T4_1D
            | t4_image_compression_t.T4_COMPRESSION_T4_2D
            | t4_image_compression_t.T4_COMPRESSION_T6
            | t4_image_compression_t.T4_COMPRESSION_T85
            | t4_image_compression_t.T4_COMPRESSION_T85_L0
            | t4_image_compression_t.T4_COMPRESSION_T88
            | t4_image_compression_t.T4_COMPRESSION_T42_T81
            | t4_image_compression_t.T4_COMPRESSION_SYCC_T81
            | t4_image_compression_t.T4_COMPRESSION_T43
            | t4_image_compression_t.T4_COMPRESSION_T45
            | t4_image_compression_t.T4_COMPRESSION_GRAYSCALE
            | t4_image_compression_t.T4_COMPRESSION_COLOUR
            | t4_image_compression_t.T4_COMPRESSION_12BIT
            | t4_image_compression_t.T4_COMPRESSION_COLOUR_TO_GRAY
            | t4_image_compression_t.T4_COMPRESSION_GRAY_TO_BILEVEL
            | t4_image_compression_t.T4_COMPRESSION_COLOUR_TO_BILEVEL
            | t4_image_compression_t.T4_COMPRESSION_RESCALING);

    private const int SupportedBilevelResolutionMask =
        (int)(t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD
            | t4_image_resolution_t.T4_RESOLUTION_R8_FINE
            | t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE
            | t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE
            | t4_image_resolution_t.T4_RESOLUTION_200_100
            | t4_image_resolution_t.T4_RESOLUTION_200_200
            | t4_image_resolution_t.T4_RESOLUTION_200_400
            | t4_image_resolution_t.T4_RESOLUTION_300_300
            | t4_image_resolution_t.T4_RESOLUTION_300_600
            | t4_image_resolution_t.T4_RESOLUTION_400_400
            | t4_image_resolution_t.T4_RESOLUTION_400_800
            | t4_image_resolution_t.T4_RESOLUTION_600_600
            | t4_image_resolution_t.T4_RESOLUTION_600_1200
            | t4_image_resolution_t.T4_RESOLUTION_1200_1200);

    private const int SupportedColourResolutionMask =
        (int)(t4_image_resolution_t.T4_RESOLUTION_100_100
            | t4_image_resolution_t.T4_RESOLUTION_200_200
            | t4_image_resolution_t.T4_RESOLUTION_300_300
            | t4_image_resolution_t.T4_RESOLUTION_400_400
            | t4_image_resolution_t.T4_RESOLUTION_600_600
            | t4_image_resolution_t.T4_RESOLUTION_1200_1200);
    public static int t33_sub_address_extract_field(
        Span<byte> destination,
        ReadOnlySpan<byte> t33,
        int fieldNumber) {
        if (destination.Length < T30State.MaxIdentLength + 1)
            throw new ArgumentException("Destination must provide at least 21 bytes.", nameof(destination));
        destination.Clear();
        if (fieldNumber < 0) return (int)T33FieldType.None;

        int index = 0;
        int currentField = 0;
        while (index < t33.Length && t33[index] != 0) {
            if (currentField++ == fieldNumber) {
                int output = 0;
                byte first = t33[index++];
                T33FieldType type;
                if (first == (byte)'#') {
                    type = T33FieldType.Sst;
                } else {
                    type = T33FieldType.Ext;
                    destination[output++] = first;
                }

                while (index < t33.Length && t33[index] != 0 && t33[index] != (byte)'#') {
                    if (output >= T30State.MaxIdentLength) return -1;
                    destination[output++] = t33[index++];
                }
                destination[output] = 0;
                return (int)type;
            }

            index++;
            while (index < t33.Length && t33[index] != 0) {
                if (t33[index++] == (byte)'#') break;
            }
        }
        return (int)T33FieldType.None;
    }

    public static string t33_sub_address_add_field(string? t33, string field, T33FieldType type) {
        ArgumentNullException.ThrowIfNull(field);
        StringBuilder result = new(t33 ?? string.Empty);
        if (result.Length != 0) result.Append('#');
        if (type == T33FieldType.Sst) result.Append('#');
        result.Append(field);
        return result.ToString();
    }

    public static void t33_sub_address_add_field(Span<byte> t33, ReadOnlySpan<byte> field, int type) {
        int end = t33.IndexOf((byte)0);
        if (end < 0) throw new ArgumentException("T.33 buffer is not NUL terminated.", nameof(t33));
        int fieldLength = field.IndexOf((byte)0);
        if (fieldLength < 0) fieldLength = field.Length;
        int separatorBytes = end == 0 ? 0 : 1;
        if (type == (int)T33FieldType.Sst) separatorBytes++;
        int required = separatorBytes + fieldLength + 1;
        if (end + required > t33.Length) throw new ArgumentException("T.33 destination buffer is too small.", nameof(t33));
        if (end != 0) t33[end++] = (byte)'#';
        if (type == (int)T33FieldType.Sst) t33[end++] = (byte)'#';
        field[..fieldLength].CopyTo(t33[end..]);
        t33[end + fieldLength] = 0;
    }

    public static int t30_set_tx_ident(T30State state, string? value) {
        int result = SetIdent(value, v => state.TxInfo.Ident = v);
        if (result == 0) t4_tx.t4_tx_set_local_ident(state.T4Tx, value);
        return result;
    }
    public static string? t30_get_tx_ident(T30State state) => NullIfEmpty(state.TxInfo.Ident);
    public static string? t30_get_rx_ident(T30State state) => NullIfEmpty(state.RxInfo.Ident);

    public static int t30_set_tx_sub_address(T30State state, string? value) => SetIdent(value, v => state.TxInfo.SubAddress = v);
    public static string? t30_get_tx_sub_address(T30State state) => NullIfEmpty(state.TxInfo.SubAddress);
    public static string? t30_get_rx_sub_address(T30State state) => NullIfEmpty(state.RxInfo.SubAddress);

    public static int t30_set_tx_selective_polling_address(T30State state, string? value) => SetIdent(value, v => state.TxInfo.SelectivePollingAddress = v);
    public static string? t30_get_tx_selective_polling_address(T30State state) => NullIfEmpty(state.TxInfo.SelectivePollingAddress);
    public static string? t30_get_rx_selective_polling_address(T30State state) => NullIfEmpty(state.RxInfo.SelectivePollingAddress);

    public static int t30_set_tx_polled_sub_address(T30State state, string? value) => SetIdent(value, v => state.TxInfo.PolledSubAddress = v);
    public static string? t30_get_tx_polled_sub_address(T30State state) => NullIfEmpty(state.TxInfo.PolledSubAddress);
    public static string? t30_get_rx_polled_sub_address(T30State state) => NullIfEmpty(state.RxInfo.PolledSubAddress);

    public static int t30_set_tx_sender_ident(T30State state, string? value) => SetIdent(value, v => state.TxInfo.SenderIdent = v);
    public static string? t30_get_tx_sender_ident(T30State state) => NullIfEmpty(state.TxInfo.SenderIdent);
    public static string? t30_get_rx_sender_ident(T30State state) => NullIfEmpty(state.RxInfo.SenderIdent);

    public static int t30_set_tx_password(T30State state, string? value) => SetIdent(value, v => state.TxInfo.Password = v);
    public static string? t30_get_tx_password(T30State state) => NullIfEmpty(state.TxInfo.Password);
    public static string? t30_get_rx_password(T30State state) => NullIfEmpty(state.RxInfo.Password);

    public static int t30_set_tx_nsf(T30State state, ReadOnlySpan<byte> value) { state.TxInfo.Nsf = value.ToArray(); return 0; }
    public static ReadOnlyMemory<byte> t30_get_tx_nsf(T30State state) => state.TxInfo.Nsf;
    public static ReadOnlyMemory<byte> t30_get_rx_nsf(T30State state) => state.RxInfo.Nsf;
    public static int t30_set_tx_nsc(T30State state, ReadOnlySpan<byte> value) { state.TxInfo.Nsc = value.ToArray(); return 0; }
    public static ReadOnlyMemory<byte> t30_get_tx_nsc(T30State state) => state.TxInfo.Nsc;
    public static ReadOnlyMemory<byte> t30_get_rx_nsc(T30State state) => state.RxInfo.Nsc;
    public static int t30_set_tx_nss(T30State state, ReadOnlySpan<byte> value) { state.TxInfo.Nss = value.ToArray(); return 0; }
    public static ReadOnlyMemory<byte> t30_get_tx_nss(T30State state) => state.TxInfo.Nss;
    public static ReadOnlyMemory<byte> t30_get_rx_nss(T30State state) => state.RxInfo.Nss;

    public static int t30_set_tx_tsa(T30State state, int type, string? address, int length) {
        state.TxInfo.Tsa = null;
        state.TxInfo.TsaLength = 0;
        if (address is null || length == 0)
            return 0;

        state.TxInfo.TsaType = type;
        if (length < 0) {
            int terminator = address.IndexOf('\0');
            length = terminator >= 0 ? terminator : address.Length;
        }
        if (length > address.Length)
            return -1;

        state.TxInfo.Tsa = address[..length];
        state.TxInfo.TsaLength = length;
        return 0;
    }

    public static int t30_get_tx_tsa(T30State state, out int type, out string? address) => GetAddress(state.TxInfo.TsaType, state.TxInfo.Tsa, state.TxInfo.TsaLength, out type, out address);
    public static int t30_get_rx_tsa(T30State state, out int type, out string? address) => GetAddress(state.RxInfo.TsaType, state.RxInfo.Tsa, state.RxInfo.TsaLength, out type, out address);

    public static int t30_set_tx_ira(T30State state, int type, string? address, int length) {
        state.TxInfo.Ira = null;
        if (address is null)
            return 0;

        int terminator = address.IndexOf('\0');
        state.TxInfo.Ira = terminator >= 0 ? address[..terminator] : address;
        return 0;
    }

    public static int t30_get_tx_ira(T30State state, out int type, out string? address) => GetAddress(state.TxInfo.IraType, state.TxInfo.Ira, state.TxInfo.IraLength, out type, out address);
    public static int t30_get_rx_ira(T30State state, out int type, out string? address) => GetAddress(state.RxInfo.IraType, state.RxInfo.Ira, state.RxInfo.IraLength, out type, out address);

    public static int t30_set_tx_cia(T30State state, int type, string? address, int length) {
        state.TxInfo.Cia = null;
        if (address is null)
            return 0;

        int terminator = address.IndexOf('\0');
        state.TxInfo.Cia = terminator >= 0 ? address[..terminator] : address;
        return 0;
    }

    public static int t30_get_tx_cia(T30State state, out int type, out string? address) => GetAddress(state.TxInfo.CiaType, state.TxInfo.Cia, state.TxInfo.CiaLength, out type, out address);
    public static int t30_get_rx_cia(T30State state, out int type, out string? address) => GetAddress(state.RxInfo.CiaType, state.RxInfo.Cia, state.RxInfo.CiaLength, out type, out address);

    public static int t30_set_tx_isp(T30State state, int type, string? address, int length) {
        state.TxInfo.Isp = null;
        if (address is null)
            return 0;

        int terminator = address.IndexOf('\0');
        state.TxInfo.Isp = terminator >= 0 ? address[..terminator] : address;
        return 0;
    }

    public static int t30_get_tx_isp(T30State state, out int type, out string? address) => GetAddress(state.TxInfo.IspType, state.TxInfo.Isp, state.TxInfo.IspLength, out type, out address);
    public static int t30_get_rx_isp(T30State state, out int type, out string? address) => GetAddress(state.RxInfo.IspType, state.RxInfo.Isp, state.RxInfo.IspLength, out type, out address);

    public static int t30_set_tx_csa(T30State state, int type, string? address, int length) {
        state.TxInfo.Csa = null;
        if (address is null)
            return 0;

        int terminator = address.IndexOf('\0');
        state.TxInfo.Csa = terminator >= 0 ? address[..terminator] : address;
        return 0;
    }

    public static int t30_get_tx_csa(T30State state, out int type, out string? address) => GetAddress(state.TxInfo.CsaType, state.TxInfo.Csa, state.TxInfo.CsaLength, out type, out address);
    public static int t30_get_rx_csa(T30State state, out int type, out string? address) => GetAddress(state.RxInfo.CsaType, state.RxInfo.Csa, state.RxInfo.CsaLength, out type, out address);

    public static int t30_set_tx_page_header_overlays_image(T30State state, bool overlays) {
        state.HeaderOverlaysImage = overlays;
        t4_tx.t4_tx_set_header_overlays_image(state.T4Tx, overlays);
        return 0;
    }

    public static int t30_set_tx_page_header_info(T30State state, string? info) {
        if (info is not null && info.Length > T30State.MaxPageHeaderInfoLength) return -1;
        state.HeaderInfo = info;
        t4_tx.t4_tx_set_header_info(state.T4Tx, info);
        return 0;
    }

    public static string? t30_get_tx_page_header_info(T30State state) => state.HeaderInfo;

    public static int t30_set_tx_page_header_tz(T30State state, string? timezone) {
        try {
            if (!string.IsNullOrWhiteSpace(timezone)) _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            state.HeaderTimezone = timezone;
            if (!string.IsNullOrEmpty(timezone)) {
                try { t4_tx.t4_tx_set_header_tz(state.T4Tx, TimeZoneInfo.FindSystemTimeZoneById(timezone)); }
                catch (TimeZoneNotFoundException) { return -1; }
                catch (InvalidTimeZoneException) { return -1; }
            }
            return 0;
        } catch (TimeZoneNotFoundException) { return -1; } catch (InvalidTimeZoneException) { return -1; }
    }

    public static string? t30_get_rx_country(T30State state) => state.Country;
    public static string? t30_get_rx_vendor(T30State state) => state.Vendor;
    public static string? t30_get_rx_model(T30State state) => state.Model;

    public static void t30_set_rx_file(T30State state, string? file, int stopPage) { state.RxFile = file; state.RxStopPage = stopPage; }
    public static void t30_set_tx_file(T30State state, string? file, int startPage, int stopPage) { state.TxFile = file; state.TxStartPage = startPage; state.TxStopPage = stopPage; }
    public static void t30_set_iaf_mode(T30State state, int iaf) => state.IafMode = (T30IafMode)iaf;

    public static int t30_set_ecm_capability(T30State state, bool enabled) {
        state.EcmAllowed = enabled;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static void t30_set_retransmit_capable(T30State state, bool enabled) => state.RetransmitCapable = enabled;
    public static void t30_set_max_command_tries(T30State state, int tries) => state.MaxCommandTries = tries;
    public static void t30_set_max_response_tries(T30State state, int tries) => state.MaxResponseTries = tries;
    public static void t30_set_keep_bad_quality_pages(T30State state, bool keep) => state.KeepBadPages = keep;

    public static int t30_set_supported_output_compressions(T30State state, int supportedCompressions) {
        supportedCompressions &= SupportedOutputCompressionMask;
        state.SupportedOutputCompressions = supportedCompressions;
        return 0;
    }

    public static int t30_set_minimum_scan_line_time(T30State state, int milliseconds) {
        state.LocalMinimumScanTimeCode = milliseconds switch {
            0 => 7,
            <= 5 => 1,
            <= 10 => 2,
            <= 20 => 0,
            <= 40 => 4,
            _ => byte.MaxValue
        };
        if (state.LocalMinimumScanTimeCode == byte.MaxValue) return -1;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_modems(T30State state, int supportedModems) {
        state.SupportedModems = (T30SupportedModems)supportedModems;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_compressions(T30State state, int supportedCompressions) {
        supportedCompressions &= SupportedCompressionMask;
        state.SupportedCompressions = supportedCompressions;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_bilevel_resolutions(T30State state, int supportedResolutions) {
        supportedResolutions &= SupportedBilevelResolutionMask;
        supportedResolutions |= state.SupportedColourResolutions & ~(int)t4_image_resolution_t.T4_RESOLUTION_100_100;
        state.SupportedBilevelResolutions = supportedResolutions;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_colour_resolutions(T30State state, int supportedResolutions) {
        supportedResolutions &= SupportedColourResolutionMask;
        state.SupportedColourResolutions = supportedResolutions;
        state.SupportedBilevelResolutions |= supportedResolutions & ~(int)t4_image_resolution_t.T4_RESOLUTION_100_100;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_image_sizes(T30State state, int supportedImageSizes) {
        supportedImageSizes |= (int)(t4_image_support_t.T4_SUPPORT_WIDTH_215MM | t4_image_support_t.T4_SUPPORT_LENGTH_A4);
        if ((supportedImageSizes & (int)t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED) != 0)
            supportedImageSizes |= (int)t4_image_support_t.T4_SUPPORT_LENGTH_B4;
        if ((supportedImageSizes & (int)t4_image_support_t.T4_SUPPORT_WIDTH_303MM) != 0)
            supportedImageSizes |= (int)t4_image_support_t.T4_SUPPORT_WIDTH_255MM;
        state.SupportedImageSizes = supportedImageSizes;
        T30.t30_build_dis_or_dtc(state);
        return 0;
    }

    public static int t30_set_supported_t30_features(T30State state, int value) { state.SupportedFeatures = (T30SupportedFeatures)value; T30.t30_build_dis_or_dtc(state); return 0; }

    public static void t30_set_status(T30State state, int status) {
        T30Error next = (T30Error)status;
        if (state.CurrentStatus == next) return;
        state.Logging.Flow($"Status changing to '{T30Logging.t30_completion_code_to_str(status)}'.");
        state.CurrentStatus = next;
    }

    public static int t30_set_receiver_not_ready(T30State state, int count) { state.ReceiverNotReadyCount = count; return 0; }

    public static void t30_set_phase_b_handler(T30State state, T30PhaseBHandler? handler, object? userData) { state.PhaseBHandler = handler; state.PhaseBUserData = userData; }
    public static void t30_set_phase_d_handler(T30State state, T30PhaseDHandler? handler, object? userData) { state.PhaseDHandler = handler; state.PhaseDUserData = userData; }
    public static void t30_set_phase_e_handler(T30State state, T30PhaseEHandler? handler, object? userData) { state.PhaseEHandler = handler; state.PhaseEUserData = userData; }
    public static void t30_set_document_handler(T30State state, T30DocumentHandler? handler, object? userData) { state.DocumentHandler = handler; state.DocumentUserData = userData; }
    public static void t30_set_real_time_frame_handler(T30State state, T30RealTimeFrameHandler? handler, object? userData) { state.RealTimeFrameHandler = handler; state.RealTimeFrameUserData = userData; }
    public static void t30_set_document_get_handler(T30State state, T30DocumentGetHandler? handler, object? userData) { state.DocumentGetHandler = handler; state.DocumentGetUserData = userData; }
    public static void t30_set_document_put_handler(T30State state, T30DocumentPutHandler? handler, object? userData) { state.DocumentPutHandler = handler; state.DocumentPutUserData = userData; }
    public static T30Log t30_get_logging_state(T30State state) => state.Logging;

    private static int GetAddress(int sourceType, string? sourceAddress, int sourceLength, out int type, out string? address) {
        type = sourceType;
        address = sourceAddress;
        return sourceLength;
    }

    private static int SetIdent(string? value, Action<string?> setter) {
        if (value is not null && value.Length > T30State.MaxIdentLength) return -1;
        setter(value);
        return 0;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
