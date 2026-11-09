/*
 * TKFaxEngine - managed C# port
 *
 * Godard.cs
 *
 * Combined port of:
 *   godard.h
 *   private/godard.h
 *   godard.c
 *
 * Godard symbol timing error detector.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2024 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

namespace TKFaxEngine;

/// <summary>
/// Descriptor for the Godard symbol timing error detector.
/// Managed equivalent of <c>godard_ted_descriptor_t</c>.
/// </summary>
public sealed class GodardTedDescriptor : IDisposable {
    private bool _disposed;

    public GodardTedDescriptor() {
    }

    public GodardTedDescriptor(
        float sampleRate,
        float baudRate,
        float carrierFrequency,
        float alpha,
        float coarseTrigger,
        float fineTrigger,
        int coarseStep,
        int fineStep) {
        Initialize(
            sampleRate,
            baudRate,
            carrierFrequency,
            alpha,
            coarseTrigger,
            fineTrigger,
            coarseStep,
            fineStep);
    }

    private GodardTedDescriptor(GodardTedDescriptor source) {
        source.ThrowIfDisposed();

        source.LowBandEdgeCoefficients.CopyTo(
            LowBandEdgeCoefficients,
            0);

        source.HighBandEdgeCoefficients.CopyTo(
            HighBandEdgeCoefficients,
            0);

        MixedBandEdgesCoefficient3 =
            source.MixedBandEdgesCoefficient3;

        CoarseTrigger = source.CoarseTrigger;
        FineTrigger = source.FineTrigger;
        CoarseStep = source.CoarseStep;
        FineStep = source.FineStep;
    }

    /// <summary>
    /// Low band-edge filter coefficients.
    /// </summary>
    public float[] LowBandEdgeCoefficients { get; } =
        new float[3];

    /// <summary>
    /// High band-edge filter coefficients.
    /// </summary>
    public float[] HighBandEdgeCoefficients { get; } =
        new float[3];

    /// <summary>
    /// Blended band-edge coefficient.
    /// </summary>
    public float MixedBandEdgesCoefficient3 { get; private set; }

    /// <summary>
    /// Error magnitude required to select a coarse correction step.
    /// </summary>
    public float CoarseTrigger { get; private set; }

    /// <summary>
    /// Error magnitude required to select a fine correction step.
    /// </summary>
    public float FineTrigger { get; private set; }

    /// <summary>
    /// Coarse baud-alignment correction step.
    /// </summary>
    public int CoarseStep { get; private set; }

    /// <summary>
    /// Fine baud-alignment correction step.
    /// </summary>
    public int FineStep { get; private set; }

    public bool IsDisposed => _disposed;

    /// <summary>
    /// Calculates the descriptor coefficients, corresponding to
    /// <c>godard_ted_make_descriptor()</c>.
    /// </summary>
    public void Initialize(
        float sampleRate,
        float baudRate,
        float carrierFrequency,
        float alpha,
        float coarseTrigger,
        float fineTrigger,
        int coarseStep,
        int fineStep) {
        float lowEdge =
            2.0f *
            MathF.PI *
            (carrierFrequency - baudRate / 2.0f) /
            sampleRate;

        float highEdge =
            2.0f *
            MathF.PI *
            (carrierFrequency + baudRate / 2.0f) /
            sampleRate;

        float alphaSquared = alpha * alpha;

        LowBandEdgeCoefficients[0] =
            2.0f * alpha * MathF.Cos(lowEdge);

        LowBandEdgeCoefficients[1] =
            -alphaSquared;

        LowBandEdgeCoefficients[2] =
            -alpha * MathF.Sin(lowEdge);

        HighBandEdgeCoefficients[0] =
            2.0f * alpha * MathF.Cos(highEdge);

        HighBandEdgeCoefficients[1] =
            -alphaSquared;

        HighBandEdgeCoefficients[2] =
            -alpha * MathF.Sin(highEdge);

        MixedBandEdgesCoefficient3 =
            -alphaSquared *
            (
                MathF.Sin(highEdge) * MathF.Cos(lowEdge) -
                MathF.Sin(lowEdge) * MathF.Cos(highEdge)
            );

        CoarseTrigger = coarseTrigger;
        FineTrigger = fineTrigger;
        CoarseStep = coarseStep;
        FineStep = fineStep;

        _disposed = false;
    }

    /// <summary>
    /// Produces the value-copy semantics used when the native state stores
    /// <c>s-&gt;desc = *desc</c>.
    /// </summary>
    public GodardTedDescriptor Clone() {
        return new GodardTedDescriptor(this);
    }

    public void Dispose() {
        if (_disposed)
            return;

        Array.Clear(LowBandEdgeCoefficients);
        Array.Clear(HighBandEdgeCoefficients);

        MixedBandEdgesCoefficient3 = 0.0f;
        CoarseTrigger = 0.0f;
        FineTrigger = 0.0f;
        CoarseStep = 0;
        FineStep = 0;

        _disposed = true;
    }

    internal void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(GodardTedDescriptor));
        }
    }
}

/// <summary>
/// Runtime state for the Godard symbol timing error detector.
/// Managed equivalent of <c>godard_ted_state_t</c>.
/// </summary>
public sealed class GodardTedState : IDisposable {
    private GodardTedDescriptor? _descriptor;
    private bool _disposed;

    /// <summary>
    /// Low Nyquist band-edge filter state.
    /// </summary>
    public float[] LowBandEdge { get; } =
        new float[2];

    /// <summary>
    /// High Nyquist band-edge filter state.
    /// </summary>
    public float[] HighBandEdge { get; } =
        new float[2];

    /// <summary>
    /// DC-removal filter state.
    /// </summary>
    public float[] DcFilter { get; } =
        new float[2];

    /// <summary>
    /// Integrated baud phase error.
    /// </summary>
    public float BaudPhase { get; private set; }

    /// <summary>
    /// Total symbol timing correction since initialization.
    /// </summary>
    public int TotalBaudTimingCorrection { get; private set; }

    public bool IsDisposed => _disposed;

    public GodardTedDescriptor Descriptor {
        get {
            ThrowIfDisposed();

            return _descriptor ??
                throw new InvalidOperationException(
                    "The Godard TED state has not been initialized.");
        }
    }

    public GodardTedState() {
    }

    public GodardTedState(
        GodardTedDescriptor descriptor) {
        Initialize(descriptor);
    }

    /// <summary>
    /// Initializes or resets the detector state, corresponding to
    /// <c>godard_ted_init()</c>.
    /// </summary>
    public void Initialize(
        GodardTedDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);
        descriptor.ThrowIfDisposed();

        _descriptor?.Dispose();
        _descriptor = descriptor.Clone();

        Array.Clear(LowBandEdge);
        Array.Clear(HighBandEdge);
        Array.Clear(DcFilter);

        BaudPhase = 0.0f;
        TotalBaudTimingCorrection = 0;

        _disposed = false;
    }

    /// <summary>
    /// Returns the accumulated timing correction.
    /// </summary>
    public int Correction() {
        ThrowIfDisposed();
        return TotalBaudTimingCorrection;
    }

    /// <summary>
    /// Processes one received sample through both symbol-sync band-edge
    /// filters. This corresponds to <c>godard_ted_rx()</c>.
    /// </summary>
    public void Receive(float sample) {
        GodardTedDescriptor descriptor =
            Descriptor;

        float lowValue =
            LowBandEdge[0] *
            descriptor.LowBandEdgeCoefficients[0] +
            LowBandEdge[1] *
            descriptor.LowBandEdgeCoefficients[1] +
            sample;

        LowBandEdge[1] = LowBandEdge[0];
        LowBandEdge[0] = lowValue;

        float highValue =
            HighBandEdge[0] *
            descriptor.HighBandEdgeCoefficients[0] +
            HighBandEdge[1] *
            descriptor.HighBandEdgeCoefficients[1] +
            sample;

        HighBandEdge[1] = HighBandEdge[0];
        HighBandEdge[0] = highValue;
    }

    /// <summary>
    /// Performs the once-per-baud timing-error update and returns the
    /// equalizer input-step correction.
    /// </summary>
    public int PerBaud() {
        GodardTedDescriptor descriptor =
            Descriptor;

        float correlation =
            LowBandEdge[1] *
            HighBandEdge[0] *
            descriptor.LowBandEdgeCoefficients[2] -
            LowBandEdge[0] *
            HighBandEdge[1] *
            descriptor.HighBandEdgeCoefficients[2] +
            LowBandEdge[1] *
            HighBandEdge[1] *
            descriptor.MixedBandEdgesCoefficient3;

        float dcRemoved =
            correlation -
            DcFilter[1];

        DcFilter[1] = DcFilter[0];
        DcFilter[0] = correlation;

        BaudPhase -= dcRemoved;

        float absolutePhase =
            MathF.Abs(BaudPhase);

        int correction = 0;

        if (absolutePhase > descriptor.FineTrigger) {
            correction =
                absolutePhase > descriptor.CoarseTrigger
                    ? descriptor.CoarseStep
                    : descriptor.FineStep;

            if (BaudPhase < 0.0f)
                correction = -correction;

            TotalBaudTimingCorrection =
                unchecked(
                    TotalBaudTimingCorrection +
                    correction);
        }

        return correction;
    }

    /// <summary>
    /// Matches <c>godard_ted_release()</c>. The native implementation has no
    /// release work.
    /// </summary>
    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _descriptor?.Dispose();
        _descriptor = null;

        Array.Clear(LowBandEdge);
        Array.Clear(HighBandEdge);
        Array.Clear(DcFilter);

        BaudPhase = 0.0f;
        TotalBaudTimingCorrection = 0;

        _disposed = true;
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(GodardTedState));
        }
    }
}

/// <summary>
/// Compatibility facade retaining the original C function names.
/// </summary>
public static class GodardTedApi {
    public static int godard_ted_correction(
        GodardTedState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Correction();
    }

    public static GodardTedDescriptor godard_ted_make_descriptor(
        GodardTedDescriptor? descriptor,
        float sampleRate,
        float baudRate,
        float carrierFrequency,
        float alpha,
        float coarseTrigger,
        float fineTrigger,
        int coarseStep,
        int fineStep) {
        descriptor ??=
            new GodardTedDescriptor();

        descriptor.Initialize(
            sampleRate,
            baudRate,
            carrierFrequency,
            alpha,
            coarseTrigger,
            fineTrigger,
            coarseStep,
            fineStep);

        return descriptor;
    }

    public static int godard_ted_free_descriptor(
        GodardTedDescriptor? descriptor) {
        descriptor?.Dispose();
        return 0;
    }

    public static void godard_ted_rx(
        GodardTedState state,
        float sample) {
        ArgumentNullException.ThrowIfNull(state);
        state.Receive(sample);
    }

    public static int godard_ted_per_baud(
        GodardTedState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.PerBaud();
    }

    public static GodardTedState godard_ted_init(
        GodardTedState? state,
        GodardTedDescriptor descriptor) {
        ArgumentNullException.ThrowIfNull(descriptor);

        state ??=
            new GodardTedState();

        state.Initialize(descriptor);
        return state;
    }

    public static int godard_ted_release(
        GodardTedState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int godard_ted_free(
        GodardTedState? state) {
        if (state is null)
            return 0;

        int result = state.Release();
        state.Dispose();
        return result;
    }
}
