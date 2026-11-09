/*
 * TKFaxEngineFX - managed C# port
 *
 * C# conversion of t30_fcf.h.
 */

namespace TKFaxEngine.Daten.T30;

/// <summary>
/// Represents the <c>T30Frame</c> type.
/// </summary>
public static class T30Frame {
    /// <summary>
    /// Defines the <c>Dis</c> constant.
    /// </summary>
    public const byte Dis = 0x80;
    /// <summary>
    /// Defines the <c>Csi</c> constant.
    /// </summary>
    public const byte Csi = 0x40;
    /// <summary>
    /// Defines the <c>Nsf</c> constant.
    /// </summary>
    public const byte Nsf = 0x20;
    /// <summary>
    /// Defines the <c>Dtc</c> constant.
    /// </summary>
    public const byte Dtc = 0x81;
    /// <summary>
    /// Defines the <c>Cig</c> constant.
    /// </summary>
    public const byte Cig = 0x41;
    /// <summary>
    /// Defines the <c>Nsc</c> constant.
    /// </summary>
    public const byte Nsc = 0x21;
    /// <summary>
    /// Defines the <c>Pwd</c> constant.
    /// </summary>
    public const byte Pwd = 0xC1;
    /// <summary>
    /// Defines the <c>Sep</c> constant.
    /// </summary>
    public const byte Sep = 0xA1;
    /// <summary>
    /// Defines the <c>Psa</c> constant.
    /// </summary>
    public const byte Psa = 0x61;
    /// <summary>
    /// Defines the <c>Cia</c> constant.
    /// </summary>
    public const byte Cia = 0xE1;
    /// <summary>
    /// Defines the <c>Isp</c> constant.
    /// </summary>
    public const byte Isp = 0x11;
    /// <summary>
    /// Defines the <c>Dcs</c> constant.
    /// </summary>
    public const byte Dcs = 0x82;
    /// <summary>
    /// Defines the <c>Tsi</c> constant.
    /// </summary>
    public const byte Tsi = 0x42;
    /// <summary>
    /// Defines the <c>Nss</c> constant.
    /// </summary>
    public const byte Nss = 0x22;
    /// <summary>
    /// Defines the <c>Sub</c> constant.
    /// </summary>
    public const byte Sub = 0xC2;
    /// <summary>
    /// Defines the <c>Sid</c> constant.
    /// </summary>
    public const byte Sid = 0xA2;
    /// <summary>
    /// Defines the <c>Ctc</c> constant.
    /// </summary>
    public const byte Ctc = 0x12;
    /// <summary>
    /// Defines the <c>Tsa</c> constant.
    /// </summary>
    public const byte Tsa = 0x62;
    /// <summary>
    /// Defines the <c>Ira</c> constant.
    /// </summary>
    public const byte Ira = 0xE2;
    /// <summary>
    /// Defines the <c>Cfr</c> constant.
    /// </summary>
    public const byte Cfr = 0x84;
    /// <summary>
    /// Defines the <c>Ftt</c> constant.
    /// </summary>
    public const byte Ftt = 0x44;
    /// <summary>
    /// Defines the <c>Ctr</c> constant.
    /// </summary>
    public const byte Ctr = 0xC4;
    /// <summary>
    /// Defines the <c>Csa</c> constant.
    /// </summary>
    public const byte Csa = 0x24;
    /// <summary>
    /// Defines the <c>Eom</c> constant.
    /// </summary>
    public const byte Eom = 0x8E;
    /// <summary>
    /// Defines the <c>Mps</c> constant.
    /// </summary>
    public const byte Mps = 0x4E;
    /// <summary>
    /// Defines the <c>Eop</c> constant.
    /// </summary>
    public const byte Eop = 0x2E;
    /// <summary>
    /// Defines the <c>PriEom</c> constant.
    /// </summary>
    public const byte PriEom = 0x9E;
    /// <summary>
    /// Defines the <c>PriMps</c> constant.
    /// </summary>
    public const byte PriMps = 0x5E;
    /// <summary>
    /// Defines the <c>PriEop</c> constant.
    /// </summary>
    public const byte PriEop = 0x3E;
    /// <summary>
    /// Defines the <c>Eos</c> constant.
    /// </summary>
    public const byte Eos = 0x1E;
    /// <summary>
    /// Defines the <c>Pps</c> constant.
    /// </summary>
    public const byte Pps = 0xBE;
    /// <summary>
    /// Defines the <c>Eor</c> constant.
    /// </summary>
    public const byte Eor = 0xCE;
    /// <summary>
    /// Defines the <c>Rr</c> constant.
    /// </summary>
    public const byte Rr = 0x6E;
    /// <summary>
    /// Defines the <c>Mcf</c> constant.
    /// </summary>
    public const byte Mcf = 0x8C;
    /// <summary>
    /// Defines the <c>Rtp</c> constant.
    /// </summary>
    public const byte Rtp = 0xCC;
    /// <summary>
    /// Defines the <c>Rtn</c> constant.
    /// </summary>
    public const byte Rtn = 0x4C;
    /// <summary>
    /// Defines the <c>Pip</c> constant.
    /// </summary>
    public const byte Pip = 0xAC;
    /// <summary>
    /// Defines the <c>Pin</c> constant.
    /// </summary>
    public const byte Pin = 0x2C;
    /// <summary>
    /// Defines the <c>Ppr</c> constant.
    /// </summary>
    public const byte Ppr = 0xBC;
    /// <summary>
    /// Defines the <c>Rnr</c> constant.
    /// </summary>
    public const byte Rnr = 0xEC;
    /// <summary>
    /// Defines the <c>Err</c> constant.
    /// </summary>
    public const byte Err = 0x1C;
    /// <summary>
    /// Defines the <c>Fdm</c> constant.
    /// </summary>
    public const byte Fdm = 0xFC;
    /// <summary>
    /// Defines the <c>Dcn</c> constant.
    /// </summary>
    public const byte Dcn = 0xFA;
    /// <summary>
    /// Defines the <c>Crp</c> constant.
    /// </summary>
    public const byte Crp = 0x1A;
    /// <summary>
    /// Defines the <c>Fnv</c> constant.
    /// </summary>
    public const byte Fnv = 0xCA;
    /// <summary>
    /// Defines the <c>Tnr</c> constant.
    /// </summary>
    public const byte Tnr = 0xEA;
    /// <summary>
    /// Defines the <c>Tr</c> constant.
    /// </summary>
    public const byte Tr = 0x6A;
    /// <summary>
    /// Defines the <c>Tk</c> constant.
    /// </summary>
    public const byte Tk = 0x4B;
    /// <summary>
    /// Defines the <c>Rk</c> constant.
    /// </summary>
    public const byte Rk = 0x4A;
    /// <summary>
    /// Defines the <c>Pss</c> constant.
    /// </summary>
    public const byte Pss = 0x1F;
    /// <summary>
    /// Defines the <c>Des</c> constant.
    /// </summary>
    public const byte Des = 0xA0;
    /// <summary>
    /// Defines the <c>Dec</c> constant.
    /// </summary>
    public const byte Dec = 0x93;
    /// <summary>
    /// Defines the <c>Der</c> constant.
    /// </summary>
    public const byte Der = 0x53;
    /// <summary>
    /// Defines the <c>Dtr</c> constant.
    /// </summary>
    public const byte Dtr = 0x11;
    /// <summary>
    /// Defines the <c>Dnk</c> constant.
    /// </summary>
    public const byte Dnk = 0x9A;
    /// <summary>
    /// Defines the <c>Pid</c> constant.
    /// </summary>
    public const byte Pid = 0x6C;
    /// <summary>
    /// Defines the <c>Spi</c> constant.
    /// </summary>
    public const byte Spi = 0x10;
    /// <summary>
    /// Defines the <c>Spt</c> constant.
    /// </summary>
    public const byte Spt = 0x80;
    /// <summary>
    /// Defines the <c>Null</c> constant.
    /// </summary>
    public const byte Null = 0x00;
    /// <summary>
    /// Defines the <c>Fcd</c> constant.
    /// </summary>
    public const byte Fcd = 0x06;
    /// <summary>
    /// Defines the <c>Ccd</c> constant.
    /// </summary>
    public const byte Ccd = 0x46;
    /// <summary>
    /// Defines the <c>Rcp</c> constant.
    /// </summary>
    public const byte Rcp = 0x86;


    // Original TKFaxEngineFX identifiers.
    /// <summary>
    /// Defines the <c>T30_DIS</c> constant.
    /// </summary>
    public const byte T30_DIS = 0x80;
    /// <summary>
    /// Defines the <c>T30_CSI</c> constant.
    /// </summary>
    public const byte T30_CSI = 0x40;
    /// <summary>
    /// Defines the <c>T30_NSF</c> constant.
    /// </summary>
    public const byte T30_NSF = 0x20;
    /// <summary>
    /// Defines the <c>T30_DTC</c> constant.
    /// </summary>
    public const byte T30_DTC = 0x81;
    /// <summary>
    /// Defines the <c>T30_CIG</c> constant.
    /// </summary>
    public const byte T30_CIG = 0x41;
    /// <summary>
    /// Defines the <c>T30_NSC</c> constant.
    /// </summary>
    public const byte T30_NSC = 0x21;
    /// <summary>
    /// Defines the <c>T30_PWD</c> constant.
    /// </summary>
    public const byte T30_PWD = 0xC1;
    /// <summary>
    /// Defines the <c>T30_SEP</c> constant.
    /// </summary>
    public const byte T30_SEP = 0xA1;
    /// <summary>
    /// Defines the <c>T30_PSA</c> constant.
    /// </summary>
    public const byte T30_PSA = 0x61;
    /// <summary>
    /// Defines the <c>T30_CIA</c> constant.
    /// </summary>
    public const byte T30_CIA = 0xE1;
    /// <summary>
    /// Defines the <c>T30_ISP</c> constant.
    /// </summary>
    public const byte T30_ISP = 0x11;
    /// <summary>
    /// Defines the <c>T30_DCS</c> constant.
    /// </summary>
    public const byte T30_DCS = 0x82;
    /// <summary>
    /// Defines the <c>T30_TSI</c> constant.
    /// </summary>
    public const byte T30_TSI = 0x42;
    /// <summary>
    /// Defines the <c>T30_NSS</c> constant.
    /// </summary>
    public const byte T30_NSS = 0x22;
    /// <summary>
    /// Defines the <c>T30_SUB</c> constant.
    /// </summary>
    public const byte T30_SUB = 0xC2;
    /// <summary>
    /// Defines the <c>T30_SID</c> constant.
    /// </summary>
    public const byte T30_SID = 0xA2;
    /// <summary>
    /// Defines the <c>T30_CTC</c> constant.
    /// </summary>
    public const byte T30_CTC = 0x12;
    /// <summary>
    /// Defines the <c>T30_TSA</c> constant.
    /// </summary>
    public const byte T30_TSA = 0x62;
    /// <summary>
    /// Defines the <c>T30_IRA</c> constant.
    /// </summary>
    public const byte T30_IRA = 0xE2;
    /// <summary>
    /// Defines the <c>T30_CFR</c> constant.
    /// </summary>
    public const byte T30_CFR = 0x84;
    /// <summary>
    /// Defines the <c>T30_FTT</c> constant.
    /// </summary>
    public const byte T30_FTT = 0x44;
    /// <summary>
    /// Defines the <c>T30_CTR</c> constant.
    /// </summary>
    public const byte T30_CTR = 0xC4;
    /// <summary>
    /// Defines the <c>T30_CSA</c> constant.
    /// </summary>
    public const byte T30_CSA = 0x24;
    /// <summary>
    /// Defines the <c>T30_EOM</c> constant.
    /// </summary>
    public const byte T30_EOM = 0x8E;
    /// <summary>
    /// Defines the <c>T30_MPS</c> constant.
    /// </summary>
    public const byte T30_MPS = 0x4E;
    /// <summary>
    /// Defines the <c>T30_EOP</c> constant.
    /// </summary>
    public const byte T30_EOP = 0x2E;
    /// <summary>
    /// Defines the <c>T30_PRI_EOM</c> constant.
    /// </summary>
    public const byte T30_PRI_EOM = 0x9E;
    /// <summary>
    /// Defines the <c>T30_PRI_MPS</c> constant.
    /// </summary>
    public const byte T30_PRI_MPS = 0x5E;
    /// <summary>
    /// Defines the <c>T30_PRI_EOP</c> constant.
    /// </summary>
    public const byte T30_PRI_EOP = 0x3E;
    /// <summary>
    /// Defines the <c>T30_EOS</c> constant.
    /// </summary>
    public const byte T30_EOS = 0x1E;
    /// <summary>
    /// Defines the <c>T30_PPS</c> constant.
    /// </summary>
    public const byte T30_PPS = 0xBE;
    /// <summary>
    /// Defines the <c>T30_EOR</c> constant.
    /// </summary>
    public const byte T30_EOR = 0xCE;
    /// <summary>
    /// Defines the <c>T30_RR</c> constant.
    /// </summary>
    public const byte T30_RR = 0x6E;
    /// <summary>
    /// Defines the <c>T30_MCF</c> constant.
    /// </summary>
    public const byte T30_MCF = 0x8C;
    /// <summary>
    /// Defines the <c>T30_RTP</c> constant.
    /// </summary>
    public const byte T30_RTP = 0xCC;
    /// <summary>
    /// Defines the <c>T30_RTN</c> constant.
    /// </summary>
    public const byte T30_RTN = 0x4C;
    /// <summary>
    /// Defines the <c>T30_PIP</c> constant.
    /// </summary>
    public const byte T30_PIP = 0xAC;
    /// <summary>
    /// Defines the <c>T30_PIN</c> constant.
    /// </summary>
    public const byte T30_PIN = 0x2C;
    /// <summary>
    /// Defines the <c>T30_PPR</c> constant.
    /// </summary>
    public const byte T30_PPR = 0xBC;
    /// <summary>
    /// Defines the <c>T30_RNR</c> constant.
    /// </summary>
    public const byte T30_RNR = 0xEC;
    /// <summary>
    /// Defines the <c>T30_ERR</c> constant.
    /// </summary>
    public const byte T30_ERR = 0x1C;
    /// <summary>
    /// Defines the <c>T30_FDM</c> constant.
    /// </summary>
    public const byte T30_FDM = 0xFC;
    /// <summary>
    /// Defines the <c>T30_DCN</c> constant.
    /// </summary>
    public const byte T30_DCN = 0xFA;
    /// <summary>
    /// Defines the <c>T30_CRP</c> constant.
    /// </summary>
    public const byte T30_CRP = 0x1A;
    /// <summary>
    /// Defines the <c>T30_FNV</c> constant.
    /// </summary>
    public const byte T30_FNV = 0xCA;
    /// <summary>
    /// Defines the <c>T30_TNR</c> constant.
    /// </summary>
    public const byte T30_TNR = 0xEA;
    /// <summary>
    /// Defines the <c>T30_TR</c> constant.
    /// </summary>
    public const byte T30_TR = 0x6A;
    /// <summary>
    /// Defines the <c>T30_TK</c> constant.
    /// </summary>
    public const byte T30_TK = 0x4B;
    /// <summary>
    /// Defines the <c>T30_RK</c> constant.
    /// </summary>
    public const byte T30_RK = 0x4A;
    /// <summary>
    /// Defines the <c>T30_PSS</c> constant.
    /// </summary>
    public const byte T30_PSS = 0x1F;
    /// <summary>
    /// Defines the <c>T30_DES</c> constant.
    /// </summary>
    public const byte T30_DES = 0xA0;
    /// <summary>
    /// Defines the <c>T30_DEC</c> constant.
    /// </summary>
    public const byte T30_DEC = 0x93;
    /// <summary>
    /// Defines the <c>T30_DER</c> constant.
    /// </summary>
    public const byte T30_DER = 0x53;
    /// <summary>
    /// Defines the <c>T30_DTR</c> constant.
    /// </summary>
    public const byte T30_DTR = 0x11;
    /// <summary>
    /// Defines the <c>T30_DNK</c> constant.
    /// </summary>
    public const byte T30_DNK = 0x9A;
    /// <summary>
    /// Defines the <c>T30_PID</c> constant.
    /// </summary>
    public const byte T30_PID = 0x6C;
    /// <summary>
    /// Defines the <c>T30_SPI</c> constant.
    /// </summary>
    public const byte T30_SPI = 0x10;
    /// <summary>
    /// Defines the <c>T30_SPT</c> constant.
    /// </summary>
    public const byte T30_SPT = 0x80;
    /// <summary>
    /// Defines the <c>T30_NULL</c> constant.
    /// </summary>
    public const byte T30_NULL = 0x00;
    /// <summary>
    /// Defines the <c>T4_FCD</c> constant.
    /// </summary>
    public const byte T4_FCD = 0x06;
    /// <summary>
    /// Defines the <c>T4_CCD</c> constant.
    /// </summary>
    public const byte T4_CCD = 0x46;
    /// <summary>
    /// Defines the <c>T4_RCP</c> constant.
    /// </summary>
    public const byte T4_RCP = 0x86;
}
