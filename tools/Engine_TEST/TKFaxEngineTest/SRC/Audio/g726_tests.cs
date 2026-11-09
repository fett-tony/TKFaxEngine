/* TKFaxEngine - g726_tests.cs. Managed 1:1 test-flow port of spanDSP g726_tests.c. */
#nullable enable

using System.Globalization;
using System.Runtime.InteropServices;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.g726;

namespace TKFaxEngineTest.Audio.Test;

public static class g726_tests {
    private const int BLOCK_LEN = 320, MAX_TEST_VECTOR_LEN = 40000, G726_ENCODING_NONE = 9999;
    private const string TESTDATA_DIR = "Test-Files/itu/g726/", IN_FILE_NAME = "Test-Files/local/short_nb_voice.wav", OUT_FILE_NAME = "post_g726.wav";
    private sealed record TestSet(string ConditioningPcmFile, string PcmFile, string ConditioningAdpcmFile, string AdpcmFile, string OutputFile, int Rate, int CompressionLaw, int DecompressionLaw);
    private static readonly TestSet[] itu_test_sets =
    {
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/16/RN16FM.I", TESTDATA_DIR + "DISK1/RESET/16/RN16FM.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", "", TESTDATA_DIR + "DISK1/INPUT/I16", TESTDATA_DIR + "DISK1/RESET/16/RI16FM.O", 16000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/16/RV16FM.I", TESTDATA_DIR + "DISK1/RESET/16/RV16FM.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/24/RN24FM.I", TESTDATA_DIR + "DISK1/RESET/24/RN24FM.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", "", TESTDATA_DIR + "DISK1/INPUT/I24", TESTDATA_DIR + "DISK1/RESET/24/RI24FM.O", 24000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/24/RV24FM.I", TESTDATA_DIR + "DISK1/RESET/24/RV24FM.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/32/RN32FM.I", TESTDATA_DIR + "DISK1/RESET/32/RN32FM.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", "", TESTDATA_DIR + "DISK1/INPUT/I32", TESTDATA_DIR + "DISK1/RESET/32/RI32FM.O", 32000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/32/RV32FM.I", TESTDATA_DIR + "DISK1/RESET/32/RV32FM.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/40/RN40FM.I", TESTDATA_DIR + "DISK1/RESET/40/RN40FM.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", "", TESTDATA_DIR + "DISK1/INPUT/I40", TESTDATA_DIR + "DISK1/RESET/40/RI40FM.O", 40000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/40/RV40FM.I", TESTDATA_DIR + "DISK1/RESET/40/RV40FM.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/16/RN16FA.I", TESTDATA_DIR + "DISK2/RESET/16/RN16FA.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", "", TESTDATA_DIR + "DISK2/INPUT/I16", TESTDATA_DIR + "DISK2/RESET/16/RI16FA.O", 16000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/16/RV16FA.I", TESTDATA_DIR + "DISK2/RESET/16/RV16FA.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/24/RN24FA.I", TESTDATA_DIR + "DISK2/RESET/24/RN24FA.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", "", TESTDATA_DIR + "DISK2/INPUT/I24", TESTDATA_DIR + "DISK2/RESET/24/RI24FA.O", 24000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/24/RV24FA.I", TESTDATA_DIR + "DISK2/RESET/24/RV24FA.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/32/RN32FA.I", TESTDATA_DIR + "DISK2/RESET/32/RN32FA.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", "", TESTDATA_DIR + "DISK2/INPUT/I32", TESTDATA_DIR + "DISK2/RESET/32/RI32FA.O", 32000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/32/RV32FA.I", TESTDATA_DIR + "DISK2/RESET/32/RV32FA.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/40/RN40FA.I", TESTDATA_DIR + "DISK2/RESET/40/RN40FA.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", "", TESTDATA_DIR + "DISK2/INPUT/I40", TESTDATA_DIR + "DISK2/RESET/40/RI40FA.O", 40000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/40/RV40FA.I", TESTDATA_DIR + "DISK2/RESET/40/RV40FA.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/16/RN16FM.I", TESTDATA_DIR + "DISK1/RESET/16/RN16FC.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/16/RV16FM.I", TESTDATA_DIR + "DISK1/RESET/16/RV16FC.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/24/RN24FM.I", TESTDATA_DIR + "DISK1/RESET/24/RN24FC.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/24/RV24FM.I", TESTDATA_DIR + "DISK1/RESET/24/RV24FC.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/32/RN32FM.I", TESTDATA_DIR + "DISK1/RESET/32/RN32FC.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/32/RV32FM.I", TESTDATA_DIR + "DISK1/RESET/32/RV32FC.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/NRM.M", "", TESTDATA_DIR + "DISK1/RESET/40/RN40FM.I", TESTDATA_DIR + "DISK1/RESET/40/RN40FC.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK1/INPUT/OVR.M", "", TESTDATA_DIR + "DISK1/RESET/40/RV40FM.I", TESTDATA_DIR + "DISK1/RESET/40/RV40FC.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/16/RN16FA.I", TESTDATA_DIR + "DISK2/RESET/16/RN16FX.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/16/RV16FA.I", TESTDATA_DIR + "DISK2/RESET/16/RV16FX.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/24/RN24FA.I", TESTDATA_DIR + "DISK2/RESET/24/RN24FX.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/24/RV24FA.I", TESTDATA_DIR + "DISK2/RESET/24/RV24FX.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/32/RN32FA.I", TESTDATA_DIR + "DISK2/RESET/32/RN32FX.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/32/RV32FA.I", TESTDATA_DIR + "DISK2/RESET/32/RV32FX.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/NRM.A", "", TESTDATA_DIR + "DISK2/RESET/40/RN40FA.I", TESTDATA_DIR + "DISK2/RESET/40/RN40FX.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new("", TESTDATA_DIR + "DISK2/INPUT/OVR.A", "", TESTDATA_DIR + "DISK2/RESET/40/RV40FA.I", TESTDATA_DIR + "DISK2/RESET/40/RV40FX.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK1/HOMING/16/I_INI_16.M", TESTDATA_DIR + "DISK1/HOMING/16/HN16FM.I", TESTDATA_DIR + "DISK1/HOMING/16/HN16FM.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", TESTDATA_DIR + "DISK1/HOMING/16/I_INI_16.M", TESTDATA_DIR + "DISK1/INPUT/I16", TESTDATA_DIR + "DISK1/HOMING/16/HI16FM.O", 16000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK1/HOMING/16/I_INI_16.M", TESTDATA_DIR + "DISK1/HOMING/16/HV16FM.I", TESTDATA_DIR + "DISK1/HOMING/16/HV16FM.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK1/HOMING/24/I_INI_24.M", TESTDATA_DIR + "DISK1/HOMING/24/HN24FM.I", TESTDATA_DIR + "DISK1/HOMING/24/HN24FM.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", TESTDATA_DIR + "DISK1/HOMING/24/I_INI_24.M", TESTDATA_DIR + "DISK1/INPUT/I24", TESTDATA_DIR + "DISK1/HOMING/24/HI24FM.O", 24000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK1/HOMING/24/I_INI_24.M", TESTDATA_DIR + "DISK1/HOMING/24/HV24FM.I", TESTDATA_DIR + "DISK1/HOMING/24/HV24FM.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK1/HOMING/32/I_INI_32.M", TESTDATA_DIR + "DISK1/HOMING/32/HN32FM.I", TESTDATA_DIR + "DISK1/HOMING/32/HN32FM.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", TESTDATA_DIR + "DISK1/HOMING/32/I_INI_32.M", TESTDATA_DIR + "DISK1/INPUT/I32", TESTDATA_DIR + "DISK1/HOMING/32/HI32FM.O", 32000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK1/HOMING/32/I_INI_32.M", TESTDATA_DIR + "DISK1/HOMING/32/HV32FM.I", TESTDATA_DIR + "DISK1/HOMING/32/HV32FM.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK1/HOMING/40/I_INI_40.M", TESTDATA_DIR + "DISK1/HOMING/40/HN40FM.I", TESTDATA_DIR + "DISK1/HOMING/40/HN40FM.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new("", "", TESTDATA_DIR + "DISK1/HOMING/40/I_INI_40.M", TESTDATA_DIR + "DISK1/INPUT/I40", TESTDATA_DIR + "DISK1/HOMING/40/HI40FM.O", 40000, G726_ENCODING_NONE, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK1/HOMING/40/I_INI_40.M", TESTDATA_DIR + "DISK1/HOMING/40/HV40FM.I", TESTDATA_DIR + "DISK1/HOMING/40/HV40FM.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK2/HOMING/16/I_INI_16.A", TESTDATA_DIR + "DISK2/HOMING/16/HN16FA.I", TESTDATA_DIR + "DISK2/HOMING/16/HN16FA.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", TESTDATA_DIR + "DISK2/HOMING/16/I_INI_16.A", TESTDATA_DIR + "DISK2/INPUT/I16", TESTDATA_DIR + "DISK2/HOMING/16/HI16FA.O", 16000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK2/HOMING/16/I_INI_16.A", TESTDATA_DIR + "DISK2/HOMING/16/HV16FA.I", TESTDATA_DIR + "DISK2/HOMING/16/HV16FA.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK2/HOMING/24/I_INI_24.A", TESTDATA_DIR + "DISK2/HOMING/24/HN24FA.I", TESTDATA_DIR + "DISK2/HOMING/24/HN24FA.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", TESTDATA_DIR + "DISK2/HOMING/24/I_INI_24.A", TESTDATA_DIR + "DISK2/INPUT/I24", TESTDATA_DIR + "DISK2/HOMING/24/HI24FA.O", 24000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK2/HOMING/24/I_INI_24.A", TESTDATA_DIR + "DISK2/HOMING/24/HV24FA.I", TESTDATA_DIR + "DISK2/HOMING/24/HV24FA.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK2/HOMING/32/I_INI_32.A", TESTDATA_DIR + "DISK2/HOMING/32/HN32FA.I", TESTDATA_DIR + "DISK2/HOMING/32/HN32FA.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", TESTDATA_DIR + "DISK2/HOMING/32/I_INI_32.A", TESTDATA_DIR + "DISK2/INPUT/I32", TESTDATA_DIR + "DISK2/HOMING/32/HI32FA.O", 32000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK2/HOMING/32/I_INI_32.A", TESTDATA_DIR + "DISK2/HOMING/32/HV32FA.I", TESTDATA_DIR + "DISK2/HOMING/32/HV32FA.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK2/HOMING/40/I_INI_40.A", TESTDATA_DIR + "DISK2/HOMING/40/HN40FA.I", TESTDATA_DIR + "DISK2/HOMING/40/HN40FA.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new("", "", TESTDATA_DIR + "DISK2/HOMING/40/I_INI_40.A", TESTDATA_DIR + "DISK2/INPUT/I40", TESTDATA_DIR + "DISK2/HOMING/40/HI40FA.O", 40000, G726_ENCODING_NONE, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK2/HOMING/40/I_INI_40.A", TESTDATA_DIR + "DISK2/HOMING/40/HV40FA.I", TESTDATA_DIR + "DISK2/HOMING/40/HV40FA.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK2/HOMING/16/I_INI_16.A", TESTDATA_DIR + "DISK1/HOMING/16/HN16FM.I", TESTDATA_DIR + "DISK1/HOMING/16/HN16FC.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK2/HOMING/16/I_INI_16.A", TESTDATA_DIR + "DISK1/HOMING/16/HV16FM.I", TESTDATA_DIR + "DISK1/HOMING/16/HV16FC.O", 16000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK2/HOMING/24/I_INI_24.A", TESTDATA_DIR + "DISK1/HOMING/24/HN24FM.I", TESTDATA_DIR + "DISK1/HOMING/24/HN24FC.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK2/HOMING/24/I_INI_24.A", TESTDATA_DIR + "DISK1/HOMING/24/HV24FM.I", TESTDATA_DIR + "DISK1/HOMING/24/HV24FC.O", 24000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK2/HOMING/32/I_INI_32.A", TESTDATA_DIR + "DISK1/HOMING/32/HN32FM.I", TESTDATA_DIR + "DISK1/HOMING/32/HN32FC.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK2/HOMING/32/I_INI_32.A", TESTDATA_DIR + "DISK1/HOMING/32/HV32FM.I", TESTDATA_DIR + "DISK1/HOMING/32/HV32FC.O", 32000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/NRM.M", TESTDATA_DIR + "DISK2/HOMING/40/I_INI_40.A", TESTDATA_DIR + "DISK1/HOMING/40/HN40FM.I", TESTDATA_DIR + "DISK1/HOMING/40/HN40FC.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK1/PCM_INIT.M", TESTDATA_DIR + "DISK1/INPUT/OVR.M", TESTDATA_DIR + "DISK2/HOMING/40/I_INI_40.A", TESTDATA_DIR + "DISK1/HOMING/40/HV40FM.I", TESTDATA_DIR + "DISK1/HOMING/40/HV40FC.O", 40000, G726_ENCODING_ULAW, G726_ENCODING_ALAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK1/HOMING/16/I_INI_16.M", TESTDATA_DIR + "DISK2/HOMING/16/HN16FA.I", TESTDATA_DIR + "DISK2/HOMING/16/HN16FX.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK1/HOMING/16/I_INI_16.M", TESTDATA_DIR + "DISK2/HOMING/16/HV16FA.I", TESTDATA_DIR + "DISK2/HOMING/16/HV16FX.O", 16000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK1/HOMING/24/I_INI_24.M", TESTDATA_DIR + "DISK2/HOMING/24/HN24FA.I", TESTDATA_DIR + "DISK2/HOMING/24/HN24FX.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK1/HOMING/24/I_INI_24.M", TESTDATA_DIR + "DISK2/HOMING/24/HV24FA.I", TESTDATA_DIR + "DISK2/HOMING/24/HV24FX.O", 24000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK1/HOMING/32/I_INI_32.M", TESTDATA_DIR + "DISK2/HOMING/32/HN32FA.I", TESTDATA_DIR + "DISK2/HOMING/32/HN32FX.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK1/HOMING/32/I_INI_32.M", TESTDATA_DIR + "DISK2/HOMING/32/HV32FA.I", TESTDATA_DIR + "DISK2/HOMING/32/HV32FX.O", 32000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/NRM.A", TESTDATA_DIR + "DISK1/HOMING/40/I_INI_40.M", TESTDATA_DIR + "DISK2/HOMING/40/HN40FA.I", TESTDATA_DIR + "DISK2/HOMING/40/HN40FX.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
        new(TESTDATA_DIR + "DISK2/PCM_INIT.A", TESTDATA_DIR + "DISK2/INPUT/OVR.A", TESTDATA_DIR + "DISK1/HOMING/40/I_INI_40.M", TESTDATA_DIR + "DISK2/HOMING/40/HV40FA.I", TESTDATA_DIR + "DISK2/HOMING/40/HV40FX.O", 40000, G726_ENCODING_ALAW, G726_ENCODING_ULAW),
    };
    public sealed class G726TestsExitException : Exception { public G726TestsExitException(int exitCode) : base($"g726_tests exited with code {exitCode}.") => ExitCode = exitCode; public int ExitCode { get; } }
    private static void Exit(int code) => throw new G726TestsExitException(code);
    public static int main(string[] testArguments) { try { return tkfx_g726_tests_body_main(testArguments); } catch (G726TestsExitException caught) { return caught.ExitCode; } }
    private static byte[] GetTestVector(string path) {
        List<byte> data = new(MAX_TEST_VECTOR_LEN);
        foreach (string raw in File.ReadLines(path)) { string line = raw.Trim(); for (int pos = 0; pos + 2 <= line.Length; pos += 2) { if (!byte.TryParse(line.Substring(pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value)) break; data.Add(value); if (data.Count > MAX_TEST_VECTOR_LEN) throw new InvalidDataException($"Vector '{path}' is too long."); } }
        if (data.Count == 0) throw new InvalidDataException($"Vector '{path}' is empty."); byte checksum = data[^1]; data.RemoveAt(data.Count - 1); int sum = 0; foreach (byte value in data) sum += value; if (sum % 255 != checksum) { Console.Error.WriteLine($"    Sumcheck failed in '{path}' - {sum % 255:x} {checksum:x}"); Exit(2); }
        return data.ToArray();
    }
    private static byte[] Concat(byte[] a, byte[] b) { byte[] r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
    private static void ItuComplianceTests() {
        for (int test = 0; test < itu_test_sets.Length; test++) {
            TestSet set = itu_test_sets[test];
            Console.WriteLine($"Test {test,2}: '{set.ConditioningPcmFile}' + '{set.PcmFile}'\n      -> '{set.ConditioningAdpcmFile}' + '{set.AdpcmFile}'\n      -> '{set.OutputFile}' [{set.Rate}, {set.CompressionLaw}, {set.DecompressionLaw}]");

            byte[] encoded = Array.Empty<byte>();
            int conditioningSamples = 0;
            if (set.CompressionLaw != G726_ENCODING_NONE) {
                g726_state_t encoder = g726_init(null, set.Rate, set.CompressionLaw, G726_PACKING_NONE)
                    ?? throw new InvalidOperationException("g726_init failed");
                byte[] conditioning = string.IsNullOrEmpty(set.ConditioningPcmFile)
                    ? Array.Empty<byte>()
                    : GetTestVector(set.ConditioningPcmFile);
                conditioningSamples = conditioning.Length;
                if (conditioningSamples != 0)
                    Console.WriteLine($"Test {test}: Homing {conditioningSamples} samples at {set.Rate}bps");

                byte[] samples = GetTestVector(set.PcmFile);
                Console.WriteLine($"Test {test}: Compressing {samples.Length} samples at {set.Rate}bps");
                byte[] input = Concat(conditioning, samples);
                short[] inputSamples = new short[(input.Length + sizeof(short) - 1) / sizeof(short)];
                input.AsSpan().CopyTo(MemoryMarshal.AsBytes(inputSamples.AsSpan()));

                encoded = new byte[input.Length * 2 + 16];
                int encodedLength = g726_encode(encoder, encoded, inputSamples, input.Length);
                Array.Resize(ref encoded, encodedLength);
                g726_free(encoder);
            }

            g726_state_t decoder = g726_init(null, set.Rate, set.DecompressionLaw, G726_PACKING_NONE)
                ?? throw new InvalidOperationException("g726_init failed");
            byte[] conditioningAdpcm = string.IsNullOrEmpty(set.ConditioningAdpcmFile)
                ? Array.Empty<byte>()
                : GetTestVector(set.ConditioningAdpcmFile);
            if (conditioningAdpcm.Length != 0)
                Console.WriteLine($"Test {test}: Homing {conditioningAdpcm.Length} octets at {set.Rate}bps");

            byte[] adpcm = GetTestVector(set.AdpcmFile);
            byte[] referenceAdpcm = Concat(conditioningAdpcm, adpcm);
            if (set.CompressionLaw != G726_ENCODING_NONE) {
                Console.WriteLine($"Test {test}: Compressed data check - {referenceAdpcm.Length}/{encoded.Length} octets");
                if (referenceAdpcm.Length != encoded.Length) {
                    Console.WriteLine($"Test {test}: Length mismatch - ref = {referenceAdpcm.Length}, processed = {encoded.Length}");
                    g726_free(decoder);
                    Exit(2);
                }

                int bad = 0;
                for (int i = conditioningSamples; i < encoded.Length; i++) {
                    if (encoded[i] == referenceAdpcm[i])
                        continue;
                    bad++;
                    Console.WriteLine($"Test {test}: Compressed mismatch {i} {encoded[i]:x} {referenceAdpcm[i]:x}");
                }
                if (bad != 0) {
                    Console.WriteLine("Test failed");
                    g726_free(decoder);
                    Exit(2);
                }
                Console.WriteLine("Test passed");
            }

            short[] decoded = new short[(referenceAdpcm.Length + sizeof(short) - 1) / sizeof(short)];
            int decodedLength = g726_decode(decoder, decoded, referenceAdpcm, referenceAdpcm.Length);
            ReadOnlySpan<byte> decodedBytes = MemoryMarshal.AsBytes(decoded.AsSpan());
            byte[] expected = GetTestVector(set.OutputFile);
            int conditioningOutput = conditioningAdpcm.Length;
            Console.WriteLine($"Test {test}: Decompressed data check - {expected.Length}/{decodedLength - conditioningOutput} samples");
            if (expected.Length != decodedLength - conditioningOutput) {
                Console.WriteLine($"Test {test}: Length mismatch - ref = {expected.Length}, processed = {decodedLength - conditioningOutput}");
                g726_free(decoder);
                Exit(2);
            }

            int badDecoded = 0;
            for (int i = 0; i < expected.Length; i++) {
                byte actual = decodedBytes[i + conditioningOutput];
                if (expected[i] == actual)
                    continue;
                badDecoded++;
                Console.WriteLine($"Test {test}: Decompressed mismatch {i} {expected[i]:x} {actual:x}");
            }
            g726_free(decoder);
            if (badDecoded != 0) {
                Console.WriteLine("Test failed");
                Exit(2);
            }
            Console.WriteLine("Test passed");
        }
        Console.WriteLine("Tests passed.");
    }
    public static int tkfx_g726_tests_body_main(string[] testArguments) {
        ArgumentNullException.ThrowIfNull(testArguments); int bitRate = 32000, packing = G726_PACKING_NONE; bool ituTests = true;
        for (int i = 0; i < testArguments.Length; i++) {
            switch (testArguments[i]) {
                case "-b":
                    if (++i >= testArguments.Length || !int.TryParse(testArguments[i], out bitRate) || (bitRate != 16000 && bitRate != 24000 && bitRate != 32000 && bitRate != 40000)) {
                        Console.Error.WriteLine("Invalid bit rate selected. Only 16000, 24000, 32000 and 40000 are valid.");
                        Exit(2);
                    }
                    ituTests = false;
                    break;
                case "-L": packing = G726_PACKING_LEFT; break;
                case "-R": packing = G726_PACKING_RIGHT; break;
                default: if (testArguments[i].StartsWith("-", StringComparison.Ordinal)) Exit(2); break;
            }
        }
        if (ituTests) { ItuComplianceTests(); return 0; }
        using Pcm16WaveReader reader = new(IN_FILE_NAME, 8000, 1); using Pcm16WaveWriter writer = new(OUT_FILE_NAME, 8000, 1); Console.WriteLine($"ADPCM packing is {packing}"); g726_state_t encoder = g726_init(null, bitRate, G726_ENCODING_LINEAR, packing) ?? throw new InvalidOperationException("g726_init encoder failed"); g726_state_t decoder = g726_init(null, bitRate, G726_ENCODING_LINEAR, packing) ?? throw new InvalidOperationException("g726_init decoder failed"); short[] samples = new short[1024]; byte[] data = new byte[2048]; while (true) { int frames = reader.ReadFrames(samples.AsSpan(0, 159)); if (frames <= 0) break; int bytes = g726_encode(encoder, data, samples, frames); int decoded = g726_decode(decoder, samples, data, bytes); writer.WriteFrames(samples.AsSpan(0, decoded)); }
        g726_free(encoder); g726_free(decoder); Console.WriteLine($"'{IN_FILE_NAME}' transcoded to '{OUT_FILE_NAME}' at {bitRate}bps."); return 0;
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly BinaryReader _reader;
        private readonly int _channels;
        private long _remaining;
        public Pcm16WaveReader(string path, int expectedRate, int expectedChannels) {
            _reader = new BinaryReader(File.OpenRead(path));
            if (new string(_reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not RIFF");
            _reader.ReadUInt32();
            if (new string(_reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not WAVE");
            int format = 0, channels = 0, rate = 0, bits = 0;
            while (_reader.BaseStream.Position + 8 <= _reader.BaseStream.Length) {
                string id = new string(_reader.ReadChars(4));
                int size = _reader.ReadInt32();
                long next = _reader.BaseStream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = _reader.ReadUInt16(); channels = _reader.ReadUInt16(); rate = _reader.ReadInt32();
                    _reader.ReadInt32(); _reader.ReadUInt16(); bits = _reader.ReadUInt16();
                } else if (id == "data") { _remaining = size; break; }
                _reader.BaseStream.Position = next;
            }
            if (format != 1 || bits != 16 || channels != expectedChannels || rate != expectedRate)
                throw new InvalidDataException($"Unexpected WAV format in '{path}'.");
            _channels = channels;
        }
        public int ReadFrames(Span<short> destination) {
            int count = 0;
            while (count < destination.Length && _remaining >= 2L * _channels) {
                destination[count++] = _reader.ReadInt16();
                for (int channel = 1; channel < _channels; channel++) _reader.ReadInt16();
                _remaining -= 2L * _channels;
            }
            return count;
        }
        public void Dispose() => _reader.Dispose();
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly BinaryWriter _writer;
        private readonly int _rate;
        private readonly short _channels;
        private long _samples;
        public Pcm16WaveWriter(string path, int rate, int channels) {
            _rate = rate; _channels = checked((short)channels); _writer = new BinaryWriter(File.Create(path));
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("RIFF")); _writer.Write(0);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); _writer.Write(16); _writer.Write((short)1);
            _writer.Write(_channels); _writer.Write(_rate); _writer.Write(_rate * _channels * 2);
            _writer.Write((short)(_channels * 2)); _writer.Write((short)16);
            _writer.Write(global::System.Text.Encoding.ASCII.GetBytes("data")); _writer.Write(0);
        }
        public void WriteFrames(ReadOnlySpan<short> samples) {
            foreach (short sample in samples) _writer.Write(sample);
            _samples += samples.Length;
        }
        public void Dispose() {
            long end = _writer.BaseStream.Position;
            _writer.BaseStream.Position = 4; _writer.Write(checked((int)(end - 8)));
            _writer.BaseStream.Position = 40; _writer.Write(checked((int)(_samples * 2)));
            _writer.BaseStream.Position = end; _writer.Dispose();
        }
    }

}
