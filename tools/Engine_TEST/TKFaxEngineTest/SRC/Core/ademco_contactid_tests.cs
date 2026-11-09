/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * ademco_contactid_tests.cs - Ademco Contact ID alarm protocol tests.
 *
 * Original C implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2012 Steve Underwood
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License version 2.1,
 * as published by the Free Software Foundation.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser
 * General Public License for more details.
 *
 * Managed C# port. The supplied TKFaxEngine snapshot has no managed
 * ademco_contactid production module, so the exact test-required Contact ID
 * codec and sender/receiver state flow are contained in this test source.
 * DTMF, DDS, G.711 A-law and AWGN use TKFaxEngine's managed DSP classes.
 */

#nullable enable

using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using static TKFaxEngine.Audio.Dds;
using static TKFaxEngine.Audio.Dtmf;

namespace TKFaxEngineTest.Core.Test;

public static class ademco_contactid_tests {
    private const int SAMPLE_RATE = 8000;
    private const int SAMPLES_PER_CHUNK = 160;
    private const string OUTPUT_FILE_NAME = "ademco_contactid.wav";
    private const string MITEL_DIR = "Test-Files/mitel/";
    private const string BELLCORE_DIR = "Test-Files/bellcore/";

    private static readonly string[] bellcore_files =
    {
        MITEL_DIR + "mitel-cm7291-talkoff.wav",
        BELLCORE_DIR + "tr-tsy-00763-1.wav",
        BELLCORE_DIR + "tr-tsy-00763-2.wav",
        BELLCORE_DIR + "tr-tsy-00763-3.wav",
        BELLCORE_DIR + "tr-tsy-00763-4.wav",
        BELLCORE_DIR + "tr-tsy-00763-5.wav",
        BELLCORE_DIR + "tr-tsy-00763-6.wav",
        string.Empty
    };

    private static readonly ContactIdReport[] reports =
    {
        new(0x1234, 0x18, 0x1, 0x131, 0x1, 0x15),
        new(0x1234, 0x18, 0x3, 0x131, 0x1, 0x15),
        new(0x1234, 0x18, 0x1, 0x401, 0x2, 0x3),
        new(0x1234, 0x18, 0x3, 0x401, 0x3, 0x5),
        new(0x1234, 0x56, 0x7, 0x890, 0xBC, 0xDEF),
        new(0x1234, 0x56, 0x7, 0x89A, 0xBC, 0xDEF)
    };

    private static int reports_entry;
    private static bool tx_callback_reported;
    private static bool rx_callback_reported;
    private static bool sending_complete;

    public static int main(string[] testArguments) {
        try {
            return tkfx_ademco_contactid_tests_body_main(testArguments);
        } catch (AdemcoTestExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_ademco_contactid_tests_body_main(string[] testArguments) {
        string? decode_test_file = null;
        for (int index = 0; index < testArguments.Length; index++) {
            switch (testArguments[index]) {
                case "-d":
                    if (++index >= testArguments.Length)
                        Exit(2);
                    decode_test_file = testArguments[index];
                    break;
                default:
                    Exit(2);
                    break;
            }
        }

        if (decode_test_file is not null) {
            decode_file(decode_test_file);
            return 0;
        }

        if (encode_decode_tests() != 0) {
            Console.WriteLine("Tests failed");
            return 2;
        }
        if (mitel_cm7291_side_2_and_bellcore_tests() != 0) {
            Console.WriteLine("Tests failed");
            return 2;
        }
        if (end_to_end_tests() != 0) {
            Console.WriteLine("Tests failed");
            return 2;
        }

        Console.WriteLine("Tests passed");
        return 0;
    }

    private static void talkoff_tx_callback(object? userData, int tone, int level, int duration) {
        _ = userData;
        _ = level;
        _ = duration;
        Console.WriteLine($"Ademco sender report {tone}");
        tx_callback_reported = true;
    }

    private static int mitel_cm7291_side_2_and_bellcore_tests() {
        Console.WriteLine("Talk-off test");
        using var sender = new ContactIdSender(talkoff_tx_callback, null);
        tx_callback_reported = false;
        short[] amp = new short[SAMPLE_RATE];

        for (int j = 0; j < bellcore_files.Length && bellcore_files[j].Length != 0; j++) {
            string file = bellcore_files[j];
            try {
                using var inhandle = Pcm16WaveReader.OpenMono(file);
                while (true) {
                    int frames = inhandle.ReadFrames(amp, SAMPLE_RATE);
                    if (frames == 0)
                        break;
                    sender.Receive(amp.AsSpan(0, frames));
                }
            } catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) {
                Console.WriteLine($"    Cannot open speech file '{file}'");
                return -1;
            }
            Console.WriteLine($"    File {j + 1} gave 0 false hits.");
        }

        if (tx_callback_reported) {
            Console.WriteLine("    Failed");
            return -1;
        }
        Console.WriteLine("    Passed");
        return 0;
    }

    private static void rx_callback(object? userData, ContactIdReport report) {
        _ = userData;
        Console.WriteLine("Ademco Contact ID message:");
        Console.WriteLine($"    Account {report.Account:X}");
        Console.WriteLine($"    Message type {report.MessageType:X}");
        Console.WriteLine($"    Qualifier {report.Qualifier:X}");
        Console.WriteLine($"    Event {report.Event:X}");
        Console.WriteLine($"    Group/partition {report.Group:X}");
        Console.WriteLine($"    User/Zone information {report.Data:X}");

        if (reports_entry < 0 || reports_entry >= reports.Length || reports[reports_entry] != report) {
            Console.WriteLine("Report mismatch");
            Exit(2);
        }
        rx_callback_reported = true;
    }

    private static void tx_callback(object? userData, int tone, int level, int duration) {
        _ = level;
        _ = duration;
        var sender = (ContactIdSender)(userData ?? throw new InvalidOperationException("Missing sender callback state."));
        Console.WriteLine($"Ademco sender report {tone}");

        switch (tone) {
            case -1:
                _ = sender.Put(reports[reports_entry]);
                break;
            case 1:
                if (++reports_entry < 5)
                    _ = sender.Put(reports[reports_entry]);
                else
                    sending_complete = true;
                break;
            case 0:
                sending_complete = true;
                break;
        }
    }

    private static int end_to_end_tests() {
        Console.WriteLine("End to end tests");
        reports_entry = 0;
        sending_complete = false;
        rx_callback_reported = false;

        using var outhandle = new Pcm16WaveWriter(OUTPUT_FILE_NAME, 2, SAMPLE_RATE);
        using var receiver = new ContactIdReceiver(rx_callback, null);
        receiver.SetRealtimeCallback(rx_callback, receiver);
        using var sender = new ContactIdSender(tx_callback, null);
        sender.SetRealtimeCallback(tx_callback, sender);

        var noiseSource = new Awgn(1234567, -50.0);
        short[] amp = new short[SAMPLES_PER_CHUNK];
        short[] sndfileBuf = new short[2 * SAMPLES_PER_CHUNK];

        for (int i = 0; i < 1000; i++) {
            int samples = sender.Transmit(amp);
            if (samples < SAMPLES_PER_CHUNK)
                Array.Clear(amp, samples, SAMPLES_PER_CHUNK - samples);

            for (int j = 0; j < SAMPLES_PER_CHUNK; j++)
                sndfileBuf[2 * j] = amp[j];

            receiver.Receive(amp);

            samples = receiver.Transmit(amp);
            if (samples < SAMPLES_PER_CHUNK)
                Array.Clear(amp, samples, SAMPLES_PER_CHUNK - samples);

            CodecMungeALaw(amp);
            for (int j = 0; j < SAMPLES_PER_CHUNK; j++) {
                sndfileBuf[2 * j + 1] = amp[j];
                amp[j] = SaturatingAdd(amp[j], noiseSource.NextSample());
            }
            CodecMungeALaw(amp);
            sender.Receive(amp);
            outhandle.WriteFrames(sndfileBuf);

            if (sending_complete && rx_callback_reported)
                break;
        }

        if (!rx_callback_reported) {
            Console.Error.WriteLine("    Report not received");
            return -1;
        }
        Console.WriteLine("    Passed");
        return 0;
    }

    private static int encode_decode_tests() {
        Console.WriteLine("Encode and decode tests");
        using var receiver = new ContactIdReceiver(null, null);
        using var sender = new ContactIdSender(null, null);

        for (int i = 0; i < 5; i++) {
            if (!ContactIdCodec.TryEncode(reports[i], out string encoded)) {
                Console.WriteLine("Bad encode message");
                return -1;
            }
            Console.WriteLine($"'{encoded}'");

            if (!ContactIdCodec.TryDecode(encoded, out ContactIdReport result)) {
                Console.WriteLine("Bad decode message");
                return -1;
            }
            receiver.LogMessage(result);
            Console.WriteLine();

            if (reports[i] != result) {
                Console.WriteLine("Received message does not match the one sent");
                return -1;
            }
        }

        if (ContactIdCodec.TryEncode(reports[5], out string invalidEncoded)) {
            Console.WriteLine("Incorrectly good message");
            return -1;
        }
        Console.WriteLine($"'{invalidEncoded}'");
        Console.WriteLine();
        Console.WriteLine("    Passed");
        return 0;
    }

    private static void decode_file(string file) {
        // The source test contains only a commented declaration in this path.
        // Preserve that behavior while validating that the supplied path is not null.
        ArgumentNullException.ThrowIfNull(file);
    }

    private static void CodecMungeALaw(Span<short> samples) {
        for (int i = 0; i < samples.Length; i++)
            samples[i] = g711.alaw_to_linear(g711.linear_to_alaw(samples[i]));
    }

    private static short SaturatingAdd(short left, short right) {
        int value = left + right;
        if (value > short.MaxValue)
            return short.MaxValue;
        if (value < short.MinValue)
            return short.MinValue;
        return (short)value;
    }

    internal static void Exit(int code) => ademco_contactid_tests_adapter.tkfx_ademco_contactid_tests_exit(code);

    internal sealed class AdemcoTestExitException : Exception {
        public AdemcoTestExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private readonly record struct ContactIdReport(
        int Account,
        int MessageType,
        int Qualifier,
        int Event,
        int Group,
        int Data);

    private static class ContactIdCodec {
        private static readonly char[] Remap = { 'D', '*', '#', 'A', 'B', 'C' };

        public static bool TryEncode(ContactIdReport report, out string message) {
            string source = string.Format(
                CultureInfo.InvariantCulture,
                "{0:X4}{1:X2}{2:X1}{3:X3}{4:X2}{5:X3}",
                report.Account,
                report.MessageType,
                report.Qualifier,
                report.Event,
                report.Group,
                report.Data);
            var chars = source.ToCharArray();
            int sum = 0;

            for (int i = 0; i < chars.Length; i++) {
                int value;
                if (chars[i] == 'A') {
                    message = source;
                    return false;
                }
                if (chars[i] > '9') {
                    value = chars[i] - ('A' - 10);
                    chars[i] = Remap[value - 10];
                } else {
                    value = chars[i] - '0';
                    if (value == 0)
                        value = 10;
                }
                sum += value;
            }

            int checksum = ((sum + 15) / 15) * 15 - sum;
            char check = checksum switch {
                0 => 'C',
                <= 9 => (char)('0' + checksum),
                _ => Remap[checksum - 10]
            };
            message = new string(chars) + check;
            return true;
        }

        public static bool TryDecode(string message, out ContactIdReport report) {
            report = default;
            if (message.Length != 16)
                return false;

            Span<char> pseudoHex = stackalloc char[message.Length];
            int sum = 0;
            for (int i = 0; i < message.Length; i++) {
                char mapped = message[i] switch {
                    '*' => 'B',
                    '#' => 'C',
                    'A' => 'D',
                    'B' => 'E',
                    'C' => 'F',
                    'D' => 'A',
                    _ => message[i]
                };
                pseudoHex[i] = mapped;

                int value;
                if (mapped > '9')
                    value = mapped - ('B' - 11);
                else
                    value = mapped == '0' ? 10 : mapped - '0';
                if (value < 0 || value > 15)
                    return false;
                sum += value;
            }

            if (sum % 15 != 0)
                return false;

            try {
                report = new ContactIdReport(
                    ParseHex(pseudoHex.Slice(0, 4)),
                    ParseHex(pseudoHex.Slice(4, 2)),
                    ParseHex(pseudoHex.Slice(6, 1)),
                    ParseHex(pseudoHex.Slice(7, 3)),
                    ParseHex(pseudoHex.Slice(10, 2)),
                    ParseHex(pseudoHex.Slice(12, 3)));
                return true;
            } catch (FormatException) {
                return false;
            }
        }

        private static int ParseHex(ReadOnlySpan<char> value) =>
            int.Parse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
    }

    private sealed class ContactIdReceiver : IDisposable {
        private readonly DtmfRxState dtmf;
        private Action<object?, ContactIdReport>? callback;
        private object? callbackUserData;
        private readonly StringBuilder rxDigits = new(16);
        private int step;
        private int remainingSamples;
        private int tonePhaseRate;
        private short toneLevel;
        private uint tonePhase;

        public ContactIdReceiver(Action<object?, ContactIdReport>? callback, object? userData) {
            this.callback = callback;
            callbackUserData = userData;
            dtmf = dtmf_rx_init(null, DeliverDigits, this);
            step = 0;
            remainingSamples = MillisecondsToSamples(500);
        }

        public void SetRealtimeCallback(Action<object?, ContactIdReport>? value, object? userData) {
            callback = value;
            callbackUserData = userData;
        }

        public int Transmit(Span<short> amp) {
            int samples;
            switch (step) {
                case 0:
                    samples = Math.Min(remainingSamples, amp.Length);
                    amp[..samples].Clear();
                    remainingSamples -= samples;
                    if (remainingSamples == 0) {
                        Console.WriteLine("Initial silence finished");
                        step++;
                        tonePhaseRate = dds_phase_rate(1400.0f);
                        toneLevel = dds_scaling_dbm0(-11.0f);
                        tonePhase = 0;
                        remainingSamples = MillisecondsToSamples(100);
                    }
                    return samples;

                case 1:
                    samples = GenerateTone(amp, 1400.0f);
                    if (remainingSamples == 0) {
                        Console.WriteLine("1400Hz tone finished");
                        step++;
                        remainingSamples = MillisecondsToSamples(100);
                    }
                    return samples;

                case 2:
                    samples = Math.Min(remainingSamples, amp.Length);
                    amp[..samples].Clear();
                    remainingSamples -= samples;
                    if (remainingSamples == 0) {
                        Console.WriteLine("Second silence finished");
                        step++;
                        tonePhaseRate = dds_phase_rate(2300.0f);
                        toneLevel = dds_scaling_dbm0(-11.0f);
                        tonePhase = 0;
                        remainingSamples = MillisecondsToSamples(100);
                    }
                    return samples;

                case 3:
                    samples = GenerateTone(amp, 2300.0f);
                    if (remainingSamples == 0) {
                        Console.WriteLine("2300Hz tone finished");
                        step++;
                        remainingSamples = MillisecondsToSamples(100);
                    }
                    return samples;

                case 4:
                    return 0;

                case 5:
                    samples = Math.Min(remainingSamples, amp.Length);
                    amp[..samples].Clear();
                    remainingSamples -= samples;
                    if (remainingSamples == 0) {
                        Console.WriteLine("Sending kissoff");
                        step++;
                        tonePhaseRate = dds_phase_rate(1400.0f);
                        toneLevel = dds_scaling_dbm0(-11.0f);
                        tonePhase = 0;
                        remainingSamples = MillisecondsToSamples(850);
                    }
                    return samples;

                case 6:
                    samples = GenerateTone(amp, 1400.0f);
                    if (remainingSamples == 0) {
                        Console.WriteLine("1400Hz tone finished");
                        step = 4;
                        remainingSamples = MillisecondsToSamples(100);
                    }
                    return samples;

                default:
                    return amp.Length;
            }
        }

        public int Receive(ReadOnlySpan<short> amp) => dtmf_rx(dtmf, amp);

        public void LogMessage(ContactIdReport report) {
            Console.WriteLine("Ademco Contact ID message:");
            Console.WriteLine($"    Account {report.Account:X}");
            string messageType = report.MessageType is 0x18 or 0x98 ? "Contact ID" : "???";
            Console.WriteLine($"    Message type {messageType} ({report.MessageType:X})");
            string qualifier = report.Qualifier switch {
                1 => "New event",
                3 => "New restore",
                6 => "Status report",
                _ => "???"
            };
            Console.WriteLine($"    Qualifier {qualifier} ({report.Qualifier:X})");
            Console.WriteLine($"    Event {EventToString(report.Event)} ({report.Event:X})");
            Console.WriteLine($"    Group/partition {report.Group:X}");
            Console.WriteLine($"    User/Zone information {report.Data:X}");
        }

        public void Dispose() => dtmf_rx_free(dtmf);

        private int GenerateTone(Span<short> amp, float expectedFrequency) {
            _ = expectedFrequency;
            int samples = Math.Min(remainingSamples, amp.Length);
            for (int i = 0; i < samples; i++)
                amp[i] = dds_mod(ref tonePhase, tonePhaseRate, toneLevel, 0);
            remainingSamples -= samples;
            return samples;
        }

        private static void DeliverDigits(object? userData, string digits, int length) {
            var state = (ContactIdReceiver)(userData ?? throw new InvalidOperationException("Missing receiver state."));
            int accepted = Math.Min(length, digits.Length);
            for (int i = 0; i < accepted; i++)
                state.rxDigits.Append(digits[i]);

            while (state.rxDigits.Length >= 16) {
                string message = state.rxDigits.ToString(0, 16);
                state.rxDigits.Remove(0, 16);
                if (ContactIdCodec.TryDecode(message, out ContactIdReport report)) {
                    state.LogMessage(report);
                    state.callback?.Invoke(state.callbackUserData, report);
                    state.step++;
                }
            }
        }
    }

    private sealed class ContactIdSender : IDisposable {
        private const int GOERTZEL_SAMPLES_PER_BLOCK = 55;
        private const float DetectionThreshold = 49728296.6f;
        private const float ToneToTotalEnergy = 45.2233f;

        private readonly DtmfTxState dtmf;
        private readonly Goertzel tone1400 = new(1400.0f);
        private readonly Goertzel tone2300 = new(2300.0f);
        private Action<object?, int, int, int>? callback;
        private object? callbackUserData;
        private int step;
        private int remainingSamples;
        private bool clearToSend;
        private bool busy;
        private string txDigits = string.Empty;
        private int currentSample;
        private float energy;
        private int inTone;
        private int lastHit;
        private int toneState;
        private int duration;
        private int timer;
        private int tries;

        public ContactIdSender(Action<object?, int, int, int>? callback, object? userData) {
            this.callback = callback;
            callbackUserData = userData;
            step = 0;
            remainingSamples = MillisecondsToSamples(100);
            dtmf = dtmf_tx_init(null, null, null);
            dtmf_tx_set_timing(dtmf, 55, 55);
        }

        public void SetRealtimeCallback(Action<object?, int, int, int>? value, object? userData) {
            callback = value;
            callbackUserData = userData;
        }

        public int Put(ContactIdReport report) {
            if (busy)
                return -1;
            if (!ContactIdCodec.TryEncode(report, out txDigits))
                return -1;
            busy = true;
            return dtmf_tx_put(dtmf, txDigits, txDigits.Length);
        }

        public int Transmit(Span<short> amp) {
            int sample = 0;
            while (sample < amp.Length) {
                switch (step) {
                    case 0:
                        if (!clearToSend)
                            return sample;
                        clearToSend = false;
                        step++;
                        remainingSamples = MillisecondsToSamples(250);
                        goto case 1;

                    case 1:
                        int silence = Math.Min(remainingSamples, amp.Length - sample);
                        amp.Slice(sample, silence).Clear();
                        remainingSamples -= silence;
                        sample += silence;
                        if (remainingSamples > 0)
                            return sample;
                        Console.WriteLine("Pre-send silence finished");
                        step++;
                        break;

                    case 2:
                        int generated = dtmf_tx(dtmf, amp[sample..]);
                        if (generated == 0) {
                            clearToSend = false;
                            step = 0;
                            return sample;
                        }
                        sample += generated;
                        break;

                    default:
                        return sample;
                }
            }
            return sample;
        }

        public int Receive(ReadOnlySpan<short> amp) {
            int sample = 0;
            while (sample < amp.Length) {
                int needed = GOERTZEL_SAMPLES_PER_BLOCK - currentSample;
                int limit = Math.Min(amp.Length, sample + needed);
                for (int j = sample; j < limit; j++) {
                    float value = amp[j];
                    energy += value * value;
                    tone1400.Sample(value);
                    tone2300.Sample(value);
                }
                currentSample += limit - sample;
                sample = limit;
                if (currentSample < GOERTZEL_SAMPLES_PER_BLOCK)
                    continue;

                float energy1400 = tone1400.Result();
                float energy2300 = tone2300.Result();
                int hit = 0;
                if (energy1400 > DetectionThreshold || energy2300 > DetectionThreshold) {
                    if (energy1400 > energy2300) {
                        if (energy1400 > ToneToTotalEnergy * energy)
                            hit = 1;
                    } else if (energy2300 > ToneToTotalEnergy * energy) {
                        hit = 2;
                    }
                }

                if (hit != inTone && hit == lastHit)
                    ApplyStableHit(hit);

                lastHit = hit;
                duration += GOERTZEL_SAMPLES_PER_BLOCK;
                UpdateTimer();
                energy = 0.0f;
                currentSample = 0;
            }
            return 0;
        }

        public void Dispose() => dtmf_tx_free(dtmf);

        private void ApplyStableHit(int hit) {
            switch (toneState) {
                case 0:
                    if (hit == 1) {
                        Console.WriteLine("Receiving initial 1400Hz");
                        inTone = hit;
                        toneState = 1;
                        duration = 0;
                    }
                    break;

                case 1:
                    if (hit == 0) {
                        if (duration < MillisecondsToSamples(70) || duration > MillisecondsToSamples(130)) {
                            Console.WriteLine("Bad initial 1400Hz tone duration");
                            toneState = 0;
                        } else {
                            Console.WriteLine("Received 1400Hz tone");
                            toneState = 2;
                        }
                        inTone = hit;
                        duration = 0;
                    }
                    break;

                case 2:
                    if (duration < MillisecondsToSamples(70) || duration > MillisecondsToSamples(130)) {
                        Console.WriteLine("Bad silence length");
                        toneState = 0;
                        inTone = hit;
                    } else if (hit == 2) {
                        Console.WriteLine("Received silence");
                        toneState = 3;
                        inTone = hit;
                    } else {
                        toneState = 0;
                        inTone = 0;
                    }
                    duration = 0;
                    break;

                case 3:
                    if (hit == 0) {
                        if (duration < MillisecondsToSamples(70) || duration > MillisecondsToSamples(130)) {
                            Console.WriteLine("Bad initial 2300Hz tone duration");
                            toneState = 0;
                        } else {
                            Console.WriteLine("Received 2300Hz");
                            callback?.Invoke(callbackUserData, -1, 0, 0);
                            toneState = 4;
                            clearToSend = true;
                            tries = 0;
                            if (txDigits.Length != 0)
                                timer = MillisecondsToSamples(3000);
                        }
                        inTone = hit;
                        duration = 0;
                    }
                    break;

                case 4:
                    if (hit == 1) {
                        Console.WriteLine("Receiving kissoff");
                        toneState = 5;
                        inTone = hit;
                        duration = 0;
                    }
                    break;

                case 5:
                    if (hit == 0) {
                        busy = false;
                        if (duration < MillisecondsToSamples(400) || duration > MillisecondsToSamples(1500)) {
                            Console.WriteLine($"Bad kissoff duration {duration}");
                            if (++tries < 4) {
                                _ = dtmf_tx_put(dtmf, txDigits, txDigits.Length);
                                timer = MillisecondsToSamples(3000);
                                toneState = 4;
                            } else {
                                timer = 0;
                                callback?.Invoke(callbackUserData, 0, 0, 0);
                            }
                        } else {
                            Console.WriteLine("Received good kissoff");
                            clearToSend = true;
                            txDigits = string.Empty;
                            callback?.Invoke(callbackUserData, 1, 0, 0);
                            toneState = 4;
                            clearToSend = true;
                            tries = 0;
                            if (txDigits.Length != 0)
                                timer = MillisecondsToSamples(3000);
                        }
                        inTone = hit;
                        duration = 0;
                    }
                    break;
            }
        }

        private void UpdateTimer() {
            if (timer <= 0)
                return;
            timer -= GOERTZEL_SAMPLES_PER_BLOCK;
            if (timer > 0)
                return;

            Console.WriteLine("Timer expired");
            if (toneState == 4 && txDigits.Length != 0) {
                if (++tries < 4) {
                    _ = dtmf_tx_put(dtmf, txDigits, txDigits.Length);
                    timer = MillisecondsToSamples(3000);
                } else {
                    timer = 0;
                    callback?.Invoke(callbackUserData, 0, 0, 0);
                }
            }
        }

        private sealed class Goertzel {
            private readonly float factor;
            private float v2;
            private float v3;

            public Goertzel(float frequency) =>
                factor = 2.0f * MathF.Cos(2.0f * MathF.PI * frequency / SAMPLE_RATE);

            public void Sample(float amplitude) {
                float previousV2 = v2;
                v2 = v3;
                v3 = factor * v2 - previousV2 + amplitude;
            }

            public float Result() {
                float previousV2 = v2;
                v2 = v3;
                v3 = factor * v2 - previousV2;
                float result = (v3 * v3 + v2 * v2 - v2 * v3 * factor) * 2.0f;
                v2 = 0.0f;
                v3 = 0.0f;
                return result;
            }
        }
    }

    private static int MillisecondsToSamples(int milliseconds) =>
        checked(milliseconds * SAMPLE_RATE / 1000);

    private static string EventToString(int eventCode) => eventCode switch {
        0x131 => "Perimeter",
        0x401 => "O/C by user",
        0x890 => "???",
        _ => "???"
    };

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataEnd;
        private readonly int channels;

        private Pcm16WaveReader(FileStream stream, BinaryReader reader, long dataEnd, int channels) {
            this.stream = stream;
            this.reader = reader;
            this.dataEnd = dataEnd;
            this.channels = channels;
        }

        public static Pcm16WaveReader OpenMono(string path) {
            var stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (ReadFourCc(reader) != "RIFF")
                throw new InvalidDataException($"'{path}' is not a RIFF file.");
            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
                throw new InvalidDataException($"'{path}' is not a WAVE file.");

            ushort format = 0;
            int channels = 0;
            int bits = 0;
            long dataStart = -1;
            uint dataLength = 0;

            while (stream.Position + 8 <= stream.Length) {
                string id = ReadFourCc(reader);
                uint size = reader.ReadUInt32();
                long next = stream.Position + size + (size & 1);
                if (id == "fmt ") {
                    format = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    bits = reader.ReadUInt16();
                } else if (id == "data") {
                    dataStart = stream.Position;
                    dataLength = size;
                    break;
                }
                stream.Position = next;
            }

            if (format != 1 || bits != 16 || channels < 1 || dataStart < 0) {
                reader.Dispose();
                stream.Dispose();
                throw new InvalidDataException($"'{path}' must be PCM16 WAV.");
            }

            stream.Position = dataStart;
            return new Pcm16WaveReader(stream, reader, dataStart + dataLength, channels);
        }

        public int ReadFrames(short[] destination, int maximumFrames) {
            int frames = 0;
            while (frames < maximumFrames && stream.Position + channels * 2L <= dataEnd) {
                short first = reader.ReadInt16();
                for (int channel = 1; channel < channels; channel++)
                    _ = reader.ReadInt16();
                destination[frames++] = first;
            }
            return frames;
        }

        public void Dispose() {
            reader.Dispose();
            stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long samplesWritten;
        private bool disposed;

        public Pcm16WaveWriter(string path, short channels, int sampleRate) {
            this.channels = channels;
            this.sampleRate = sampleRate;
            stream = File.Create(path);
            writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
            WriteHeader(0);
        }

        public void WriteFrames(ReadOnlySpan<short> interleavedSamples) {
            foreach (short sample in interleavedSamples)
                writer.Write(sample);
            samplesWritten += interleavedSamples.Length;
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;
            uint dataBytes = checked((uint)(samplesWritten * sizeof(short)));
            stream.Position = 0;
            WriteHeader(dataBytes);
            writer.Flush();
            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(uint dataBytes) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36u + dataBytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write((uint)(sampleRate * channels * sizeof(short)));
            writer.Write((ushort)(channels * sizeof(short)));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataBytes);
        }
    }
}
