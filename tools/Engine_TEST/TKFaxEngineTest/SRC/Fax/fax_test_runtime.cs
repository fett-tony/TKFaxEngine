/*
 * TKFaxEngineTest - direct runtime binding for fax_tests and TSB-85.
 * Current TKFaxEngine states own their T.30, modem, T.38 and T.4/T.6 modules.
 */

#nullable enable

using TKFaxEngineTest.Network.Test;

namespace TKFaxEngineTest.Fax.Test;

internal static class FaxTestRuntimeBootstrap {
    private static readonly object Sync = new();
    private static bool configured;

    public static void EnsureConfigured() {
        if (configured)
            return;

        lock (Sync) {
            if (configured)
                return;

            var runtime = new ManagedFaxTestsRuntime();
            fax_tests.Runtime = runtime;
            Tsb85Tests.Runtime = runtime;
            configured = true;
        }
    }
}

internal sealed class ManagedFaxTestsRuntime : IFaxTestsRuntime {
    public IFaxTestsG1050Path CreateG1050Path(
        int modelNumber,
        int speedPatternNumber,
        int packetSize,
        int packetsPerSecond) =>
        new ManagedFaxG1050Path(
            modelNumber,
            speedPatternNumber,
            packetSize,
            packetsPerSecond);
}

internal sealed class ManagedFaxG1050Path : IFaxTestsG1050Path {
    private G1050State? state;

    public ManagedFaxG1050Path(
        int modelNumber,
        int speedPatternNumber,
        int packetSize,
        int packetsPerSecond) {
        state = G1050.g1050_init(
            modelNumber,
            speedPatternNumber,
            packetSize,
            packetsPerSecond);
    }

    public int Put(ReadOnlySpan<byte> packet, int sequenceNumber, double departureTime) {
        G1050State current = RequireState();
        byte[] copy = packet.ToArray();
        return G1050.g1050_put(current, copy, copy.Length, sequenceNumber, departureTime);
    }

    public bool TryGet(
        double currentTime,
        Span<byte> destination,
        out int length,
        out int sequenceNumber,
        out double departureTime,
        out double arrivalTime) {
        G1050State current = RequireState();
        byte[] buffer = new byte[destination.Length];
        length = G1050.g1050_get(
            current,
            buffer,
            buffer.Length,
            currentTime,
            out sequenceNumber,
            out departureTime,
            out arrivalTime);
        if (length < 0)
            return false;
        buffer.AsSpan(0, Math.Min(length, destination.Length)).CopyTo(destination);
        return true;
    }

    public void Dispose() {
        if (state is null)
            return;
        _ = G1050.g1050_free(state);
        state = null;
    }

    private G1050State RequireState() =>
        state ?? throw new ObjectDisposedException(nameof(ManagedFaxG1050Path));
}
