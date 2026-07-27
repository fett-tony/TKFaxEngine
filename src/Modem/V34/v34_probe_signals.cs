/*
 * TKFaxEngine - direct C# conversion of the TKFaxEngineFX/spanDSP V.34 sources.
 * Direct translation of v34_probe_signals.h.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2009 Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.Modem.V34;

public readonly struct line_probe_t {
    public readonly int phase_rate;
    public readonly int starting_phase;

    public line_probe_t(int phase_rate, int starting_phase) {
        this.phase_rate = phase_rate;
        this.starting_phase = starting_phase;
    }
}

public static partial class v34 {
    internal const int LINE_PROBE_SAMPLES = 160;
    internal const int PP_REPEATS = 6;
    internal const int PP_SYMBOLS = 48;
    internal const int PPH_REPEATS = 4;
    internal const int PPH_SYMBOLS = 32;

    internal static readonly line_probe_t[] line_probe = new line_probe_t[]
    {
        new(unchecked((int)0x04CCCCCC), unchecked((int)0x00000000)),
        new(unchecked((int)0x09999999), unchecked((int)0x80000000)),
        new(unchecked((int)0x0E666666), unchecked((int)0x00000000)),
        new(unchecked((int)0x13333333), unchecked((int)0x00000000)),
        new(unchecked((int)0x18000000), unchecked((int)0x00000000)),
        new(unchecked((int)0x21999999), unchecked((int)0x00000000)),
        new(unchecked((int)0x2B333333), unchecked((int)0x00000000)),
        new(unchecked((int)0x30000000), unchecked((int)0x00000000)),
        new(unchecked((int)0x34CCCCCC), unchecked((int)0x80000000)),
        new(unchecked((int)0x3E666666), unchecked((int)0x00000000)),
        new(unchecked((int)0x43333333), unchecked((int)0x00000000)),
        new(unchecked((int)0x48000000), unchecked((int)0x80000000)),
        new(unchecked((int)0x51999999), unchecked((int)0x00000000)),
        new(unchecked((int)0x56666666), unchecked((int)0x80000000)),
        new(unchecked((int)0x5B333333), unchecked((int)0x00000000)),
        new(unchecked((int)0x60000000), unchecked((int)0x80000000)),
        new(unchecked((int)0x64CCCCCC), unchecked((int)0x80000000)),
        new(unchecked((int)0x69999999), unchecked((int)0x80000000)),
        new(unchecked((int)0x6E666666), unchecked((int)0x80000000)),
        new(unchecked((int)0x73333333), unchecked((int)0x00000000)),
        new(unchecked((int)0x78000000), unchecked((int)0x00000000)),
    };
    internal static readonly float[] line_probe_samples = new float[]
    {
        24903.28f, 24514.95f, 1310.90f, -16258.72f, 9817.84f, -24025.86f, 8281.32f, -6871.09f,
        -1708.98f, 7508.05f, -21770.92f, 22231.72f, 15049.83f, 19801.68f, -16424.84f, 5252.57f,
        22274.17f, 14530.10f, -19975.32f, -3076.90f, 1458.80f, -23447.22f, 19443.63f, 2000.17f,
        -22319.84f, -8546.17f, -17045.09f, -22641.13f, -7647.30f, -18923.08f, -11525.04f, 22730.19f,
        -11137.09f, -13550.21f, 8957.77f, -23987.32f, 5328.12f, 20795.55f, 16732.44f, -17855.91f,
        9961.31f, 17258.87f, 22939.52f, -6971.65f, -10695.69f, 13711.92f, -18119.58f, 15541.35f,
        -22274.17f, 1702.23f, -5479.98f, -11407.13f, 21227.27f, 25013.07f, 25168.02f, 13430.54f,
        -18701.18f, 6183.88f, -13160.21f, -8287.47f, 8502.51f, -15974.55f, 18099.42f, -23453.54f,
        11137.09f, 18037.90f, 21093.02f, -4873.47f, -8707.18f, 22003.29f, 18853.32f, -7950.18f,
        -17037.87f, 11000.25f, -24223.48f, 6126.40f, 15472.35f, -20246.24f, -13154.89f, -11026.84f,
        -24903.28f, -11026.84f, -13154.89f, -20246.24f, 15472.35f, 6126.40f, -24223.48f, 11000.25f,
        -17037.87f, -7950.18f, 18853.32f, 22003.29f, -8707.18f, -4873.47f, 21093.02f, 18037.90f,
        11137.09f, -23453.54f, 18099.42f, -15974.55f, 8502.51f, -8287.47f, -13160.21f, 6183.88f,
        -18701.18f, 13430.54f, 25168.02f, 25013.07f, 21227.27f, -11407.13f, -5479.98f, 1702.23f,
        -22274.17f, 15541.35f, -18119.58f, 13711.92f, -10695.69f, -6971.65f, 22939.52f, 17258.87f,
        9961.31f, -17855.91f, 16732.44f, 20795.55f, 5328.12f, -23987.32f, 8957.77f, -13550.21f,
        -11137.09f, 22730.19f, -11525.04f, -18923.08f, -7647.30f, -22641.13f, -17045.09f, -8546.17f,
        -22319.84f, 2000.17f, 19443.63f, -23447.22f, 1458.80f, -3076.90f, -19975.32f, 14530.10f,
        22274.17f, 5252.57f, -16424.84f, 19801.68f, 15049.83f, 22231.72f, -21770.92f, 7508.05f,
        -1708.98f, -6871.09f, 8281.32f, -24025.86f, 9817.84f, -16258.72f, 1310.90f, 24514.95f,
    };
    internal static readonly complexf_t[] pp_symbols = new complexf_t[]
    {
        new(1.0000000f, 0.0000000f),
        new(1.0000000f, 0.0000000f),
        new(1.0000000f, 0.0000000f),
        new(1.0000000f, 0.0000000f),
        new(-0.5000000f, 0.8660254f),
        new(-0.8660254f, 0.5000000f),
        new(-1.0000000f, 0.0000000f),
        new(-0.8660254f, -0.5000000f),
        new(1.0000000f, 0.0000000f),
        new(0.5000000f, 0.8660254f),
        new(-0.5000000f, 0.8660254f),
        new(-1.0000000f, 0.0000000f),
        new(1.0000000f, 0.0000000f),
        new(0.0000000f, 1.0000000f),
        new(-1.0000000f, 0.0000000f),
        new(-0.0000000f, -1.0000000f),
        new(-0.5000000f, 0.8660254f),
        new(-0.5000000f, -0.8660254f),
        new(1.0000000f, -0.0000000f),
        new(-0.5000000f, 0.8660254f),
        new(1.0000000f, 0.0000000f),
        new(-0.8660254f, 0.5000000f),
        new(0.5000000f, -0.8660254f),
        new(0.0000000f, 1.0000000f),
        new(1.0000000f, 0.0000000f),
        new(-1.0000000f, 0.0000000f),
        new(1.0000000f, -0.0000000f),
        new(-1.0000000f, 0.0000000f),
        new(-0.5000000f, 0.8660254f),
        new(0.8660254f, -0.5000000f),
        new(-1.0000000f, 0.0000000f),
        new(0.8660254f, 0.5000000f),
        new(1.0000000f, 0.0000000f),
        new(-0.5000000f, -0.8660254f),
        new(-0.5000000f, 0.8660254f),
        new(1.0000000f, -0.0000000f),
        new(1.0000000f, 0.0000000f),
        new(-0.0000000f, -1.0000000f),
        new(-1.0000000f, 0.0000000f),
        new(0.0000000f, 1.0000000f),
        new(-0.5000000f, 0.8660254f),
        new(0.5000000f, 0.8660254f),
        new(1.0000000f, -0.0000000f),
        new(0.5000000f, -0.8660254f),
        new(1.0000000f, 0.0000000f),
        new(0.8660254f, -0.5000000f),
        new(0.5000000f, -0.8660254f),
        new(-0.0000000f, -1.0000000f),
    };
    internal static readonly complexf_t[] pph_symbols = new complexf_t[]
    {
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
        new(-0.7071068f, 0.7071068f),
        new(-0.7071068f, -0.7071068f),
    };
}
