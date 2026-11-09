/*
 * TKFaxEngine - managed C# port of v80_tests.c.
 *
 * Every V.80 DTE, DCE, transparency, duplex and V.34 half-duplex escape
 * symbol from the spanDSP test is emitted in the original order.
 */

#nullable enable

using TKFaxEngine.Modem.V80;
using static TKFaxEngine.Modem.V80.V80Api;

namespace TKFaxEngineTest.Modem.Test;

public static class v80_tests {
    private static readonly (string Symbol, V80EscapeCode Code)[] Escapes =
    {
        ("V80_FROM_DTE_MFGEXTEND", V80EscapeCode.FromDteManufacturerExtended),
        ("V80_FROM_DTE_MFG1", V80EscapeCode.FromDteManufacturer1),
        ("V80_FROM_DTE_MFG2", V80EscapeCode.FromDteManufacturer2),
        ("V80_FROM_DTE_MFG3", V80EscapeCode.FromDteManufacturer3),
        ("V80_FROM_DTE_MFG4", V80EscapeCode.FromDteManufacturer4),
        ("V80_FROM_DTE_MFG5", V80EscapeCode.FromDteManufacturer5),
        ("V80_FROM_DTE_MFG6", V80EscapeCode.FromDteManufacturer6),
        ("V80_FROM_DTE_MFG7", V80EscapeCode.FromDteManufacturer7),
        ("V80_FROM_DTE_MFG8", V80EscapeCode.FromDteManufacturer8),
        ("V80_FROM_DTE_MFG9", V80EscapeCode.FromDteManufacturer9),
        ("V80_FROM_DTE_MFG10", V80EscapeCode.FromDteManufacturer10),
        ("V80_FROM_DTE_MFG11", V80EscapeCode.FromDteManufacturer11),
        ("V80_FROM_DTE_MFG12", V80EscapeCode.FromDteManufacturer12),
        ("V80_FROM_DTE_MFG13", V80EscapeCode.FromDteManufacturer13),
        ("V80_FROM_DTE_MFG14", V80EscapeCode.FromDteManufacturer14),
        ("V80_FROM_DTE_MFG15", V80EscapeCode.FromDteManufacturer15),
        ("V80_FROM_DTE_EXTEND0", V80EscapeCode.FromDteExtend0),
        ("V80_FROM_DTE_EXTEND1", V80EscapeCode.FromDteExtend1),
        ("V80_FROM_DTE_CIRCUIT_105_OFF", V80EscapeCode.FromDteCircuit105Off),
        ("V80_FROM_DTE_CIRCUIT_105_ON", V80EscapeCode.FromDteCircuit105On),
        ("V80_FROM_DTE_CIRCUIT_108_OFF", V80EscapeCode.FromDteCircuit108Off),
        ("V80_FROM_DTE_CIRCUIT_108_ON", V80EscapeCode.FromDteCircuit108On),
        ("V80_FROM_DTE_CIRCUIT_133_OFF", V80EscapeCode.FromDteCircuit133Off),
        ("V80_FROM_DTE_CIRCUIT_133_ON", V80EscapeCode.FromDteCircuit133On),
        ("V80_FROM_DTE_SINGLE_EM_P", V80EscapeCode.FromDteSingleEmPrime),
        ("V80_FROM_DTE_DOUBLE_EM_P", V80EscapeCode.FromDteDoubleEmPrime),
        ("V80_FROM_DTE_FLOW_OFF", V80EscapeCode.FromDteFlowOff),
        ("V80_FROM_DTE_FLOW_ON", V80EscapeCode.FromDteFlowOn),
        ("V80_FROM_DTE_SINGLE_EM", V80EscapeCode.FromDteSingleEm),
        ("V80_FROM_DTE_DOUBLE_EM", V80EscapeCode.FromDteDoubleEm),
        ("V80_FROM_DTE_POLL", V80EscapeCode.FromDtePoll),
        ("V80_FROM_DCE_EXTENDMFG", V80EscapeCode.FromDceManufacturerExtended),
        ("V80_FROM_DCE_MFG1", V80EscapeCode.FromDceManufacturer1),
        ("V80_FROM_DCE_MFG2", V80EscapeCode.FromDceManufacturer2),
        ("V80_FROM_DCE_MFG3", V80EscapeCode.FromDceManufacturer3),
        ("V80_FROM_DCE_MFG4", V80EscapeCode.FromDceManufacturer4),
        ("V80_FROM_DCE_MFG5", V80EscapeCode.FromDceManufacturer5),
        ("V80_FROM_DCE_MFG6", V80EscapeCode.FromDceManufacturer6),
        ("V80_FROM_DCE_MFG7", V80EscapeCode.FromDceManufacturer7),
        ("V80_FROM_DCE_MFG8", V80EscapeCode.FromDceManufacturer8),
        ("V80_FROM_DCE_MFG9", V80EscapeCode.FromDceManufacturer9),
        ("V80_FROM_DCE_MFG10", V80EscapeCode.FromDceManufacturer10),
        ("V80_FROM_DCE_MFG11", V80EscapeCode.FromDceManufacturer11),
        ("V80_FROM_DCE_MFG12", V80EscapeCode.FromDceManufacturer12),
        ("V80_FROM_DCE_MFG13", V80EscapeCode.FromDceManufacturer13),
        ("V80_FROM_DCE_MFG14", V80EscapeCode.FromDceManufacturer14),
        ("V80_FROM_DCE_MFG15", V80EscapeCode.FromDceManufacturer15),
        ("V80_FROM_DCE_EXTEND0", V80EscapeCode.FromDceExtend0),
        ("V80_FROM_DCE_EXTEND1", V80EscapeCode.FromDceExtend1),
        ("V80_FROM_DCE_CIRCUIT_106_OFF", V80EscapeCode.FromDceCircuit106Off),
        ("V80_FROM_DCE_CIRCUIT_106_ON", V80EscapeCode.FromDceCircuit106On),
        ("V80_FROM_DCE_CIRCUIT_107_OFF", V80EscapeCode.FromDceCircuit107Off),
        ("V80_FROM_DCE_CIRCUIT_107_ON", V80EscapeCode.FromDceCircuit107On),
        ("V80_FROM_DCE_CIRCUIT_109_OFF", V80EscapeCode.FromDceCircuit109Off),
        ("V80_FROM_DCE_CIRCUIT_109_ON", V80EscapeCode.FromDceCircuit109On),
        ("V80_FROM_DCE_CIRCUIT_110_OFF", V80EscapeCode.FromDceCircuit110Off),
        ("V80_FROM_DCE_CIRCUIT_110_ON", V80EscapeCode.FromDceCircuit110On),
        ("V80_FROM_DCE_CIRCUIT_125_OFF", V80EscapeCode.FromDceCircuit125Off),
        ("V80_FROM_DCE_CIRCUIT_125_ON", V80EscapeCode.FromDceCircuit125On),
        ("V80_FROM_DCE_CIRCUIT_132_OFF", V80EscapeCode.FromDceCircuit132Off),
        ("V80_FROM_DCE_CIRCUIT_132_ON", V80EscapeCode.FromDceCircuit132On),
        ("V80_FROM_DCE_CIRCUIT_142_OFF", V80EscapeCode.FromDceCircuit142Off),
        ("V80_FROM_DCE_CIRCUIT_142_ON", V80EscapeCode.FromDceCircuit142On),
        ("V80_FROM_DCE_SINGLE_EM_P", V80EscapeCode.FromDceSingleEmPrime),
        ("V80_FROM_DCE_DOUBLE_EM_P", V80EscapeCode.FromDceDoubleEmPrime),
        ("V80_FROM_DCE_OFF_LINE", V80EscapeCode.FromDceOffLine),
        ("V80_FROM_DCE_ON_LINE", V80EscapeCode.FromDceOnLine),
        ("V80_FROM_DCE_FLOW_OFF", V80EscapeCode.FromDceFlowOff),
        ("V80_FROM_DCE_FLOW_ON", V80EscapeCode.FromDceFlowOn),
        ("V80_FROM_DCE_SINGLE_EM", V80EscapeCode.FromDceSingleEm),
        ("V80_FROM_DCE_DOUBLE_EM", V80EscapeCode.FromDceDoubleEm),
        ("V80_FROM_DCE_POLL", V80EscapeCode.FromDcePoll),
        ("V80_TRANSPARENCY_T1", V80EscapeCode.TransparencyT1),
        ("V80_TRANSPARENCY_T2", V80EscapeCode.TransparencyT2),
        ("V80_TRANSPARENCY_T3", V80EscapeCode.TransparencyT3),
        ("V80_TRANSPARENCY_T4", V80EscapeCode.TransparencyT4),
        ("V80_TRANSPARENCY_T5", V80EscapeCode.TransparencyT5),
        ("V80_TRANSPARENCY_T6", V80EscapeCode.TransparencyT6),
        ("V80_TRANSPARENCY_T7", V80EscapeCode.TransparencyT7),
        ("V80_TRANSPARENCY_T8", V80EscapeCode.TransparencyT8),
        ("V80_TRANSPARENCY_T9", V80EscapeCode.TransparencyT9),
        ("V80_TRANSPARENCY_T10", V80EscapeCode.TransparencyT10),
        ("V80_TRANSPARENCY_T11", V80EscapeCode.TransparencyT11),
        ("V80_TRANSPARENCY_T12", V80EscapeCode.TransparencyT12),
        ("V80_TRANSPARENCY_T13", V80EscapeCode.TransparencyT13),
        ("V80_TRANSPARENCY_T14", V80EscapeCode.TransparencyT14),
        ("V80_TRANSPARENCY_T15", V80EscapeCode.TransparencyT15),
        ("V80_TRANSPARENCY_T16", V80EscapeCode.TransparencyT16),
        ("V80_TRANSPARENCY_T17", V80EscapeCode.TransparencyT17),
        ("V80_TRANSPARENCY_T18", V80EscapeCode.TransparencyT18),
        ("V80_TRANSPARENCY_T19", V80EscapeCode.TransparencyT19),
        ("V80_TRANSPARENCY_T20", V80EscapeCode.TransparencyT20),
        ("V80_MARK", V80EscapeCode.Mark),
        ("V80_FLAG", V80EscapeCode.Flag),
        ("V80_ERR", V80EscapeCode.Error),
        ("V80_HUNT", V80EscapeCode.Hunt),
        ("V80_UNDER", V80EscapeCode.Underflow),
        ("V80_TOVER", V80EscapeCode.TransmitOverrun),
        ("V80_ROVER", V80EscapeCode.ReceiveOverrun),
        ("V80_RESUME", V80EscapeCode.Resume),
        ("V80_BNUM", V80EscapeCode.BufferOctetCount),
        ("V80_UNUM", V80EscapeCode.DiscardedOctetCount),
        ("V80_EOT", V80EscapeCode.EndOfTransmission),
        ("V80_ECS", V80EscapeCode.EscapeToCommandState),
        ("V80_RRN", V80EscapeCode.RequestRateRenegotiation),
        ("V80_RTN", V80EscapeCode.RequestRateRetrain),
        ("V80_RATE", V80EscapeCode.Rate),
        ("V80_PRI", V80EscapeCode.PrimaryChannel),
        ("V80_CTL", V80EscapeCode.ControlChannel),
        ("V80_RTNH", V80EscapeCode.PrimaryChannelRetrain),
        ("V80_RTNC", V80EscapeCode.ControlChannelRetrain),
        ("V80_RATEH", V80EscapeCode.HalfDuplexRate),
        ("V80_EOTH", V80EscapeCode.EndOfTransmissionHalfDuplex)
    };

    public static int Main(string[] args) =>
        tkfx_v80_tests_body_main(args);

    public static int tkfx_v80_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments);

        foreach ((string symbol, V80EscapeCode code) in Escapes)
            Console.WriteLine("{0,-40} {1}", symbol, v80_escape_to_str((int)code));

        return 0;
    }
}
