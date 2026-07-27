/*
 * TKFaxEngine common QAM callback types.
 *
 * Managed C# port of qam.h. The original header was extracted from the
 * V.29 receiver so the V.17/V.27ter/V.29/V.32/V.34 modem sources can share
 * one QAM reporting callback definition.
 */

#nullable enable

namespace TKFaxEngine;

/// <summary>
/// Floating-point complex value used by QAM constellation reports.
/// </summary>
public readonly record struct QamComplexF(float Real, float Imaginary) {
    public float Re => Real;

    public float Im => Imaginary;
}

/// <summary>
/// Signed 16-bit fixed-point complex value used by QAM constellation reports.
/// </summary>
public readonly record struct QamComplexI16(short Real, short Imaginary) {
    public short Re => Real;

    public short Im => Imaginary;
}

/// <summary>
/// Floating-point QAM report callback. <paramref name="constellation"/> or
/// <paramref name="target"/> may be null for timing-only status reports.
/// </summary>
public delegate void QamReportHandler(
    object? userData,
    QamComplexF? constellation,
    QamComplexF? target,
    int symbol);

/// <summary>
/// Fixed-point QAM report callback. <paramref name="constellation"/> or
/// <paramref name="target"/> may be null for timing-only status reports.
/// </summary>
public delegate void QamFixedReportHandler(
    object? userData,
    QamComplexI16? constellation,
    QamComplexI16? target,
    int symbol);

/// <summary>
/// Native-name-compatible callback type. Its signature follows the same
/// TKFAXENGINE_USE_FIXED_POINT conditional selection as the C header.
/// </summary>
#if TKFAXENGINE_USE_FIXED_POINT
public delegate void qam_report_handler_t(
    object? userData,
    QamComplexI16? constel,
    QamComplexI16? target,
    int symbol);
#else
public delegate void qam_report_handler_t(
    object? userData,
    QamComplexF? constel,
    QamComplexF? target,
    int symbol);
#endif