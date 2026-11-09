#nullable enable

using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TKFaxEngine.Modem.V18;
using static TKFaxEngine.Modem.V18.V18Api;

namespace TKFaxEngineTest.Modem.Test;

/// <summary>
/// End-to-end V.18 regression suite. The engine remains unmodified; two public
/// V18State endpoints exchange real audio samples through a deterministic test channel.
/// </summary>
internal static class v18_complete_tests
{
    private const int SampleRate = 8000;
    private const int ChunkSamples = 160;
    private const int MaximumSecondsPerRun = 180;
    private const int TailSeconds = 8;
    private const double DefaultNoiseSnrDb = 18.0;
    private const string TiffFileName = "Fax_Testseite_A4_Fine_1bit_V0001.tif";

    private static readonly string[] NoiseFiles =
    {
        "audiocheck.net_violetnoise.wav",
        "audiocheck.net_whitenoise.wav",
        "audiocheck.net_whitenoisegaussian.wav",
        "audiocheck.net_bluenoise.wav",
        "audiocheck.net_brownnoise.wav",
        "audiocheck.net_pinknoise.wav"
    };

    private sealed class EndpointCapture
    {
        internal readonly List<byte> Bytes = new();
        internal readonly List<int> Statuses = new();

        internal void Put(ReadOnlySpan<byte> data)
        {
            for (int i = 0; i < data.Length; i++)
                Bytes.Add(data[i]);
        }
    }

    private sealed record NoiseData(string FileName, double OriginalRms, short[] Samples);

    private sealed record TransferResult(
        bool Passed,
        string Reason,
        int Chunks,
        int CallerMode,
        int AnswerMode,
        int CallerCarrierLosses,
        int AnswerCarrierLosses,
        int ClippedSamples,
        byte[] Received,
        double SignalRms,
        double NoiseRms,
        double AppliedNoiseGain);

    internal static int Main(string[] args)
    {
        string testFiles = Path.Combine(AppContext.BaseDirectory, "Test-Files");
        string noiseFolder = Path.Combine(testFiles, "Noises-test");
        string tiffPath = Path.Combine(testFiles, TiffFileName);

        Console.WriteLine("V.18 complete end-to-end regression test");
        Console.WriteLine($"Test-Files: {testFiles}");

        ValidateRequiredFile(tiffPath);
        foreach (string noiseFile in NoiseFiles)
            ValidateRequiredFile(Path.Combine(noiseFolder, noiseFile));

        List<NoiseData> noises = NoiseFiles
            .Select(name => LoadAndNormalizeNoise(Path.Combine(noiseFolder, name)))
            .ToList();

        int failures = 0;
        failures += RunApiContractTests();
        failures += RunFixedModeBaselineTests();
        failures += RunAutomodingBidirectionalTest();
        failures += RunCarrierLossRecoveryTest();
        failures += RunTiffBaselineTest(tiffPath);
        failures += RunNoiseTransferTests(tiffPath, noises, DefaultNoiseSnrDb);
        failures += RunNoiseImmunityTests(noises);

        Console.WriteLine();
        Console.WriteLine(failures == 0
            ? "V.18 COMPLETE TEST: PASS"
            : $"V.18 COMPLETE TEST: FAIL ({failures.ToString(CultureInfo.InvariantCulture)} failed groups)");
        return failures == 0 ? 0 : 2;
    }

    private static int RunApiContractTests()
    {
        Console.WriteLine("[API] lifecycle, mode and string mappings");
        V18State? state = v18_init(null, true, V18_MODE_V18TEXTPHONE, V18_AUTOMODING_NONE,
            static (_, _) => { }, null, static (_, _) => { }, null);
        if (state is null)
            return Fail("API", "v18_init returned null");

        bool ok = v18_get_current_mode(state) == V18_MODE_V18TEXTPHONE
            && !string.IsNullOrWhiteSpace(v18_mode_to_str(V18_MODE_V18TEXTPHONE))
            && !string.IsNullOrWhiteSpace(v18_status_to_str(V18_MODE_V18TEXTPHONE));
        v18_release(state);
        v18_free(state);
        return ok ? Pass("API") : Fail("API", "mode or status mapping mismatch");
    }

    private static int RunFixedModeBaselineTests()
    {
        Console.WriteLine("[BASELINE] fixed modes over real audio");
        (int Mode, string Name, string Payload)[] cases =
        {
            (V18_MODE_WEITBRECHT_5BIT_4545, "Baudot 45.45", "THE QUICK BROWN FOX 0123456789"),
            (V18_MODE_WEITBRECHT_5BIT_476, "Baudot 47.6", "THE QUICK BROWN FOX 0123456789"),
            (V18_MODE_WEITBRECHT_5BIT_50, "Baudot 50", "THE QUICK BROWN FOX 0123456789"),
            (V18_MODE_DTMF, "DTMF", "0123456789ABCD."),
            (V18_MODE_EDT, "EDT", "V18 EDT 0123456789"),
            (V18_MODE_BELL103, "Bell 103", "V18 BELL103 0123456789"),
            (V18_MODE_V23VIDEOTEX, "V.23", "V18 V23 0123456789"),
            (V18_MODE_V21TEXTPHONE, "V.21", "V18 V21 0123456789"),
            (V18_MODE_V18TEXTPHONE, "V.18/T.140", "V18 UTF8 ÄÖÜ € 0123456789")
        };

        int failures = 0;
        foreach ((int mode, string name, string payloadText) in cases)
        {
            byte[] payload = Encoding.UTF8.GetBytes(payloadText);
            TransferResult result = RunTransfer(mode, V18_AUTOMODING_NONE, payload, null, null, false);
            if (result.Passed)
                Pass($"BASELINE {name}");
            else
                failures += Fail($"BASELINE {name}", result.Reason);
        }
        return failures;
    }

    private static int RunAutomodingBidirectionalTest()
    {
        Console.WriteLine("[AUTOMODING] caller/answer negotiation and reverse text");
        byte[] forward = Encoding.UTF8.GetBytes("AUTOMODING CALLER TO ANSWER 0123456789");
        TransferResult first = RunTransfer(V18_MODE_V18TEXTPHONE, V18_AUTOMODING_GLOBAL, forward, null, null, false);
        if (!first.Passed)
            return Fail("AUTOMODING forward", first.Reason);

        byte[] reverse = Encoding.UTF8.GetBytes("AUTOMODING ANSWER TO CALLER 9876543210");
        TransferResult second = RunTransfer(V18_MODE_V18TEXTPHONE, V18_AUTOMODING_GLOBAL, reverse, null, null, true);
        if (!second.Passed)
            return Fail("AUTOMODING reverse", second.Reason);

        Pass("AUTOMODING bidirectional");
        return 0;
    }

    private static int RunCarrierLossRecoveryTest()
    {
        Console.WriteLine("[CARRIER] loss, fill-in and recovery");
        byte[] payload = Encoding.UTF8.GetBytes("CARRIER LOSS RECOVERY 0123456789 ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        TransferResult result = RunTransfer(
            V18_MODE_V21TEXTPHONE,
            V18_AUTOMODING_NONE,
            payload,
            null,
            static chunk => chunk >= 80 && chunk < 95,
            false);

        // The public V.18 status callback reports mode changes, not raw carrier
        // transitions. Recovery is therefore verified end-to-end by exact data
        // delivery after ReceiveFillIn has represented the missing audio interval.
        return result.Passed
            ? Pass("CARRIER loss/recovery")
            : Fail("CARRIER", result.Reason);
    }

    private static int RunTiffBaselineTest(string tiffPath)
    {
        Console.WriteLine("[TIFF] complete file transported as T.140-safe Base64 stream");
        byte[] source = File.ReadAllBytes(tiffPath);
        string envelope = BuildTiffEnvelope(source);
        byte[] payload = Encoding.ASCII.GetBytes(envelope);
        int maximumSeconds = Math.Max(900, payload.Length / 35 + 120);
        TransferResult result = RunTransfer(V18_MODE_V18TEXTPHONE, V18_AUTOMODING_NONE, payload, null, null, false, maximumSeconds);
        if (!result.Passed)
            return Fail("TIFF baseline", result.Reason);
        return VerifyTiffEnvelope(result.Received, source)
            ? Pass("TIFF baseline byte-for-byte")
            : Fail("TIFF baseline", "received envelope/hash does not reproduce source TIFF");
    }

    private static int RunNoiseTransferTests(string tiffPath, IReadOnlyList<NoiseData> noises, double snrDb)
    {
        Console.WriteLine($"[NOISE] normalized WAV mixed during transmission at {snrDb.ToString("F1", CultureInfo.InvariantCulture)} dB SNR");
        byte[] tiff = File.ReadAllBytes(tiffPath);
        // Use a deterministic TIFF-derived payload for every disturbance. The complete
        // TIFF is already checked in the baseline; this keeps six noise runs bounded.
        int probeLength = Math.Min(4096, tiff.Length);
        byte[] probe = tiff.AsSpan(0, probeLength).ToArray();
        byte[] payload = Encoding.ASCII.GetBytes(BuildTiffEnvelope(probe));

        int failures = 0;
        foreach (NoiseData noise in noises)
        {
            TransferResult result = RunTransfer(V18_MODE_V18TEXTPHONE, V18_AUTOMODING_NONE, payload, noise, null, false, 300, snrDb);
            string label = $"NOISE {noise.FileName}";
            Console.WriteLine($"    sourceRMS={noise.OriginalRms:F2} normalizedRMS={result.NoiseRms:F2} signalRMS={result.SignalRms:F2} gain={result.AppliedNoiseGain:F6} clipped={result.ClippedSamples}");
            if (!result.Passed)
            {
                failures += Fail(label, result.Reason);
                continue;
            }
            if (!VerifyTiffEnvelope(result.Received, probe))
            {
                failures += Fail(label, "payload/hash mismatch under noise");
                continue;
            }
            Pass(label);
        }
        return failures;
    }

    private static int RunNoiseImmunityTests(IReadOnlyList<NoiseData> noises)
    {
        Console.WriteLine("[IMMUNITY] noise-only audio must not create text or a textphone mode");
        int failures = 0;
        foreach (NoiseData noise in noises)
        {
            EndpointCapture capture = new();
            V18State? receiver = v18_init(null, false, V18_MODE_NONE, V18_AUTOMODING_GLOBAL,
                static (u, m) => ((EndpointCapture)u!).Put(m), capture,
                static (u, s) => ((EndpointCapture)u!).Statuses.Add(s), capture);
            if (receiver is null)
            {
                failures += Fail($"IMMUNITY {noise.FileName}", "receiver init failed");
                continue;
            }

            short[] block = new short[ChunkSamples];
            int position = 0;
            for (int chunk = 0; chunk < CHUNKS_PER_SECOND * 12; chunk++)
            {
                for (int i = 0; i < block.Length; i++)
                {
                    block[i] = noise.Samples[position++ % noise.Samples.Length];
                }
                v18_rx(receiver, block, block.Length);
            }

            bool falseText = capture.Bytes.Any(b => b is not 0 and not 0xEF and not 0xBB and not 0xBF);
            bool falseMode = v18_get_current_mode(receiver) != V18_MODE_NONE;
            v18_free(receiver);

            if (falseText || falseMode)
                failures += Fail($"IMMUNITY {noise.FileName}", $"falseText={falseText} falseMode={falseMode}");
            else
                Pass($"IMMUNITY {noise.FileName}");
        }
        return failures;
    }

    private static TransferResult RunTransfer(
        int mode,
        int automoding,
        byte[] payload,
        NoiseData? noise,
        Func<int, bool>? dropChunk,
        bool reverse,
        int maximumSeconds = MaximumSecondsPerRun,
        double snrDb = DefaultNoiseSnrDb)
    {
        EndpointCapture callerCapture = new();
        EndpointCapture answerCapture = new();

        V18State? caller = v18_init(null, true, mode, automoding,
            static (u, m) => ((EndpointCapture)u!).Put(m), callerCapture,
            static (u, s) => ((EndpointCapture)u!).Statuses.Add(s), callerCapture);
        V18State? answer = v18_init(null, false, mode, automoding,
            static (u, m) => ((EndpointCapture)u!).Put(m), answerCapture,
            static (u, s) => ((EndpointCapture)u!).Statuses.Add(s), answerCapture);

        if (caller is null || answer is null)
            return new(false, "v18_init failed", 0, -1, -1, 0, 0, 0, Array.Empty<byte>(), 0, 0, 0);

        V18State sender = reverse ? answer : caller;
        EndpointCapture receiverCapture = reverse ? callerCapture : answerCapture;
        bool queued = false;
        int queuedInputBytes = 0;
        bool asciiPayload = payload.All(static b => b < 0x80);
        byte[] transmittedPayload = asciiPayload && mode == V18_MODE_V18TEXTPHONE
            ? AppendDrainMarker(payload)
            : payload;
        int quietTail = 0;
        int clipped = 0;
        int noisePosition = 0;
        double signalSquare = 0;
        long signalCount = 0;
        double noiseSquare = 0;
        long noiseCount = 0;
        double lastNoiseGain = 0;
        short[] callerTx = new short[ChunkSamples];
        short[] answerTx = new short[ChunkSamples];
        short[] callerToAnswer = new short[ChunkSamples];
        short[] answerToCaller = new short[ChunkSamples];

        int maxChunks = maximumSeconds * CHUNKS_PER_SECOND;
        int chunk;
        for (chunk = 0; chunk < maxChunks; chunk++)
        {
            Array.Clear(callerTx);
            Array.Clear(answerTx);
            int callerSamples = v18_tx(caller, callerTx, callerTx.Length);
            int answerSamples = v18_tx(answer, answerTx, answerTx.Length);
            Pad(callerTx, callerSamples);
            Pad(answerTx, answerSamples);

            bool modeReady = automoding == V18_AUTOMODING_NONE
                || (v18_get_current_mode(caller) != V18_MODE_NONE
                    && v18_get_current_mode(answer) != V18_MODE_NONE);
            if (!queued && chunk >= CHUNKS_PER_SECOND * 3 && modeReady)
            {
                if (asciiPayload)
                {
                    // ASCII is fed one complete code element at a time. This keeps the
                    // test streaming-based and avoids assuming internal queue capacity.
                    int accepted = v18_put(sender, transmittedPayload.AsSpan(queuedInputBytes, 1));
                    if (accepted > 0)
                        queuedInputBytes++;
                    queued = queuedInputBytes == transmittedPayload.Length;
                }
                else
                {
                    // The only non-ASCII regression payload is small and must be
                    // accepted as one UTF-8 sequence so split multibyte characters are
                    // exercised by the engine's T.140 input path, not duplicated here.
                    queued = v18_put(sender, transmittedPayload) > 0;
                    if (queued)
                        queuedInputBytes = transmittedPayload.Length;
                }
            }

            MixChannel(callerTx, callerToAnswer, noise, snrDb, ref noisePosition,
                dropChunk?.Invoke(chunk) == true, ref clipped, ref signalSquare,
                ref signalCount, ref noiseSquare, ref noiseCount, ref lastNoiseGain);
            MixChannel(answerTx, answerToCaller, noise, snrDb, ref noisePosition,
                dropChunk?.Invoke(chunk) == true, ref clipped, ref signalSquare,
                ref signalCount, ref noiseSquare, ref noiseCount, ref lastNoiseGain);

            if (dropChunk?.Invoke(chunk) == true)
            {
                v18_rx_fillin(answer, ChunkSamples);
                v18_rx_fillin(caller, ChunkSamples);
            }
            else
            {
                v18_rx(answer, callerToAnswer, ChunkSamples);
                v18_rx(caller, answerToCaller, ChunkSamples);
            }

            if (queued && ContainsExactPayload(receiverCapture.Bytes, payload))
            {
                quietTail++;
                if (quietTail >= TailSeconds * CHUNKS_PER_SECOND)
                    break;
            }
            else
            {
                quietTail = 0;
            }
        }

        byte[] received = StripLeadingUtf8Bom(receiverCapture.Bytes.ToArray());
        bool exact = ContainsExactPayload(received, payload);
        string reason = exact
            ? "OK"
            : BuildPayloadMismatchReason(payload, received, queued, chunk);

        int callerLosses = CountCarrierLosses(callerCapture.Statuses);
        int answerLosses = CountCarrierLosses(answerCapture.Statuses);
        int callerMode = v18_get_current_mode(caller);
        int answerMode = v18_get_current_mode(answer);
        v18_free(caller);
        v18_free(answer);

        return new(
            exact,
            reason,
            chunk,
            callerMode,
            answerMode,
            callerLosses,
            answerLosses,
            clipped,
            received,
            signalCount == 0 ? 0 : Math.Sqrt(signalSquare / signalCount),
            noiseCount == 0 ? 0 : Math.Sqrt(noiseSquare / noiseCount),
            lastNoiseGain);
    }

    private static void MixChannel(
        short[] signal,
        short[] destination,
        NoiseData? noise,
        double snrDb,
        ref int noisePosition,
        bool drop,
        ref int clipped,
        ref double signalSquare,
        ref long signalCount,
        ref double noiseSquare,
        ref long noiseCount,
        ref double lastNoiseGain)
    {
        if (drop)
        {
            Array.Clear(destination);
            return;
        }

        double blockSignalRms = CalculateRms(signal);
        double gain = 0;
        if (noise is not null && blockSignalRms > 1.0)
        {
            double targetNoiseRms = blockSignalRms / Math.Pow(10.0, snrDb / 20.0);
            gain = targetNoiseRms / 10000.0; // normalized WAV RMS is 10000.
            lastNoiseGain = gain;
        }

        for (int i = 0; i < signal.Length; i++)
        {
            double noiseSample = 0;
            if (noise is not null)
            {
                noiseSample = noise.Samples[noisePosition++ % noise.Samples.Length] * gain;
                noiseSquare += noiseSample * noiseSample;
                noiseCount++;
            }

            signalSquare += (double)signal[i] * signal[i];
            signalCount++;
            int mixed = (int)Math.Round(signal[i] + noiseSample, MidpointRounding.AwayFromZero);
            if (mixed > short.MaxValue)
            {
                mixed = short.MaxValue;
                clipped++;
            }
            else if (mixed < short.MinValue)
            {
                mixed = short.MinValue;
                clipped++;
            }
            destination[i] = (short)mixed;
        }
    }

    private static NoiseData LoadAndNormalizeNoise(string path)
    {
        short[] source = ReadWaveMono8k(path);
        if (source.Length == 0)
            throw new InvalidDataException($"Noise WAV contains no samples: {path}");

        double mean = source.Average(sample => (double)sample);
        double originalRms = Math.Sqrt(source.Select(sample => (sample - mean) * (sample - mean)).Average());
        if (originalRms < 1.0)
            throw new InvalidDataException($"Noise WAV RMS is zero: {path}");

        const double targetRms = 10000.0;
        double gain = targetRms / originalRms;
        short[] normalized = new short[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            int value = (int)Math.Round((source[i] - mean) * gain, MidpointRounding.AwayFromZero);
            normalized[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }

        Console.WriteLine($"    WAV {Path.GetFileName(path)}: samples={normalized.Length} originalRMS={originalRms:F2} normalizationGain={gain:F6}");
        return new(Path.GetFileName(path), originalRms, normalized);
    }

    private static short[] ReadWaveMono8k(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using BinaryReader reader = new(stream, Encoding.ASCII, leaveOpen: false);
        if (reader.ReadUInt32() != 0x46464952 || reader.ReadUInt32() < 4 || reader.ReadUInt32() != 0x45564157)
            throw new InvalidDataException($"Not a RIFF/WAVE file: {path}");

        ushort format = 0;
        ushort channels = 0;
        int sampleRate = 0;
        ushort bits = 0;
        byte[]? data = null;

        while (stream.Position + 8 <= stream.Length)
        {
            uint id = reader.ReadUInt32();
            int size = reader.ReadInt32();
            long next = stream.Position + size + (size & 1);
            if (size < 0 || next > stream.Length + 1)
                throw new InvalidDataException($"Invalid WAV chunk in {path}");

            if (id == 0x20746D66) // "fmt "
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadInt32();
                _ = reader.ReadUInt16();
                bits = reader.ReadUInt16();
            }
            else if (id == 0x61746164) // "data"
            {
                data = reader.ReadBytes(size);
            }
            stream.Position = Math.Min(next, stream.Length);
        }

        if (format != 1 || channels is < 1 or > 2 || bits != 16 || sampleRate <= 0 || data is null)
            throw new InvalidDataException($"Only 16-bit PCM mono/stereo WAV is supported: {path}");

        int frames = data.Length / (channels * 2);
        short[] mono = new short[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            int offset = frame * channels * 2;
            int sum = 0;
            for (int channel = 0; channel < channels; channel++)
                sum += BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + channel * 2, 2));
            mono[frame] = (short)(sum / channels);
        }

        if (sampleRate == SampleRate)
            return mono;

        int outputLength = checked((int)Math.Round((double)mono.Length * SampleRate / sampleRate));
        short[] resampled = new short[outputLength];
        for (int i = 0; i < outputLength; i++)
        {
            double sourcePosition = (double)i * sampleRate / SampleRate;
            int left = Math.Min((int)sourcePosition, mono.Length - 1);
            int right = Math.Min(left + 1, mono.Length - 1);
            double fraction = sourcePosition - left;
            resampled[i] = (short)Math.Round(mono[left] + (mono[right] - mono[left]) * fraction);
        }
        return resampled;
    }

    private static string BuildTiffEnvelope(byte[] source)
    {
        string hash = Convert.ToHexString(SHA256.HashData(source));
        return $"TKFAX-V18-TIFF|{source.Length}|{hash}|{Convert.ToBase64String(source)}|END";
    }

    private static bool VerifyTiffEnvelope(byte[] received, byte[] expected)
    {
        string text = Encoding.ASCII.GetString(received);
        int start = text.IndexOf("TKFAX-V18-TIFF|", StringComparison.Ordinal);
        int end = text.IndexOf("|END", start < 0 ? 0 : start, StringComparison.Ordinal);
        if (start < 0 || end < 0)
            return false;

        string[] fields = text[start..end].Split('|');
        if (fields.Length != 4 || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int length))
            return false;
        try
        {
            byte[] decoded = Convert.FromBase64String(fields[3]);
            string hash = Convert.ToHexString(SHA256.HashData(decoded));
            return length == expected.Length
                && decoded.AsSpan().SequenceEqual(expected)
                && string.Equals(hash, fields[2], StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool ContainsExactPayload(IReadOnlyList<byte> received, ReadOnlySpan<byte> payload)
    {
        if (payload.Length == 0)
            return true;
        if (received.Count < payload.Length)
            return false;
        for (int start = 0; start <= received.Count - payload.Length; start++)
        {
            bool equal = true;
            for (int i = 0; i < payload.Length; i++)
            {
                if (received[start + i] != payload[i])
                {
                    equal = false;
                    break;
                }
            }
            if (equal)
                return true;
        }
        return false;
    }


    private static byte[] AppendDrainMarker(ReadOnlySpan<byte> payload)
    {
        byte[] framed = new byte[payload.Length + 1];
        payload.CopyTo(framed);
        // The marker is deliberately outside the compared payload. It provides the
        // following start edge needed to complete delivery of the final asynchronous
        // character through the real V.18 audio/decoder path.
        framed[^1] = (byte)'~';
        return framed;
    }

    private static string BuildPayloadMismatchReason(
        ReadOnlySpan<byte> sent,
        ReadOnlySpan<byte> received,
        bool queued,
        int chunks)
    {
        int common = Math.Min(sent.Length, received.Length);
        int firstDifference = 0;
        while (firstDifference < common && sent[firstDifference] == received[firstDifference])
            firstDifference++;

        if (firstDifference == common && sent.Length == received.Length)
            firstDifference = -1;

        string difference;
        if (firstDifference < 0) {
            difference = "none";
        } else if (firstDifference < common) {
            difference = $"offset={firstDifference} expected=0x{sent[firstDifference]:X2} actual=0x{received[firstDifference]:X2}";
        } else if (sent.Length > received.Length) {
            difference = $"offset={firstDifference} expected=0x{sent[firstDifference]:X2} actual=<missing>";
        } else {
            difference = $"offset={firstDifference} expected=<end> actual=0x{received[firstDifference]:X2}";
        }

        return $"payload mismatch queued={queued} sent={sent.Length} received={received.Length} chunks={chunks} firstDifference={difference}";
    }

    private static byte[] StripLeadingUtf8Bom(byte[] data)
    {
        int offset = 0;
        while (offset + 2 < data.Length && data[offset] == 0xEF && data[offset + 1] == 0xBB && data[offset + 2] == 0xBF)
            offset += 3;
        return offset == 0 ? data : data[offset..];
    }

    private static int CountCarrierLosses(IEnumerable<int> statuses)
        => statuses.Count(status => status < 0 && v18_status_to_str(status).Contains("carrier", StringComparison.OrdinalIgnoreCase));

    private static double CalculateRms(ReadOnlySpan<short> samples)
    {
        if (samples.Length == 0)
            return 0;
        double sum = 0;
        for (int i = 0; i < samples.Length; i++)
            sum += (double)samples[i] * samples[i];
        return Math.Sqrt(sum / samples.Length);
    }

    private static void Pad(short[] samples, int count)
    {
        if (count < 0 || count > samples.Length)
            throw new InvalidOperationException($"V.18 transmitter returned invalid sample count {count}");
        if (count < samples.Length)
            Array.Clear(samples, count, samples.Length - count);
    }

    private static void ValidateRequiredFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required V.18 test file not found below executable Test-Files folder: {path}", path);
    }

    private static int Pass(string name)
    {
        Console.WriteLine($"    PASS {name}");
        return 0;
    }

    private static int Fail(string name, string reason)
    {
        Console.Error.WriteLine($"    FAIL {name}: {reason}");
        return 1;
    }

    private const int CHUNKS_PER_SECOND = SampleRate / ChunkSamples;
    private const int V18_AUTOMODING_GLOBAL = 0;
    private const int V18_AUTOMODING_NONE = 1;
}
