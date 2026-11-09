/*
 * TKFaxEngineFX - managed C# port
 *
 * Combined C# conversion of t30_logging.c and t30_logging.h.
 */

namespace TKFaxEngine.Daten.T30;

public enum T30LogLevel {
    Flow,
    Warning,
    ProtocolWarning,
    Debug
}

public sealed class T30Log {
    public Action<T30LogLevel, string>? Sink { get; set; }
    public void Write(T30LogLevel level, string message) => Sink?.Invoke(level, message);
    public void Flow(string message) => Write(T30LogLevel.Flow, message);
    public void Warning(string message) => Write(T30LogLevel.Warning, message);
    public void ProtocolWarning(string message) => Write(T30LogLevel.ProtocolWarning, message);
    public void Debug(string message) => Write(T30LogLevel.Debug, message);
}

public static class T30Logging {
    private static readonly IReadOnlyDictionary<int, string> CompletionDescriptions =
        new Dictionary<int, string> {
            [0] = "OK",
            [1] = "The CED tone exceeded 5s",
            [2] = "Timed out waiting for initial communication",
            [3] = "Timed out waiting for the first message",
            [4] = "Timed out waiting for procedural interrupt",
            [5] = "The HDLC carrier did not stop in a timely manner",
            [6] = "Failed to train with any of the compatible modems",
            [7] = "Operator intervention failed",
            [8] = "Far end is not compatible",
            [9] = "Far end is not able to receive",
            [10] = "Far end is not able to transmit",
            [11] = "Far end cannot receive at the resolution of the image",
            [12] = "Far end cannot receive at the size of image",
            [13] = "Unexpected message received",
            [14] = "Received bad response to DCS or training",
            [15] = "Received a DCN from remote after sending a page",
            [16] = "Invalid ECM response received from receiver",
            [17] = "Received a DCN while waiting for a DIS",
            [18] = "Invalid response after sending a page",
            [19] = "Received other than DIS while waiting for DIS",
            [20] = "Received no response to DCS or TCF",
            [21] = "No response after sending a page",
            [22] = "Timed out waiting for receiver ready (ECM mode)",
            [23] = "Invalid ECM response received from transmitter",
            [24] = "DCS received while waiting for DTC",
            [25] = "Unexpected command after page received",
            [26] = "Carrier lost during fax receive",
            [27] = "Timed out while waiting for EOL (end Of line)",
            [28] = "Timed out while waiting for first line",
            [29] = "Timer T2 expired while waiting for DCN",
            [30] = "Timer T2 expired while waiting for phase D",
            [31] = "Timer T2 expired while waiting for fax page",
            [32] = "Timer T2 expired while waiting for next fax page",
            [33] = "Timer T2 expired while waiting for RR command",
            [34] = "Timer T2 expired while waiting for NSS, DCS or MCF",
            [35] = "Unexpected DCN while waiting for DCS or DIS",
            [36] = "Unexpected DCN while waiting for image data",
            [37] = "Unexpected DCN while waiting for EOM, EOP or MPS",
            [38] = "Unexpected DCN after EOM or MPS sequence",
            [39] = "Unexpected DCN after RR/RNR sequence",
            [40] = "Unexpected DCN after requested retransmission",
            [41] = "TIFF/F file cannot be opened",
            [42] = "TIFF/F page not found",
            [43] = "TIFF/F format is not compatible",
            [44] = "TIFF/F page number tag missing",
            [45] = "Incorrect values for TIFF/F tags",
            [46] = "Bad TIFF/F header - incorrect values in fields",
            [47] = "Cannot allocate memory for more pages",
            [48] = "Disconnected after permitted retries",
            [49] = "The call dropped prematurely",
            [50] = "Poll not accepted",
            [51] = "Ident not accepted",
            [54] = "Polled sub-address not accepted",
            [53] = "Selective polling address not accepted",
            [55] = "Sender identification not accepted",
            [56] = "Password not accepted",
            [52] = "Sub-address not accepted",
            [57] = "Transmitting subscriber internet address not accepted",
            [58] = "Internet routing address not accepted",
            [59] = "Calling subscriber internet address not accepted",
            [60] = "Internet selective polling address not accepted",
            [61] = "Called subscriber internet address not accepted",
        };

    private static readonly IReadOnlyDictionary<byte, string> FrameNames =
        new Dictionary<byte, string> {
            [0x00] = "NULL",
            [0x06] = "FCD",
            [0x10] = "SPI",
            [0x11] = "ISP",
            [0x12] = "CTC",
            [0x13] = "CTC",
            [0x1A] = "CRP",
            [0x1B] = "CRP",
            [0x1C] = "ERR",
            [0x1D] = "ERR",
            [0x1E] = "EOS",
            [0x1F] = "EOS/PSS",
            [0x20] = "NSF",
            [0x21] = "NSC",
            [0x22] = "NSS",
            [0x23] = "NSS",
            [0x24] = "CSA",
            [0x25] = "CSA",
            [0x2C] = "PIN",
            [0x2D] = "PIN",
            [0x2E] = "EOP",
            [0x2F] = "EOP",
            [0x3E] = "PRI-EOP",
            [0x3F] = "PRI-EOP",
            [0x40] = "CSI",
            [0x41] = "CIG",
            [0x42] = "TSI",
            [0x43] = "TSI",
            [0x44] = "FTT",
            [0x45] = "FTT",
            [0x46] = "CCD",
            [0x4A] = "RK",
            [0x4B] = "TK",
            [0x4C] = "RTN",
            [0x4D] = "RTN",
            [0x4E] = "MPS",
            [0x4F] = "MPS",
            [0x53] = "DER",
            [0x5E] = "PRI-MPS",
            [0x5F] = "PRI-MPS",
            [0x61] = "PSA",
            [0x62] = "TSA",
            [0x63] = "TSA",
            [0x6A] = "TR",
            [0x6B] = "TR",
            [0x6C] = "PID",
            [0x6D] = "PID",
            [0x6E] = "RR",
            [0x6F] = "RR",
            [0x80] = "DIS",
            [0x81] = "DTC",
            [0x82] = "DCS",
            [0x83] = "DCS",
            [0x84] = "CFR",
            [0x85] = "CFR",
            [0x86] = "RCP",
            [0x8C] = "MCF",
            [0x8D] = "MCF",
            [0x8E] = "EOM",
            [0x8F] = "EOM",
            [0x93] = "DEC",
            [0x9A] = "DNK",
            [0x9B] = "DNK",
            [0x9E] = "PRI-EOM",
            [0x9F] = "PRI-EOM",
            [0xA0] = "DES",
            [0xA1] = "SEP",
            [0xA2] = "SID",
            [0xA3] = "SID",
            [0xAC] = "PIP",
            [0xAD] = "PIP",
            [0xBC] = "PPR",
            [0xBD] = "PPR",
            [0xBE] = "PPS",
            [0xBF] = "PPS",
            [0xC1] = "PWD",
            [0xC2] = "SUB",
            [0xC3] = "SUB",
            [0xC4] = "CTR",
            [0xC5] = "CTR",
            [0xCA] = "FNV",
            [0xCB] = "FNV",
            [0xCC] = "RTP",
            [0xCD] = "RTP",
            [0xCE] = "EOR",
            [0xCF] = "EOR",
            [0xE1] = "CIA",
            [0xE2] = "IRA",
            [0xE3] = "IRA",
            [0xEA] = "TNR",
            [0xEB] = "TNR",
            [0xEC] = "RNR",
            [0xED] = "RNR",
            [0xFA] = "DCN",
            [0xFB] = "DCN",
            [0xFC] = "FDM",
            [0xFD] = "FDM",
        };

    public static string t30_completion_code_to_str(int result)
        => CompletionDescriptions.TryGetValue(result, out string? text) ? text : "???";

    public static string t30_modem_to_str(int modem)
        => modem switch {
            0 => "None",
            1 => "Pause",
            2 => "CED",
            3 => "CNG",
            4 => "V.21",
            5 => "V.27ter",
            6 => "V.29",
            7 => "V.17",
            8 => "V.34HDX",
            9 => "Done",
            _ => "???"
        };

    public static string t30_frametype(byte frameControlField)
        => FrameNames.TryGetValue(frameControlField, out string? name) ? name : "???";

    public static void t30_log_dis_dtc_dcs(T30Log log, ReadOnlySpan<byte> packet) {
        ArgumentNullException.ThrowIfNull(log);
        if (packet.Length < 3) {
            log.Warning("DIS/DTC/DCS frame is shorter than the HDLC header.");
            return;
        }

        byte frameType = packet[2];
        string name = t30_frametype(frameType);
        log.Flow($"{name}: {packet.Length} octets");
        log.Flow($"  Raw: {Convert.ToHexString(packet)}");
        if (packet.Length <= 3) return;

        bool polling = frameType == T30Frame.Dtc;
        bool dcs = (frameType & 0xFE) == T30Frame.Dcs;
        LogBit(log, packet, 9, polling ? "Ready to transmit a document (polling)" : "Ready to receive a document");
        LogBit(log, packet, 10, "Receiver operation");
        LogBit(log, packet, 11, "Fine resolution");
        LogBit(log, packet, 12, "Two-dimensional coding");
        LogBit(log, packet, 13, "Maximum recording width B4");
        LogBit(log, packet, 14, "Maximum recording width A3");
        LogBit(log, packet, 15, "Maximum recording length B4");
        LogBit(log, packet, 16, "Extension indicator");

        if (packet.Length > 4) {
            int rateCode = packet[4] & 0x3C;
            string rate = rateCode switch {
                0x00 => "V.27ter 2400",
                0x08 => "V.27ter 4800",
                0x04 => "V.29 9600",
                0x0C => "V.29 7200",
                0x20 => "V.17 14400",
                0x28 => "V.17 12000",
                0x24 => "V.17 9600",
                0x2C => "V.17 7200",
                _ => $"unknown 0x{rateCode:X2}"
            };
            log.Flow($"  Data signalling rate: {rate}");
            LogBit(log, packet, 21, "R8 x 7.7 lines/mm");
            LogBit(log, packet, 22, "R8 x 15.4 lines/mm");
            LogBit(log, packet, 23, "R16 x 15.4 lines/mm");
            LogBit(log, packet, 24, "Extension indicator");
        }

        if (packet.Length > 5) {
            int scanCode = (packet[5] >> 4) & 0x07;
            string scan = scanCode switch { 0 => "20 ms", 1 => "5 ms", 2 => "10 ms", 4 => "40 ms", 7 => "0 ms", _ => "reserved" };
            log.Flow($"  Minimum scan-line time: {scan}");
            LogBit(log, packet, 31, "Error correction mode");
            LogBit(log, packet, 32, "Extension indicator");
        }

        if (packet.Length > 6) {
            LogBit(log, packet, 33, "T.6 coding");
            LogBit(log, packet, 35, dcs ? "ECM selected" : "ECM capability");
            LogBit(log, packet, 36, "Frame size preference 64 octets");
            LogBit(log, packet, 39, "Metric/inch resolution capability");
            LogBit(log, packet, 40, "Extension indicator");
        }

        for (int octet = 7; octet < packet.Length; octet++) {
            if ((packet[octet - 1] & 0x80) == 0) break;
            log.Flow($"  Extension octet {octet - 2}: 0x{packet[octet]:X2}");
        }
    }

    public static IReadOnlyList<string> DecodeDisDtcDcs(ReadOnlySpan<byte> packet) {
        List<string> output = new();
        T30Log log = new() { Sink = (_, message) => output.Add(message) };
        t30_log_dis_dtc_dcs(log, packet);
        return output;
    }

    private static void LogBit(T30Log log, ReadOnlySpan<byte> packet, int bitNumber, string description) {
        int zeroBased = bitNumber - 1;
        int octet = 3 + zeroBased / 8;
        int bit = zeroBased % 8;
        if ((uint)octet >= (uint)packet.Length) return;
        bool set = (packet[octet] & (1 << bit)) != 0;
        log.Flow($"  Bit {bitNumber}: {description} = {(set ? "yes" : "no")}");
    }
}

public static partial class t30_logging_helpers {
    internal static void octet_reserved_bit(T30Log log, ReadOnlySpan<byte> message, int bitNumber, int expected) {
        int octetIndex = ((bitNumber - 1) >> 3) + 3;
        if ((uint)octetIndex >= (uint)message.Length)
            return;
        int bitIndex = (bitNumber - 1) & 7;
        int bit = (message[octetIndex] >> bitIndex) & 1;
        if ((bit ^ expected) == 0)
            return;
        char[] display = ".... ....".ToCharArray();
        display[7 - bitIndex + (bitIndex < 4 ? 1 : 0)] = (char)('0' + bit);
        log.Flow($"  {new string(display)}= Unexpected state for reserved bit: {bit}");
    }

    internal static void octet_bit_field(
        T30Log log,
        ReadOnlySpan<byte> message,
        int bitNumber,
        string description,
        string? yeah,
        string? neigh) {
        int octetIndex = ((bitNumber - 1) >> 3) + 3;
        if ((uint)octetIndex >= (uint)message.Length)
            return;
        int bitIndex = (bitNumber - 1) & 7;
        int bit = (message[octetIndex] >> bitIndex) & 1;
        char[] display = ".... ....".ToCharArray();
        display[7 - bitIndex + (bitIndex < 4 ? 1 : 0)] = (char)('0' + bit);
        string tag = bit != 0 ? yeah ?? "Set" : neigh ?? "Not set";
        log.Flow($"  {new string(display)}= {description}: {tag}");
    }

    internal static void octet_field(
        T30Log log,
        ReadOnlySpan<byte> message,
        int start,
        int end,
        string description,
        ReadOnlySpan<(int Value, string Text)> tags) {
        int octetIndex = ((start - 1) >> 3) + 3;
        if ((uint)octetIndex >= (uint)message.Length)
            return;
        byte octet = message[octetIndex];
        int startBit = (start - 1) & 7;
        int endBit = ((end - 1) & 7) + 1;
        char[] display = ".... ....".ToCharArray();
        for (int i = startBit; i < endBit; i++)
            display[7 - i + (i < 4 ? 1 : 0)] = (char)('0' + ((octet >> i) & 1));
        int value = (octet >> startBit) & ((1 << (endBit - startBit)) - 1);
        string tag = "Invalid";
        foreach ((int Value, string Text) item in tags) {
            if (item.Value == value) {
                tag = item.Text;
                break;
            }
        }
        log.Flow($"  {new string(display)}= {description}: {tag}");
    }
}
