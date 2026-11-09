/*
 * TKFaxEngine - managed C# port
 *
 * V18SignalTables.cs
 */

#nullable enable

using System.Collections.Generic;

namespace TKFaxEngine.Modem.V18;

public sealed partial class V18State {
    private const int InitialCallingSilenceMilliseconds = 1000;
    private const int CiSignalMilliseconds = 400;
    private const int CiInterBurstSilenceMilliseconds = 2000;
    private const int CiToXciSilenceMilliseconds = 2000;
    private const int XciSignalMilliseconds = 3000;
    private const int XciPostSilenceMilliseconds = 1000;
    private const int AnsToTxpSilenceMilliseconds = 500;
    private const int AnswerAnsamMilliseconds = 3000;
    private const int AnswerTxpSilenceMilliseconds = 75;
    private const int ProbeAnswerToneMilliseconds = 1000;
    private const int ProbeAnswerToneSilenceMilliseconds = 75;
    private const int DtmfOnMilliseconds = 70;
    private const int DtmfOffMilliseconds = 50;
    private const int DtmfReceiveSuppressionMilliseconds = 300;
    private const int BaudotCarrierPreambleMilliseconds = 150;
    private const int EdtCarrierPreambleMilliseconds = 300;
    private const int TxpAnswerRepetitions = 3;
    private const int AnswerTxpMilliseconds = 400;

    private static readonly byte[] CiPacketBits = CreateCiPacketBits();
    private static readonly byte[] XciMarkerBits = CreateXciMarkerBits();
    private static readonly byte[] CiBits = CreateCiBits();
    private static readonly byte[] XciBits = CreateXciBits();
    private static readonly byte[] TxpBits = CreateTxpBits();

    private static byte[] CreateCiPacketBits() {
        // One V.8 CI packet: ten mark bits followed by async 0x00 and
        // async call-function V.18 (0x41), LSB first.
        var bits = new List<byte>(30);
        AppendMark(bits, 10);
        AppendAsyncOctet(bits, 0x00);
        AppendAsyncOctet(bits, 0x41);
        return bits.ToArray();
    }

    private static byte[] CreateCiBits() {
        // Four identical CI packets occupy the complete 400 ms CI burst.
        var bits = new List<byte>(CiPacketBits.Length * 4);
        for (int packet = 0; packet < 4; packet++)
            bits.AddRange(CiPacketBits);
        return bits.ToArray();
    }

    private static byte[] CreateXciBits() {
        // V.23 high-band XCI: 400 ms mark, four async-FF markers separated
        // by 800 ms mark, and a final 100 ms mark.
        var bits = new List<byte>(3560);
        AppendMark(bits, 480);
        AppendXciMarker(bits);
        AppendMark(bits, 960);
        AppendXciMarker(bits);
        AppendMark(bits, 960);
        AppendXciMarker(bits);
        AppendMark(bits, 960);
        AppendXciMarker(bits);
        AppendMark(bits, 120);
        return bits.ToArray();
    }

    private static byte[] CreateTxpBits() {
        const string sequence = "1111111111000101011100001101110000010101";
        var bits = new byte[sequence.Length];
        for (int i = 0; i < sequence.Length; i++)
            bits[i] = sequence[i] == '1' ? (byte)1 : (byte)0;
        return bits;
    }

    private static byte[] CreateXciMarkerBits() {
        var bits = new List<byte>(20);
        AppendXciMarker(bits);
        return bits.ToArray();
    }

    private static void AppendXciMarker(List<byte> bits) {
        AppendAsyncOctet(bits, 0xFF);
        AppendAsyncOctet(bits, 0xFF);
    }

    private static void AppendAsyncOctet(List<byte> bits, byte value) {
        bits.Add(0);
        for (int bit = 0; bit < 8; bit++)
            bits.Add((byte)((value >> bit) & 1));
        bits.Add(1);
    }

    private static void AppendMark(List<byte> bits, int count) {
        for (int i = 0; i < count; i++)
            bits.Add(1);
    }

    private static readonly float[] ToneFrequencies =
    {
        390.0f, 980.0f, 1180.0f, 1270.0f, 1300.0f,
        1400.0f, 1650.0f, 1800.0f, 2225.0f
    };
    private static readonly int[,] ToneTargetDurations =
    {
        {
            MillisecondsToSamples(3000),
            MillisecondsToSamples(1500),
            0,
            MillisecondsToSamples(700),
            MillisecondsToSamples(1700),
            0,
            MillisecondsToSamples(500),
            0,
            MillisecondsToSamples(500)
        },
        {
            MillisecondsToSamples(3000),
            MillisecondsToSamples(1500),
            0,
            MillisecondsToSamples(700),
            MillisecondsToSamples(1700),
            0,
            MillisecondsToSamples(400),
            0,
            MillisecondsToSamples(1000)
        }
    };
    private static readonly V18Mode[,] AutomodingSequences =
    {
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Edt, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Dtmf, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.Edt, V18Mode.V23Videotex, V18Mode.Bell103 },
        { V18Mode.V21Textphone, V18Mode.Weitbrecht5Bit50, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Bell103 },
        { V18Mode.Weitbrecht5Bit4545, V18Mode.Bell103, V18Mode.V21Textphone, V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf },
        { V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.Bell103 },
        { V18Mode.V23Videotex, V18Mode.Edt, V18Mode.Dtmf, V18Mode.Weitbrecht5Bit50, V18Mode.V21Textphone, V18Mode.Bell103 }
    };
}
