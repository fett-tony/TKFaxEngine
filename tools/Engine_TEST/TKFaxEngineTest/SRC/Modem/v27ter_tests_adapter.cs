#nullable enable
using SignalStatus = TKFaxEngine.SignalStatus;

using System.Globalization;
using System.Reflection;
using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V27;
using static TKFaxEngine.AsyncApi;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;
using static TKFaxEngine.Modem.BertApi;
using static TKFaxEngine.Modem.V27.V27TerRxApi;
using static TKFaxEngine.Modem.V27.V27TerTxApi;

namespace TKFaxEngineTest.Modem.Test;

public static class v27ter_tests_adapter {
    private const int BLOCK_LEN = 160;
    private const string OUT_FILE_NAME = "v27ter.wav";
    private const int BERT_REPORT_REGULAR = 2;
    private static string? decode_test_file;
    private static int symbol_no;
    private static int rx_bits;
    private static BertState bert = null!;
    private static BertResults latest_results = new();
    private static float smooth_power;
    private static int update_interval = 100;

    private static void Reporter(object? userData, int reason, BertResults results) {
        if (reason == BERT_REPORT_REGULAR) {
            Errorf("BERT report regular - %d bits, %d bad bits, %d resyncs\n", results.TotalBits, results.BadBits, results.Resyncs);
            latest_results = results.Clone();
        } else Errorf("BERT report %s\n", bert_event_to_str(reason));
    }

    private static void RxStatus(object? userData, int status) {
        var state = (V27TerRxState)(userData ?? throw new ArgumentNullException(nameof(userData)));
        Printf("V.27ter rx status is %s (%d)\n", signal_status_to_str(status), status);
        if (status == (int)SignalStatus.TrainingSucceeded) {
            int length = v27ter_rx_equalizer_state(state, out ReadOnlyMemory<V27TerRxComplex> coefficients);
            if (length > 0) {
                Printf("Equalizer:\n");
                for (int i = 0; i < length; i++) {
                    V27TerRxComplex c = coefficients.Span[i];
                    Printf("%3d (%15.5f, %15.5f) -> %15.5f\n", i, c.Real, c.Imaginary, c.Real * c.Real + c.Imaginary * c.Imaginary);
                }
            }
        }
    }

    private static void PutBit(object? userData, int bit) {
        if (bit < 0) { RxStatus(userData, bit); return; }
        if (decode_test_file is not null) Printf("Rx bit %d - %d\n", rx_bits++, bit); else bert_put_bit(bert, bit);
    }

    private static void TxStatus(object? userData, int status) => Printf("V.27ter tx status is %s (%d)\n", signal_status_to_str(status), status);
    private static int GetBit(object? userData) => bert_get_bit(bert);

    private static void QamReport(object? userData, V27TerRxComplex? received, V27TerRxComplex? target, int symbol) {
        var rx = (V27TerRxState)(userData ?? throw new ArgumentNullException(nameof(userData)));
        if (received is not null && target is not null) {
            float er = received.Value.Real - target.Value.Real;
            float ei = received.Value.Imaginary - target.Value.Imaginary;
            float power = er * er + ei * ei;
            smooth_power = 0.95f * smooth_power + 0.05f * power;
            float trackingError = received.Value.Imaginary * target.Value.Real - received.Value.Real * target.Value.Imaginary;
            Printf("Tracking error %f %f %f %f %f %f\n", trackingError, v27ter_rx_carrier_frequency(rx), received.Value.Real, received.Value.Imaginary, target.Value.Real, target.Value.Imaginary);
            Printf("%8d [%8.4f, %8.4f] [%8.4f, %8.4f] %2x %8.4f %8.4f %9.4f %7.3f %7.4f\n",
                symbol_no++, received.Value.Real, received.Value.Imaginary, target.Value.Real, target.Value.Imaginary, symbol,
                power, smooth_power, v27ter_rx_carrier_frequency(rx), v27ter_rx_signal_power(rx), v27ter_rx_symbol_timing_correction(rx));
            if (--update_interval <= 0) { DumpEqualizer(rx, "Equalizer B:"); update_interval = 100; }
        } else {
            Printf("Gardner step %d\n", symbol);
            DumpEqualizer(rx, "Equalizer A:");
        }
    }

    private static void DumpEqualizer(V27TerRxState rx, string title) {
        int length = v27ter_rx_equalizer_state(rx, out ReadOnlyMemory<V27TerRxComplex> coefficients);
        if (length <= 0) return;
        Printf("%s\n", title);
        for (int i = 0; i < length; i++) {
            V27TerRxComplex c = coefficients.Span[i];
            Printf("%3d (%15.5f, %15.5f) -> %15.5f\n", i, c.Real, c.Imaginary, c.Real * c.Real + c.Imaginary * c.Imaginary);
        }
    }

    public static int tkfx_v27ter_tests_main(string[] testArguments) {
        try { return Run(testArguments); } catch (Exception ex) when (ex is ArgumentException or IOException or InvalidDataException) { Console.Error.WriteLine(ex.Message); return 2; }
    }

    private static int Run(string[] testArguments) {
        var generated = new short[BLOCK_LEN];
        var samplesBuffer = new short[BLOCK_LEN];
        WaveFile? input = null, output = null;
        int channelCodec = MUNGE_CODEC_NONE, rbsPattern = 0, testBps = 4800, lineModelNo = 0, noiseLevel = -70, signalLevel = -13, bitsPerTest = 50000;
        bool tep = false, logAudio = false;
        decode_test_file = null; symbol_no = 0; rx_bits = 0; latest_results = new BertResults(); smooth_power = 0; update_interval = 100;
        for (int index = 0; index < testArguments.Length; index++) {
            string arg = testArguments[index];
            switch (arg) {
                case "-b": testBps = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-B": bitsPerTest = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-c": channelCodec = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-d": decode_test_file = NextArgument(testArguments, ref index, arg); break;
                case "-g": Errorf("Graphical monitoring not available\n"); return 2;
                case "-l": logAudio = true; break;
                case "-m": lineModelNo = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-n": noiseLevel = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-r": rbsPattern = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-s": signalLevel = int.Parse(NextArgument(testArguments, ref index, arg), CultureInfo.InvariantCulture); break;
                case "-t": tep = true; break;
                default: return 2;
            }
        }
        if (testBps != 2400 && testBps != 4800) { Errorf("Invalid bit rate specified\n"); return 2; }
        if (logAudio) output = WaveFile.OpenWrite(OUT_FILE_NAME, 1);
        V27TerTxState? tx = null;
        OneWayLineModelState? line = null;
        if (decode_test_file is not null)
            input = WaveFile.OpenRead(decode_test_file, 1);
        else {
            bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20);
            bert_set_report(bert, 10000, Reporter, null);
            tx = v27ter_tx_init(null, testBps, tep, GetBit, null) ?? throw new InvalidOperationException("Unable to initialize V.27ter transmitter.");
            v27ter_tx_power(tx, signalLevel);
            v27ter_tx_set_modem_status_handler(tx, TxStatus, tx);
            V27Internals.SetCarrierFrequency(tx, 1810.0f);
            line = new OneWayLineModelState(lineModelNo, noiseLevel, channelCodec, rbsPattern);
        }
        V27TerRxState rx = v27ter_rx_init(null, testBps, PutBit, null) ?? throw new InvalidOperationException("Unable to initialize V.27ter receiver.");
        v27ter_rx_set_modem_status_handler(rx, RxStatus, rx);
        v27ter_rx_set_qam_report_handler(rx, QamReport, rx);
        while (true) {
            int samples;
            if (input is not null) {
                samples = input.ReadFrames(samplesBuffer, BLOCK_LEN);
                if (samples == 0) break;
            } else {
                samples = v27ter_tx(tx!, generated, BLOCK_LEN);
                if (samples == 0) {
                    Array.Clear(samplesBuffer);
                    v27ter_rx(rx, samplesBuffer, BLOCK_LEN); v27ter_rx(rx, samplesBuffer, BLOCK_LEN); v27ter_rx(rx, samplesBuffer, BLOCK_LEN);
                    var final = new BertResults(); bert_result(bert, final);
                    Errorf("Final result %ddBm0/%ddBm0, %d bits, %d bad bits, %d resyncs\n", signalLevel, noiseLevel, final.TotalBits, final.BadBits, final.Resyncs);
                    Errorf("Last report  %ddBm0/%ddBm0, %d bits, %d bad bits, %d resyncs\n", signalLevel, noiseLevel, latest_results.TotalBits, latest_results.BadBits, latest_results.Resyncs);
                    if (final.TotalBits < bitsPerTest || latest_results.TotalBits < bitsPerTest - 100 || latest_results.BadBits != 0) break;
                    latest_results = new BertResults(); signalLevel--;
                    _ = v27ter_tx_restart(tx!, testBps, tep); v27ter_tx_power(tx!, signalLevel); _ = v27ter_rx_restart(rx, testBps, false);
                    bert_free(bert); bert = bert_init(null, bitsPerTest, BERT_PATTERN_ITU_O152_11, testBps, 20); bert_set_report(bert, 10000, Reporter, null);
                    line.Dispose(); line = new OneWayLineModelState(lineModelNo, noiseLevel, channelCodec, 0);
                    continue;
                }
                if (output is not null && output.WriteFrames(generated, samples) != samples) throw new IOException("Error writing audio file.");
                line!.Process(samplesBuffer, generated, samples);
            }
            v27ter_rx(rx, samplesBuffer, samples);
        }
        int resultCode = 0;
        if (input is null) {
            var final = new BertResults(); bert_result(bert, final);
            Errorf("At completion:\nFinal result %ddBm0/%ddBm0, %d bits, %d bad bits, %d resyncs\n", signalLevel, noiseLevel, final.TotalBits, final.BadBits, final.Resyncs);
            Errorf("Last report  %ddBm0/%ddBm0, %d bits, %d bad bits, %d resyncs\n", signalLevel, noiseLevel, latest_results.TotalBits, latest_results.BadBits, latest_results.Resyncs);
            if (signalLevel > -43) { Printf("Tests failed.\n"); resultCode = 2; } else Printf("Tests passed.\n");
        }
        line?.Dispose(); input?.Dispose(); output?.Dispose(); v27ter_rx_free(rx); if (tx is not null) v27ter_tx_free(tx); if (input is null) bert_free(bert);
        return resultCode;
    }

    private static class V27Internals {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        public static void SetCarrierFrequency(V27TerTxState state, float frequency) {
            int phaseRate = unchecked((int)Math.Round(frequency * 4294967296.0 / 8000.0));
            FieldInfo field = state.GetType().GetField("_carrierPhaseRate", Flags) ?? throw new MissingFieldException(state.GetType().FullName, "_carrierPhaseRate");
            field.SetValue(state, phaseRate);
        }
    }

    private static string NextArgument(string[] testArguments, ref int index, string option) {
        index++;
        if (index >= testArguments.Length)
            throw new ArgumentException($"Missing argument for {option}.");
        return testArguments[index];
    }

    private static void Printf(string format, params object?[] args) => Console.Write(CFormat(format, args));
    private static void Errorf(string format, params object?[] args) => Console.Error.Write(CFormat(format, args));

    private static string CFormat(string format, params object?[] args) {
        var output = new StringBuilder(format.Length + args.Length * 12);
        int argument = 0;
        for (int i = 0; i < format.Length; i++) {
            if (format[i] != '%' || i + 1 >= format.Length) { output.Append(format[i]); continue; }
            if (format[i + 1] == '%') { output.Append('%'); i++; continue; }
            int j = i + 1;
            while (j < format.Length && "-+ #0".IndexOf(format[j]) >= 0) j++;
            int widthStart = j;
            while (j < format.Length && char.IsDigit(format[j])) j++;
            int width = j > widthStart ? int.Parse(format[widthStart..j], CultureInfo.InvariantCulture) : 0;
            int precision = -1;
            if (j < format.Length && format[j] == '.') {
                j++;
                int pStart = j;
                while (j < format.Length && char.IsDigit(format[j])) j++;
                precision = j > pStart ? int.Parse(format[pStart..j], CultureInfo.InvariantCulture) : 0;
            }
            while (j < format.Length && "hlLzjt".IndexOf(format[j]) >= 0) j++;
            if (j >= format.Length || argument >= args.Length) break;
            char specifier = format[j];
            object? value = args[argument++];
            string rendered = specifier switch {
                's' => value?.ToString() ?? "(null)",
                'c' => Convert.ToChar(value, CultureInfo.InvariantCulture).ToString(),
                'd' or 'i' => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                'u' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                'x' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString("x", CultureInfo.InvariantCulture),
                'X' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString("X", CultureInfo.InvariantCulture),
                'f' or 'F' => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(precision >= 0 ? $"F{precision}" : "F6", CultureInfo.InvariantCulture),
                'g' => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("g", CultureInfo.InvariantCulture),
                'G' => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("G", CultureInfo.InvariantCulture),
                'p' => value is null ? "0x0" : $"0x{value.GetHashCode():X}",
                _ => value?.ToString() ?? string.Empty
            };
            if (width > rendered.Length) rendered = rendered.PadLeft(width);
            output.Append(rendered);
            i = j;
        }
        return output.ToString();
    }

    private sealed class WaveFile : IDisposable {
        private readonly FileStream _stream;
        private readonly bool _write;
        private readonly int _channels;
        private bool _closed;
        private int _dataLength;

        private WaveFile(FileStream stream, bool write, int channels) {
            _stream = stream;
            _write = write;
            _channels = channels;
            if (write) WriteHeader(0); else ReadHeader(channels);
        }

        public static WaveFile OpenWrite(string path, int channels)
            => new(new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read), true, channels);

        public static WaveFile OpenRead(string path, int channels)
            => new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), false, channels);

        public int WriteFrames(short[] samples, int frames) {
            if (!_write || _closed) return 0;
            int count = Math.Min(samples.Length, checked(frames * _channels));
            Span<byte> pair = stackalloc byte[2];
            for (int i = 0; i < count; i++) {
                pair[0] = (byte)samples[i];
                pair[1] = (byte)(samples[i] >> 8);
                _stream.Write(pair);
            }
            _dataLength += count * 2;
            return count / _channels;
        }

        public int ReadFrames(short[] samples, int frames) {
            if (_write || _closed) return 0;
            int requested = Math.Min(samples.Length, checked(frames * _channels));
            Span<byte> pair = stackalloc byte[2];
            int count = 0;
            while (count < requested && _stream.Read(pair) == 2)
                samples[count++] = (short)(pair[0] | (pair[1] << 8));
            return count / _channels;
        }

        public int Close() {
            Dispose();
            return 0;
        }

        public void Dispose() {
            if (_closed) return;
            _closed = true;
            if (_write && _stream.CanSeek) {
                _stream.Position = 0;
                WriteHeader(_dataLength);
            }
            _stream.Dispose();
        }

        private void WriteHeader(int dataLength) {
            using var writer = new BinaryWriter(_stream, Encoding.ASCII, leaveOpen: true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)_channels);
            writer.Write(8000);
            writer.Write(8000 * _channels * 2);
            writer.Write((short)(_channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
        }

        private void ReadHeader(int expectedChannels) {
            using var reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                throw new InvalidDataException("Not a RIFF WAVE file.");
            _ = reader.ReadInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
                throw new InvalidDataException("Not a WAVE file.");
            bool formatSeen = false;
            while (_stream.Position + 8 <= _stream.Length) {
                string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int size = reader.ReadInt32();
                long next = _stream.Position + size + (size & 1);
                if (id == "fmt ") {
                    short format = reader.ReadInt16();
                    short channels = reader.ReadInt16();
                    int rate = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt16();
                    short bits = reader.ReadInt16();
                    if (format != 1 || channels != expectedChannels || rate != 8000 || bits != 16)
                        throw new InvalidDataException($"Expected 8 kHz, {expectedChannels}-channel, 16-bit PCM.");
                    formatSeen = true;
                } else if (id == "data") {
                    if (!formatSeen) throw new InvalidDataException("WAVE format chunk missing before data.");
                    return;
                }
                _stream.Position = next;
            }
            throw new InvalidDataException("WAVE data chunk not found.");
        }
    }

    private const int MUNGE_CODEC_NONE = 0;
    private const int MUNGE_CODEC_ALAW = 1;
    private const int MUNGE_CODEC_ULAW = 2;
    private const int MUNGE_CODEC_G726_40K = 3;
    private const int MUNGE_CODEC_G726_32K = 4;
    private const int MUNGE_CODEC_G726_24K = 5;
    private const int MUNGE_CODEC_G726_16K = 6;

    private static readonly float[][] LineModels =
    {
    // null_line_model
    new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f },
    // proakis_line_model
    new float[] { -0.000337322884f, -0.000488974636f, -0.000483556802f, -0.000343652889f, -0.000422924832f, -0.000369374895f, -0.000547705329f, -0.000603805001f, -0.00043069458f, -0.000679285741f, -0.000422642303f, -0.000285485492f, -0.000312449716f, -0.000478764176f, -0.000517698242f, -0.000640168297f, -0.000667253497f, -0.000434544177f, -0.000507683271f, -0.000362257698f, -0.00068009969f, -0.000219282207f, -0.000586763576f, 0.000133211946f, -3.45931192e-05f, 0.000144575479f, -0.000708978562f, -0.000305635053f, -0.000999026149f, -0.000616480657f, -0.000900209203f, -0.000907829976f, -0.00145849632f, -0.00163842953f, -0.00137709588f, -0.00153905612f, -0.000994426426f, -0.00117869242f, -0.00170926624f, -0.00339261069f, -0.00219492005f, -0.0038511949f, -0.00360432573f, -0.0031096534f, -0.00353263581f, -0.00380505464f, -0.00536745234f, -0.00707273729f, -0.0048968504f, -0.00947317522f, -0.00581121975f, -0.00517640425f, -0.00526265396f, -0.0108630976f, -0.0121198133f, -0.019724233f, -0.0228209902f, -0.0207222964f, -0.0278230196f, -0.0315058146f, -0.0782049676f, -0.0506121441f, -0.243419098f, 0.18190928f, 0.889069816f, 0.18190928f, -0.243419098f, -0.0506121441f, -0.0782049676f, -0.0315058146f, -0.0278230196f, -0.0207222964f, -0.0228209902f, -0.019724233f, -0.0121198133f, -0.0108630976f, -0.00526265396f, -0.00517640425f, -0.00581121975f, -0.00947317522f, -0.0048968504f, -0.00707273729f, -0.00536745234f, -0.00380505464f, -0.00353263581f, -0.0031096534f, -0.00360432573f, -0.0038511949f, -0.00219492005f, -0.00339261069f, -0.00170926624f, -0.00117869242f, -0.000994426426f, -0.00153905612f, -0.00137709588f, -0.00163842953f, -0.00145849632f, -0.000907829976f, -0.000900209203f, -0.000616480657f, -0.000999026149f, -0.000305635053f, -0.000708978562f, 0.000144575479f, -3.45931192e-05f, 0.000133211946f, -0.000586763576f, -0.000219282207f, -0.00068009969f, -0.000362257698f, -0.000507683271f, -0.000434544177f, -0.000667253497f, -0.000640168297f, -0.000517698242f, -0.000478764176f, -0.000312449716f, -0.000285485492f, -0.000422642303f, -0.000679285741f, -0.00043069458f, -0.000603805001f, -0.000547705329f, -0.000369374895f, -0.000422924832f, -0.000343652889f, -0.000483556802f, -0.000488974636f, -0.000337322884f },
    // ad_1_edd_1_model
    new float[] { -0.000819381925f, -0.000156787646f, -0.0010706276f, -0.000642467186f, -0.00115849348f, -0.00108103719f, -0.00140335019f, -0.00136930233f, -0.00161166413f, -0.00168521815f, -0.00173381102f, -0.0019741123f, -0.00179771599f, -0.00193567108f, -0.00200145368f, -0.00167096423f, -0.00184598531f, -0.00137156528f, -0.00147401359f, -0.000897670217f, -0.000803172645f, -0.000363205297f, 0.00011645718f, 7.65828629e-05f, 0.00124684058f, 0.000831279868f, 0.00181988843f, 0.00194604301f, 0.00242348987f, 0.00284592469f, 0.00310078538f, 0.00354046639f, 0.00399873933f, 0.0040934705f, 0.00505406994f, 0.00398811415f, 0.00688673525f, 0.00358785309f, 0.00793210273f, 0.00350936354f, 0.00852952321f, 0.00265428599f, 0.00861854437f, 0.000445182961f, 0.0075799148f, -0.00172276752f, 0.00252837989f, -0.00393161374f, -0.0046258024f, -0.0076630604f, -0.0151212663f, -0.0117292911f, -0.0287317532f, -0.0142194401f, -0.047755955f, -0.0154688546f, -0.066326867f, -0.0147991566f, -0.0905380592f, -0.00927047971f, -0.109438307f, 0.00108161701f, -0.151400244f, 0.0513642553f, 0.943430875f, 0.0513642553f, -0.151400244f, 0.00108161701f, -0.109438307f, -0.00927047971f, -0.0905380592f, -0.0147991566f, -0.066326867f, -0.0154688546f, -0.047755955f, -0.0142194401f, -0.0287317532f, -0.0117292911f, -0.0151212663f, -0.0076630604f, -0.0046258024f, -0.00393161374f, 0.00252837989f, -0.00172276752f, 0.0075799148f, 0.000445182961f, 0.00861854437f, 0.00265428599f, 0.00852952321f, 0.00350936354f, 0.00793210273f, 0.00358785309f, 0.00688673525f, 0.00398811415f, 0.00505406994f, 0.0040934705f, 0.00399873933f, 0.00354046639f, 0.00310078538f, 0.00284592469f, 0.00242348987f, 0.00194604301f, 0.00181988843f, 0.000831279868f, 0.00124684058f, 7.65828629e-05f, 0.00011645718f, -0.000363205297f, -0.000803172645f, -0.000897670217f, -0.00147401359f, -0.00137156528f, -0.00184598531f, -0.00167096423f, -0.00200145368f, -0.00193567108f, -0.00179771599f, -0.0019741123f, -0.00173381102f, -0.00168521815f, -0.00161166413f, -0.00136930233f, -0.00140335019f, -0.00108103719f, -0.00115849348f, -0.000642467186f, -0.0010706276f, -0.000156787646f, -0.000819381925f },
    // ad_1_edd_2_model
    new float[] { -0.000819381925f, -0.000156787646f, -0.0010706276f, -0.000642467186f, -0.00115849348f, -0.00108103719f, -0.00140335019f, -0.00136930233f, -0.00161166413f, -0.00168521815f, -0.00173381102f, -0.0019741123f, -0.00179771599f, -0.00193567108f, -0.00200145368f, -0.00167096423f, -0.00184598531f, -0.00137156528f, -0.00147401359f, -0.000897670217f, -0.000803172645f, -0.000363205297f, 0.00011645718f, 7.65828629e-05f, 0.00124684058f, 0.000831279868f, 0.00181988843f, 0.00194604301f, 0.00242348987f, 0.00284592469f, 0.00310078538f, 0.00354046639f, 0.00399873933f, 0.0040934705f, 0.00505406994f, 0.00398811415f, 0.00688673525f, 0.00358785309f, 0.00793210273f, 0.00350936354f, 0.00852952321f, 0.00265428599f, 0.00861854437f, 0.000445182961f, 0.0075799148f, -0.00172276752f, 0.00252837989f, -0.00393161374f, -0.0046258024f, -0.0076630604f, -0.0151212663f, -0.0117292911f, -0.0287317532f, -0.0142194401f, -0.047755955f, -0.0154688546f, -0.066326867f, -0.0147991566f, -0.0905380592f, -0.00927047971f, -0.109438307f, 0.00108161701f, -0.151400244f, 0.0513642553f, 0.943430875f, 0.0513642553f, -0.151400244f, 0.00108161701f, -0.109438307f, -0.00927047971f, -0.0905380592f, -0.0147991566f, -0.066326867f, -0.0154688546f, -0.047755955f, -0.0142194401f, -0.0287317532f, -0.0117292911f, -0.0151212663f, -0.0076630604f, -0.0046258024f, -0.00393161374f, 0.00252837989f, -0.00172276752f, 0.0075799148f, 0.000445182961f, 0.00861854437f, 0.00265428599f, 0.00852952321f, 0.00350936354f, 0.00793210273f, 0.00358785309f, 0.00688673525f, 0.00398811415f, 0.00505406994f, 0.0040934705f, 0.00399873933f, 0.00354046639f, 0.00310078538f, 0.00284592469f, 0.00242348987f, 0.00194604301f, 0.00181988843f, 0.000831279868f, 0.00124684058f, 7.65828629e-05f, 0.00011645718f, -0.000363205297f, -0.000803172645f, -0.000897670217f, -0.00147401359f, -0.00137156528f, -0.00184598531f, -0.00167096423f, -0.00200145368f, -0.00193567108f, -0.00179771599f, -0.0019741123f, -0.00173381102f, -0.00168521815f, -0.00161166413f, -0.00136930233f, -0.00140335019f, -0.00108103719f, -0.00115849348f, -0.000642467186f, -0.0010706276f, -0.000156787646f, -0.000819381925f },
    // ad_1_edd_3_model
    new float[] { -0.000819381925f, -0.000156787646f, -0.0010706276f, -0.000642467186f, -0.00115849348f, -0.00108103719f, -0.00140335019f, -0.00136930233f, -0.00161166413f, -0.00168521815f, -0.00173381102f, -0.0019741123f, -0.00179771599f, -0.00193567108f, -0.00200145368f, -0.00167096423f, -0.00184598531f, -0.00137156528f, -0.00147401359f, -0.000897670217f, -0.000803172645f, -0.000363205297f, 0.00011645718f, 7.65828629e-05f, 0.00124684058f, 0.000831279868f, 0.00181988843f, 0.00194604301f, 0.00242348987f, 0.00284592469f, 0.00310078538f, 0.00354046639f, 0.00399873933f, 0.0040934705f, 0.00505406994f, 0.00398811415f, 0.00688673525f, 0.00358785309f, 0.00793210273f, 0.00350936354f, 0.00852952321f, 0.00265428599f, 0.00861854437f, 0.000445182961f, 0.0075799148f, -0.00172276752f, 0.00252837989f, -0.00393161374f, -0.0046258024f, -0.0076630604f, -0.0151212663f, -0.0117292911f, -0.0287317532f, -0.0142194401f, -0.047755955f, -0.0154688546f, -0.066326867f, -0.0147991566f, -0.0905380592f, -0.00927047971f, -0.109438307f, 0.00108161701f, -0.151400244f, 0.0513642553f, 0.943430875f, 0.0513642553f, -0.151400244f, 0.00108161701f, -0.109438307f, -0.00927047971f, -0.0905380592f, -0.0147991566f, -0.066326867f, -0.0154688546f, -0.047755955f, -0.0142194401f, -0.0287317532f, -0.0117292911f, -0.0151212663f, -0.0076630604f, -0.0046258024f, -0.00393161374f, 0.00252837989f, -0.00172276752f, 0.0075799148f, 0.000445182961f, 0.00861854437f, 0.00265428599f, 0.00852952321f, 0.00350936354f, 0.00793210273f, 0.00358785309f, 0.00688673525f, 0.00398811415f, 0.00505406994f, 0.0040934705f, 0.00399873933f, 0.00354046639f, 0.00310078538f, 0.00284592469f, 0.00242348987f, 0.00194604301f, 0.00181988843f, 0.000831279868f, 0.00124684058f, 7.65828629e-05f, 0.00011645718f, -0.000363205297f, -0.000803172645f, -0.000897670217f, -0.00147401359f, -0.00137156528f, -0.00184598531f, -0.00167096423f, -0.00200145368f, -0.00193567108f, -0.00179771599f, -0.0019741123f, -0.00173381102f, -0.00168521815f, -0.00161166413f, -0.00136930233f, -0.00140335019f, -0.00108103719f, -0.00115849348f, -0.000642467186f, -0.0010706276f, -0.000156787646f, -0.000819381925f },
    // ad_5_edd_1_model
    new float[] { -0.000590927845f, -0.000454262191f, -0.00128644802f, -0.00137736085f, -0.00213616834f, -0.0022332874f, -0.00290415623f, -0.0029378688f, -0.00360404253f, -0.00341473075f, -0.00351581216f, -0.00360497547f, -0.00341535801f, -0.00344668964f, -0.00327111325f, -0.00288605761f, -0.00251789126f, -0.00230383719f, -0.00173996771f, -0.00137035267f, -0.000558594858f, 0.000251130366f, 0.00092180576f, 0.00171318452f, 0.00260136257f, 0.0032182768f, 0.00400607528f, 0.00465372627f, 0.0053492832f, 0.00535517098f, 0.00662857058f, 0.00635989411f, 0.00790941903f, 0.00711166535f, 0.00811765419f, 0.00652621696f, 0.00866095015f, 0.00529017749f, 0.00776381745f, 0.00338821884f, 0.00448722707f, 0.000927443746f, 0.00320951908f, -0.00202162231f, 0.00084589102f, -0.00518828991f, -0.00225862366f, -0.00815278516f, -0.00904966409f, -0.0105046226f, -0.0182818201f, -0.0136613879f, -0.0307161093f, -0.0202403199f, -0.0451038905f, -0.0241241621f, -0.0596551339f, -0.0265893848f, -0.0761848972f, -0.0323510612f, -0.0933584338f, -0.0119792979f, -0.1668018f, 0.117480179f, 0.930425418f, 0.117480179f, -0.1668018f, -0.0119792979f, -0.0933584338f, -0.0323510612f, -0.0761848972f, -0.0265893848f, -0.0596551339f, -0.0241241621f, -0.0451038905f, -0.0202403199f, -0.0307161093f, -0.0136613879f, -0.0182818201f, -0.0105046226f, -0.00904966409f, -0.00815278516f, -0.00225862366f, -0.00518828991f, 0.00084589102f, -0.00202162231f, 0.00320951908f, 0.000927443746f, 0.00448722707f, 0.00338821884f, 0.00776381745f, 0.00529017749f, 0.00866095015f, 0.00652621696f, 0.00811765419f, 0.00711166535f, 0.00790941903f, 0.00635989411f, 0.00662857058f, 0.00535517098f, 0.0053492832f, 0.00465372627f, 0.00400607528f, 0.0032182768f, 0.00260136257f, 0.00171318452f, 0.00092180576f, 0.000251130366f, -0.000558594858f, -0.00137035267f, -0.00173996771f, -0.00230383719f, -0.00251789126f, -0.00288605761f, -0.00327111325f, -0.00344668964f, -0.00341535801f, -0.00360497547f, -0.00351581216f, -0.00341473075f, -0.00360404253f, -0.0029378688f, -0.00290415623f, -0.0022332874f, -0.00213616834f, -0.00137736085f, -0.00128644802f, -0.000454262191f, -0.000590927845f },
    // ad_5_edd_2_model
    new float[] { -0.000590927845f, -0.000454262191f, -0.00128644802f, -0.00137736085f, -0.00213616834f, -0.0022332874f, -0.00290415623f, -0.0029378688f, -0.00360404253f, -0.00341473075f, -0.00351581216f, -0.00360497547f, -0.00341535801f, -0.00344668964f, -0.00327111325f, -0.00288605761f, -0.00251789126f, -0.00230383719f, -0.00173996771f, -0.00137035267f, -0.000558594858f, 0.000251130366f, 0.00092180576f, 0.00171318452f, 0.00260136257f, 0.0032182768f, 0.00400607528f, 0.00465372627f, 0.0053492832f, 0.00535517098f, 0.00662857058f, 0.00635989411f, 0.00790941903f, 0.00711166535f, 0.00811765419f, 0.00652621696f, 0.00866095015f, 0.00529017749f, 0.00776381745f, 0.00338821884f, 0.00448722707f, 0.000927443746f, 0.00320951908f, -0.00202162231f, 0.00084589102f, -0.00518828991f, -0.00225862366f, -0.00815278516f, -0.00904966409f, -0.0105046226f, -0.0182818201f, -0.0136613879f, -0.0307161093f, -0.0202403199f, -0.0451038905f, -0.0241241621f, -0.0596551339f, -0.0265893848f, -0.0761848972f, -0.0323510612f, -0.0933584338f, -0.0119792979f, -0.1668018f, 0.117480179f, 0.930425418f, 0.117480179f, -0.1668018f, -0.0119792979f, -0.0933584338f, -0.0323510612f, -0.0761848972f, -0.0265893848f, -0.0596551339f, -0.0241241621f, -0.0451038905f, -0.0202403199f, -0.0307161093f, -0.0136613879f, -0.0182818201f, -0.0105046226f, -0.00904966409f, -0.00815278516f, -0.00225862366f, -0.00518828991f, 0.00084589102f, -0.00202162231f, 0.00320951908f, 0.000927443746f, 0.00448722707f, 0.00338821884f, 0.00776381745f, 0.00529017749f, 0.00866095015f, 0.00652621696f, 0.00811765419f, 0.00711166535f, 0.00790941903f, 0.00635989411f, 0.00662857058f, 0.00535517098f, 0.0053492832f, 0.00465372627f, 0.00400607528f, 0.0032182768f, 0.00260136257f, 0.00171318452f, 0.00092180576f, 0.000251130366f, -0.000558594858f, -0.00137035267f, -0.00173996771f, -0.00230383719f, -0.00251789126f, -0.00288605761f, -0.00327111325f, -0.00344668964f, -0.00341535801f, -0.00360497547f, -0.00351581216f, -0.00341473075f, -0.00360404253f, -0.0029378688f, -0.00290415623f, -0.0022332874f, -0.00213616834f, -0.00137736085f, -0.00128644802f, -0.000454262191f, -0.000590927845f },
    // ad_5_edd_3_model
    new float[] { -0.000590927845f, -0.000454262191f, -0.00128644802f, -0.00137736085f, -0.00213616834f, -0.0022332874f, -0.00290415623f, -0.0029378688f, -0.00360404253f, -0.00341473075f, -0.00351581216f, -0.00360497547f, -0.00341535801f, -0.00344668964f, -0.00327111325f, -0.00288605761f, -0.00251789126f, -0.00230383719f, -0.00173996771f, -0.00137035267f, -0.000558594858f, 0.000251130366f, 0.00092180576f, 0.00171318452f, 0.00260136257f, 0.0032182768f, 0.00400607528f, 0.00465372627f, 0.0053492832f, 0.00535517098f, 0.00662857058f, 0.00635989411f, 0.00790941903f, 0.00711166535f, 0.00811765419f, 0.00652621696f, 0.00866095015f, 0.00529017749f, 0.00776381745f, 0.00338821884f, 0.00448722707f, 0.000927443746f, 0.00320951908f, -0.00202162231f, 0.00084589102f, -0.00518828991f, -0.00225862366f, -0.00815278516f, -0.00904966409f, -0.0105046226f, -0.0182818201f, -0.0136613879f, -0.0307161093f, -0.0202403199f, -0.0451038905f, -0.0241241621f, -0.0596551339f, -0.0265893848f, -0.0761848972f, -0.0323510612f, -0.0933584338f, -0.0119792979f, -0.1668018f, 0.117480179f, 0.930425418f, 0.117480179f, -0.1668018f, -0.0119792979f, -0.0933584338f, -0.0323510612f, -0.0761848972f, -0.0265893848f, -0.0596551339f, -0.0241241621f, -0.0451038905f, -0.0202403199f, -0.0307161093f, -0.0136613879f, -0.0182818201f, -0.0105046226f, -0.00904966409f, -0.00815278516f, -0.00225862366f, -0.00518828991f, 0.00084589102f, -0.00202162231f, 0.00320951908f, 0.000927443746f, 0.00448722707f, 0.00338821884f, 0.00776381745f, 0.00529017749f, 0.00866095015f, 0.00652621696f, 0.00811765419f, 0.00711166535f, 0.00790941903f, 0.00635989411f, 0.00662857058f, 0.00535517098f, 0.0053492832f, 0.00465372627f, 0.00400607528f, 0.0032182768f, 0.00260136257f, 0.00171318452f, 0.00092180576f, 0.000251130366f, -0.000558594858f, -0.00137035267f, -0.00173996771f, -0.00230383719f, -0.00251789126f, -0.00288605761f, -0.00327111325f, -0.00344668964f, -0.00341535801f, -0.00360497547f, -0.00351581216f, -0.00341473075f, -0.00360404253f, -0.0029378688f, -0.00290415623f, -0.0022332874f, -0.00213616834f, -0.00137736085f, -0.00128644802f, -0.000454262191f, -0.000590927845f },
    // ad_6_edd_1_model
    new float[] { -0.000468381717f, -0.000671682934f, -0.00127292156f, -0.00166597905f, -0.00213838597f, -0.00247727069f, -0.00290054764f, -0.00312764913f, -0.00364642189f, -0.00367443163f, -0.00375208028f, -0.00391360667f, -0.00373657937f, -0.00391774862f, -0.00381423811f, -0.00308268516f, -0.00255213524f, -0.00238099788f, -0.00197623389f, -0.00123503406f, -0.00055349589f, 0.000158815898f, 0.00100708571f, 0.00180219636f, 0.00258996702f, 0.00344935599f, 0.0042780543f, 0.00494117635f, 0.00572389477f, 0.00632520902f, 0.00671673061f, 0.00735403352f, 0.00848561454f, 0.00817083834f, 0.00732047091f, 0.00709944673f, 0.00824870474f, 0.00655631275f, 0.00734318472f, 0.00498158791f, 0.00459472191f, 0.00297230456f, 0.00310369737f, -0.000609162671f, 0.000137700859f, -0.0050937308f, -0.00350183206f, -0.00884520734f, -0.0094221329f, -0.0155831087f, -0.0178409349f, -0.0187817381f, -0.029461004f, -0.0280930993f, -0.0382703466f, -0.0349140603f, -0.0530390002f, -0.0388534029f, -0.0660896427f, -0.0489502355f, -0.0861042621f, -0.0148625382f, -0.18833659f, 0.175184873f, 0.904197602f, 0.175184873f, -0.18833659f, -0.0148625382f, -0.0861042621f, -0.0489502355f, -0.0660896427f, -0.0388534029f, -0.0530390002f, -0.0349140603f, -0.0382703466f, -0.0280930993f, -0.029461004f, -0.0187817381f, -0.0178409349f, -0.0155831087f, -0.0094221329f, -0.00884520734f, -0.00350183206f, -0.0050937308f, 0.000137700859f, -0.000609162671f, 0.00310369737f, 0.00297230456f, 0.00459472191f, 0.00498158791f, 0.00734318472f, 0.00655631275f, 0.00824870474f, 0.00709944673f, 0.00732047091f, 0.00817083834f, 0.00848561454f, 0.00735403352f, 0.00671673061f, 0.00632520902f, 0.00572389477f, 0.00494117635f, 0.0042780543f, 0.00344935599f, 0.00258996702f, 0.00180219636f, 0.00100708571f, 0.000158815898f, -0.00055349589f, -0.00123503406f, -0.00197623389f, -0.00238099788f, -0.00255213524f, -0.00308268516f, -0.00381423811f, -0.00391774862f, -0.00373657937f, -0.00391360667f, -0.00375208028f, -0.00367443163f, -0.00364642189f, -0.00312764913f, -0.00290054764f, -0.00247727069f, -0.00213838597f, -0.00166597905f, -0.00127292156f, -0.000671682934f, -0.000468381717f },
    // ad_6_edd_2_model
    new float[] { -0.000468381717f, -0.000671682934f, -0.00127292156f, -0.00166597905f, -0.00213838597f, -0.00247727069f, -0.00290054764f, -0.00312764913f, -0.00364642189f, -0.00367443163f, -0.00375208028f, -0.00391360667f, -0.00373657937f, -0.00391774862f, -0.00381423811f, -0.00308268516f, -0.00255213524f, -0.00238099788f, -0.00197623389f, -0.00123503406f, -0.00055349589f, 0.000158815898f, 0.00100708571f, 0.00180219636f, 0.00258996702f, 0.00344935599f, 0.0042780543f, 0.00494117635f, 0.00572389477f, 0.00632520902f, 0.00671673061f, 0.00735403352f, 0.00848561454f, 0.00817083834f, 0.00732047091f, 0.00709944673f, 0.00824870474f, 0.00655631275f, 0.00734318472f, 0.00498158791f, 0.00459472191f, 0.00297230456f, 0.00310369737f, -0.000609162671f, 0.000137700859f, -0.0050937308f, -0.00350183206f, -0.00884520734f, -0.0094221329f, -0.0155831087f, -0.0178409349f, -0.0187817381f, -0.029461004f, -0.0280930993f, -0.0382703466f, -0.0349140603f, -0.0530390002f, -0.0388534029f, -0.0660896427f, -0.0489502355f, -0.0861042621f, -0.0148625382f, -0.18833659f, 0.175184873f, 0.904197602f, 0.175184873f, -0.18833659f, -0.0148625382f, -0.0861042621f, -0.0489502355f, -0.0660896427f, -0.0388534029f, -0.0530390002f, -0.0349140603f, -0.0382703466f, -0.0280930993f, -0.029461004f, -0.0187817381f, -0.0178409349f, -0.0155831087f, -0.0094221329f, -0.00884520734f, -0.00350183206f, -0.0050937308f, 0.000137700859f, -0.000609162671f, 0.00310369737f, 0.00297230456f, 0.00459472191f, 0.00498158791f, 0.00734318472f, 0.00655631275f, 0.00824870474f, 0.00709944673f, 0.00732047091f, 0.00817083834f, 0.00848561454f, 0.00735403352f, 0.00671673061f, 0.00632520902f, 0.00572389477f, 0.00494117635f, 0.0042780543f, 0.00344935599f, 0.00258996702f, 0.00180219636f, 0.00100708571f, 0.000158815898f, -0.00055349589f, -0.00123503406f, -0.00197623389f, -0.00238099788f, -0.00255213524f, -0.00308268516f, -0.00381423811f, -0.00391774862f, -0.00373657937f, -0.00391360667f, -0.00375208028f, -0.00367443163f, -0.00364642189f, -0.00312764913f, -0.00290054764f, -0.00247727069f, -0.00213838597f, -0.00166597905f, -0.00127292156f, -0.000671682934f, -0.000468381717f },
    // ad_6_edd_3_model
    new float[] { -0.000468381717f, -0.000671682934f, -0.00127292156f, -0.00166597905f, -0.00213838597f, -0.00247727069f, -0.00290054764f, -0.00312764913f, -0.00364642189f, -0.00367443163f, -0.00375208028f, -0.00391360667f, -0.00373657937f, -0.00391774862f, -0.00381423811f, -0.00308268516f, -0.00255213524f, -0.00238099788f, -0.00197623389f, -0.00123503406f, -0.00055349589f, 0.000158815898f, 0.00100708571f, 0.00180219636f, 0.00258996702f, 0.00344935599f, 0.0042780543f, 0.00494117635f, 0.00572389477f, 0.00632520902f, 0.00671673061f, 0.00735403352f, 0.00848561454f, 0.00817083834f, 0.00732047091f, 0.00709944673f, 0.00824870474f, 0.00655631275f, 0.00734318472f, 0.00498158791f, 0.00459472191f, 0.00297230456f, 0.00310369737f, -0.000609162671f, 0.000137700859f, -0.0050937308f, -0.00350183206f, -0.00884520734f, -0.0094221329f, -0.0155831087f, -0.0178409349f, -0.0187817381f, -0.029461004f, -0.0280930993f, -0.0382703466f, -0.0349140603f, -0.0530390002f, -0.0388534029f, -0.0660896427f, -0.0489502355f, -0.0861042621f, -0.0148625382f, -0.18833659f, 0.175184873f, 0.904197602f, 0.175184873f, -0.18833659f, -0.0148625382f, -0.0861042621f, -0.0489502355f, -0.0660896427f, -0.0388534029f, -0.0530390002f, -0.0349140603f, -0.0382703466f, -0.0280930993f, -0.029461004f, -0.0187817381f, -0.0178409349f, -0.0155831087f, -0.0094221329f, -0.00884520734f, -0.00350183206f, -0.0050937308f, 0.000137700859f, -0.000609162671f, 0.00310369737f, 0.00297230456f, 0.00459472191f, 0.00498158791f, 0.00734318472f, 0.00655631275f, 0.00824870474f, 0.00709944673f, 0.00732047091f, 0.00817083834f, 0.00848561454f, 0.00735403352f, 0.00671673061f, 0.00632520902f, 0.00572389477f, 0.00494117635f, 0.0042780543f, 0.00344935599f, 0.00258996702f, 0.00180219636f, 0.00100708571f, 0.000158815898f, -0.00055349589f, -0.00123503406f, -0.00197623389f, -0.00238099788f, -0.00255213524f, -0.00308268516f, -0.00381423811f, -0.00391774862f, -0.00373657937f, -0.00391360667f, -0.00375208028f, -0.00367443163f, -0.00364642189f, -0.00312764913f, -0.00290054764f, -0.00247727069f, -0.00213838597f, -0.00166597905f, -0.00127292156f, -0.000671682934f, -0.000468381717f },
    // ad_7_edd_1_model
    new float[] { -0.000482335204f, -0.000815900148f, -0.00134309319f, -0.00164843863f, -0.00215258574f, -0.00245223269f, -0.002859583f, -0.00329549602f, -0.00383135651f, -0.00378484841f, -0.00379999632f, -0.00397578277f, -0.00398355632f, -0.00384025844f, -0.00355309627f, -0.00343559794f, -0.00296493777f, -0.0023840083f, -0.00192668189f, -0.00128487021f, -0.000436003644f, 0.000223938009f, 0.000907904373f, 0.00179075054f, 0.00257440655f, 0.00347923495f, 0.00422857244f, 0.00516998623f, 0.00606502031f, 0.00647288335f, 0.00708487311f, 0.00768518516f, 0.00790330246f, 0.00776303156f, 0.00854088729f, 0.0078457927f, 0.00798091852f, 0.00695509819f, 0.00773078251f, 0.0051777508f, 0.00414953664f, 0.00272756125f, 0.0039107977f, 0.00031191354f, 0.000324463109f, -0.00422109364f, -0.00446040769f, -0.0103415019f, -0.0101220285f, -0.0157822519f, -0.0202772423f, -0.0195635813f, -0.0314919239f, -0.0297271257f, -0.0372511257f, -0.0358690358f, -0.0503966128f, -0.0398428763f, -0.063429925f, -0.0584187887f, -0.0784224533f, -0.0211473585f, -0.193115097f, 0.194471789f, 0.894389195f, 0.194471789f, -0.193115097f, -0.0211473585f, -0.0784224533f, -0.0584187887f, -0.063429925f, -0.0398428763f, -0.0503966128f, -0.0358690358f, -0.0372511257f, -0.0297271257f, -0.0314919239f, -0.0195635813f, -0.0202772423f, -0.0157822519f, -0.0101220285f, -0.0103415019f, -0.00446040769f, -0.00422109364f, 0.000324463109f, 0.00031191354f, 0.0039107977f, 0.00272756125f, 0.00414953664f, 0.0051777508f, 0.00773078251f, 0.00695509819f, 0.00798091852f, 0.0078457927f, 0.00854088729f, 0.00776303156f, 0.00790330246f, 0.00768518516f, 0.00708487311f, 0.00647288335f, 0.00606502031f, 0.00516998623f, 0.00422857244f, 0.00347923495f, 0.00257440655f, 0.00179075054f, 0.000907904373f, 0.000223938009f, -0.000436003644f, -0.00128487021f, -0.00192668189f, -0.0023840083f, -0.00296493777f, -0.00343559794f, -0.00355309627f, -0.00384025844f, -0.00398355632f, -0.00397578277f, -0.00379999632f, -0.00378484841f, -0.00383135651f, -0.00329549602f, -0.002859583f, -0.00245223269f, -0.00215258574f, -0.00164843863f, -0.00134309319f, -0.000815900148f, -0.000482335204f },
    // ad_7_edd_2_model
    new float[] { -0.000482335204f, -0.000815900148f, -0.00134309319f, -0.00164843863f, -0.00215258574f, -0.00245223269f, -0.002859583f, -0.00329549602f, -0.00383135651f, -0.00378484841f, -0.00379999632f, -0.00397578277f, -0.00398355632f, -0.00384025844f, -0.00355309627f, -0.00343559794f, -0.00296493777f, -0.0023840083f, -0.00192668189f, -0.00128487021f, -0.000436003644f, 0.000223938009f, 0.000907904373f, 0.00179075054f, 0.00257440655f, 0.00347923495f, 0.00422857244f, 0.00516998623f, 0.00606502031f, 0.00647288335f, 0.00708487311f, 0.00768518516f, 0.00790330246f, 0.00776303156f, 0.00854088729f, 0.0078457927f, 0.00798091852f, 0.00695509819f, 0.00773078251f, 0.0051777508f, 0.00414953664f, 0.00272756125f, 0.0039107977f, 0.00031191354f, 0.000324463109f, -0.00422109364f, -0.00446040769f, -0.0103415019f, -0.0101220285f, -0.0157822519f, -0.0202772423f, -0.0195635813f, -0.0314919239f, -0.0297271257f, -0.0372511257f, -0.0358690358f, -0.0503966128f, -0.0398428763f, -0.063429925f, -0.0584187887f, -0.0784224533f, -0.0211473585f, -0.193115097f, 0.194471789f, 0.894389195f, 0.194471789f, -0.193115097f, -0.0211473585f, -0.0784224533f, -0.0584187887f, -0.063429925f, -0.0398428763f, -0.0503966128f, -0.0358690358f, -0.0372511257f, -0.0297271257f, -0.0314919239f, -0.0195635813f, -0.0202772423f, -0.0157822519f, -0.0101220285f, -0.0103415019f, -0.00446040769f, -0.00422109364f, 0.000324463109f, 0.00031191354f, 0.0039107977f, 0.00272756125f, 0.00414953664f, 0.0051777508f, 0.00773078251f, 0.00695509819f, 0.00798091852f, 0.0078457927f, 0.00854088729f, 0.00776303156f, 0.00790330246f, 0.00768518516f, 0.00708487311f, 0.00647288335f, 0.00606502031f, 0.00516998623f, 0.00422857244f, 0.00347923495f, 0.00257440655f, 0.00179075054f, 0.000907904373f, 0.000223938009f, -0.000436003644f, -0.00128487021f, -0.00192668189f, -0.0023840083f, -0.00296493777f, -0.00343559794f, -0.00355309627f, -0.00384025844f, -0.00398355632f, -0.00397578277f, -0.00379999632f, -0.00378484841f, -0.00383135651f, -0.00329549602f, -0.002859583f, -0.00245223269f, -0.00215258574f, -0.00164843863f, -0.00134309319f, -0.000815900148f, -0.000482335204f },
    // ad_7_edd_3_model
    new float[] { -0.000482335204f, -0.000815900148f, -0.00134309319f, -0.00164843863f, -0.00215258574f, -0.00245223269f, -0.002859583f, -0.00329549602f, -0.00383135651f, -0.00378484841f, -0.00379999632f, -0.00397578277f, -0.00398355632f, -0.00384025844f, -0.00355309627f, -0.00343559794f, -0.00296493777f, -0.0023840083f, -0.00192668189f, -0.00128487021f, -0.000436003644f, 0.000223938009f, 0.000907904373f, 0.00179075054f, 0.00257440655f, 0.00347923495f, 0.00422857244f, 0.00516998623f, 0.00606502031f, 0.00647288335f, 0.00708487311f, 0.00768518516f, 0.00790330246f, 0.00776303156f, 0.00854088729f, 0.0078457927f, 0.00798091852f, 0.00695509819f, 0.00773078251f, 0.0051777508f, 0.00414953664f, 0.00272756125f, 0.0039107977f, 0.00031191354f, 0.000324463109f, -0.00422109364f, -0.00446040769f, -0.0103415019f, -0.0101220285f, -0.0157822519f, -0.0202772423f, -0.0195635813f, -0.0314919239f, -0.0297271257f, -0.0372511257f, -0.0358690358f, -0.0503966128f, -0.0398428763f, -0.063429925f, -0.0584187887f, -0.0784224533f, -0.0211473585f, -0.193115097f, 0.194471789f, 0.894389195f, 0.194471789f, -0.193115097f, -0.0211473585f, -0.0784224533f, -0.0584187887f, -0.063429925f, -0.0398428763f, -0.0503966128f, -0.0358690358f, -0.0372511257f, -0.0297271257f, -0.0314919239f, -0.0195635813f, -0.0202772423f, -0.0157822519f, -0.0101220285f, -0.0103415019f, -0.00446040769f, -0.00422109364f, 0.000324463109f, 0.00031191354f, 0.0039107977f, 0.00272756125f, 0.00414953664f, 0.0051777508f, 0.00773078251f, 0.00695509819f, 0.00798091852f, 0.0078457927f, 0.00854088729f, 0.00776303156f, 0.00790330246f, 0.00768518516f, 0.00708487311f, 0.00647288335f, 0.00606502031f, 0.00516998623f, 0.00422857244f, 0.00347923495f, 0.00257440655f, 0.00179075054f, 0.000907904373f, 0.000223938009f, -0.000436003644f, -0.00128487021f, -0.00192668189f, -0.0023840083f, -0.00296493777f, -0.00343559794f, -0.00355309627f, -0.00384025844f, -0.00398355632f, -0.00397578277f, -0.00379999632f, -0.00378484841f, -0.00383135651f, -0.00329549602f, -0.002859583f, -0.00245223269f, -0.00215258574f, -0.00164843863f, -0.00134309319f, -0.000815900148f, -0.000482335204f },
    // ad_8_edd_1_model
    new float[] { -0.000236472295f, 0.00012057566f, -0.000615103538f, -0.000336154551f, -0.000780329313f, -0.000664308935f, -0.00108492671f, -0.000864515756f, -0.00135156574f, -0.000988475189f, -0.00130469897f, -0.00113859585f, -0.00107149905f, -0.00111115797f, -0.00110022866f, -0.000994368071f, -0.000620546977f, -0.000559769064f, -0.00069337666f, -0.000713331734f, 3.02304507e-05f, -4.21020011e-05f, 5.99977005e-05f, 0.000109612926f, 0.000462353901f, 0.000521878897f, 0.00106009176f, 0.00126256979f, 0.00220976314f, 0.00113551433f, 0.00224456822f, 0.00226668074f, 0.00354454477f, 0.00196005814f, 0.0033721673f, 0.00191411376f, 0.00412962195f, 0.001539656f, 0.00338162327f, 0.00133618336f, 0.00235622428f, 0.000419558029f, 0.00230716271f, -0.000721990208f, 0.00224215227f, -0.00114612993f, 0.000320549061f, 0.000774869904f, 0.000430127671f, 0.000694321527f, -0.00339431403f, -0.000510213904f, -0.0105571016f, -0.00488601416f, -0.021556767f, -0.00656263226f, -0.0392244188f, -0.0177135989f, -0.0647589519f, -0.0285823354f, -0.110184396f, -0.0368464795f, -0.223340262f, 0.0784505346f, 0.919597695f, 0.0784505346f, -0.223340262f, -0.0368464795f, -0.110184396f, -0.0285823354f, -0.0647589519f, -0.0177135989f, -0.0392244188f, -0.00656263226f, -0.021556767f, -0.00488601416f, -0.0105571016f, -0.000510213904f, -0.00339431403f, 0.000694321527f, 0.000430127671f, 0.000774869904f, 0.000320549061f, -0.00114612993f, 0.00224215227f, -0.000721990208f, 0.00230716271f, 0.000419558029f, 0.00235622428f, 0.00133618336f, 0.00338162327f, 0.001539656f, 0.00412962195f, 0.00191411376f, 0.0033721673f, 0.00196005814f, 0.00354454477f, 0.00226668074f, 0.00224456822f, 0.00113551433f, 0.00220976314f, 0.00126256979f, 0.00106009176f, 0.000521878897f, 0.000462353901f, 0.000109612926f, 5.99977005e-05f, -4.21020011e-05f, 3.02304507e-05f, -0.000713331734f, -0.00069337666f, -0.000559769064f, -0.000620546977f, -0.000994368071f, -0.00110022866f, -0.00111115797f, -0.00107149905f, -0.00113859585f, -0.00130469897f, -0.000988475189f, -0.00135156574f, -0.000864515756f, -0.00108492671f, -0.000664308935f, -0.000780329313f, -0.000336154551f, -0.000615103538f, 0.00012057566f, -0.000236472295f },
    // ad_8_edd_2_model
    new float[] { -0.000236472295f, 0.00012057566f, -0.000615103538f, -0.000336154551f, -0.000780329313f, -0.000664308935f, -0.00108492671f, -0.000864515756f, -0.00135156574f, -0.000988475189f, -0.00130469897f, -0.00113859585f, -0.00107149905f, -0.00111115797f, -0.00110022866f, -0.000994368071f, -0.000620546977f, -0.000559769064f, -0.00069337666f, -0.000713331734f, 3.02304507e-05f, -4.21020011e-05f, 5.99977005e-05f, 0.000109612926f, 0.000462353901f, 0.000521878897f, 0.00106009176f, 0.00126256979f, 0.00220976314f, 0.00113551433f, 0.00224456822f, 0.00226668074f, 0.00354454477f, 0.00196005814f, 0.0033721673f, 0.00191411376f, 0.00412962195f, 0.001539656f, 0.00338162327f, 0.00133618336f, 0.00235622428f, 0.000419558029f, 0.00230716271f, -0.000721990208f, 0.00224215227f, -0.00114612993f, 0.000320549061f, 0.000774869904f, 0.000430127671f, 0.000694321527f, -0.00339431403f, -0.000510213904f, -0.0105571016f, -0.00488601416f, -0.021556767f, -0.00656263226f, -0.0392244188f, -0.0177135989f, -0.0647589519f, -0.0285823354f, -0.110184396f, -0.0368464795f, -0.223340262f, 0.0784505346f, 0.919597695f, 0.0784505346f, -0.223340262f, -0.0368464795f, -0.110184396f, -0.0285823354f, -0.0647589519f, -0.0177135989f, -0.0392244188f, -0.00656263226f, -0.021556767f, -0.00488601416f, -0.0105571016f, -0.000510213904f, -0.00339431403f, 0.000694321527f, 0.000430127671f, 0.000774869904f, 0.000320549061f, -0.00114612993f, 0.00224215227f, -0.000721990208f, 0.00230716271f, 0.000419558029f, 0.00235622428f, 0.00133618336f, 0.00338162327f, 0.001539656f, 0.00412962195f, 0.00191411376f, 0.0033721673f, 0.00196005814f, 0.00354454477f, 0.00226668074f, 0.00224456822f, 0.00113551433f, 0.00220976314f, 0.00126256979f, 0.00106009176f, 0.000521878897f, 0.000462353901f, 0.000109612926f, 5.99977005e-05f, -4.21020011e-05f, 3.02304507e-05f, -0.000713331734f, -0.00069337666f, -0.000559769064f, -0.000620546977f, -0.000994368071f, -0.00110022866f, -0.00111115797f, -0.00107149905f, -0.00113859585f, -0.00130469897f, -0.000988475189f, -0.00135156574f, -0.000864515756f, -0.00108492671f, -0.000664308935f, -0.000780329313f, -0.000336154551f, -0.000615103538f, 0.00012057566f, -0.000236472295f },
    // ad_8_edd_3_model
    new float[] { -0.000236472295f, 0.00012057566f, -0.000615103538f, -0.000336154551f, -0.000780329313f, -0.000664308935f, -0.00108492671f, -0.000864515756f, -0.00135156574f, -0.000988475189f, -0.00130469897f, -0.00113859585f, -0.00107149905f, -0.00111115797f, -0.00110022866f, -0.000994368071f, -0.000620546977f, -0.000559769064f, -0.00069337666f, -0.000713331734f, 3.02304507e-05f, -4.21020011e-05f, 5.99977005e-05f, 0.000109612926f, 0.000462353901f, 0.000521878897f, 0.00106009176f, 0.00126256979f, 0.00220976314f, 0.00113551433f, 0.00224456822f, 0.00226668074f, 0.00354454477f, 0.00196005814f, 0.0033721673f, 0.00191411376f, 0.00412962195f, 0.001539656f, 0.00338162327f, 0.00133618336f, 0.00235622428f, 0.000419558029f, 0.00230716271f, -0.000721990208f, 0.00224215227f, -0.00114612993f, 0.000320549061f, 0.000774869904f, 0.000430127671f, 0.000694321527f, -0.00339431403f, -0.000510213904f, -0.0105571016f, -0.00488601416f, -0.021556767f, -0.00656263226f, -0.0392244188f, -0.0177135989f, -0.0647589519f, -0.0285823354f, -0.110184396f, -0.0368464795f, -0.223340262f, 0.0784505346f, 0.919597695f, 0.0784505346f, -0.223340262f, -0.0368464795f, -0.110184396f, -0.0285823354f, -0.0647589519f, -0.0177135989f, -0.0392244188f, -0.00656263226f, -0.021556767f, -0.00488601416f, -0.0105571016f, -0.000510213904f, -0.00339431403f, 0.000694321527f, 0.000430127671f, 0.000774869904f, 0.000320549061f, -0.00114612993f, 0.00224215227f, -0.000721990208f, 0.00230716271f, 0.000419558029f, 0.00235622428f, 0.00133618336f, 0.00338162327f, 0.001539656f, 0.00412962195f, 0.00191411376f, 0.0033721673f, 0.00196005814f, 0.00354454477f, 0.00226668074f, 0.00224456822f, 0.00113551433f, 0.00220976314f, 0.00126256979f, 0.00106009176f, 0.000521878897f, 0.000462353901f, 0.000109612926f, 5.99977005e-05f, -4.21020011e-05f, 3.02304507e-05f, -0.000713331734f, -0.00069337666f, -0.000559769064f, -0.000620546977f, -0.000994368071f, -0.00110022866f, -0.00111115797f, -0.00107149905f, -0.00113859585f, -0.00130469897f, -0.000988475189f, -0.00135156574f, -0.000864515756f, -0.00108492671f, -0.000664308935f, -0.000780329313f, -0.000336154551f, -0.000615103538f, 0.00012057566f, -0.000236472295f },
    // ad_9_edd_1_model
    new float[] { 0.000153144347f, 0.00029801394f, -6.23148958e-05f, -0.000184234486f, -0.00012807433f, -0.000178986994f, -0.000225243676f, -0.000293583442f, -0.000437921422f, -0.000135789931f, -0.0001217694f, -0.000211983477f, -9.0016069e-05f, -0.000213776505f, -7.74786073e-05f, -0.000277430108f, -0.000109700138f, -5.30449356e-05f, -0.000189265329f, -7.63046297e-05f, 7.22121875e-05f, 0.000199153379f, -0.000393669542f, -0.00019335712f, 0.000258497752f, -0.000176194296f, -0.000375657333f, 0.000505661779f, 0.000315156714f, 0.000343042617f, 6.57207868e-05f, 0.00064980479f, 0.000405783384f, 0.000325922096f, 0.000779521386f, 0.000528585923f, 0.00105761539f, 0.000426393082f, 0.00123182396f, 0.000486321273f, -0.000490761534f, -0.000922510549f, 0.000387004474f, -0.00115408759f, 0.00105490299f, -0.00239939875f, 0.00239829423f, 0.00359836888f, 0.00709471907f, 0.0059498363f, 0.00738565346f, 0.00403964061f, 0.00348115163f, 0.000739820211f, -0.00185790351f, -0.00120685439f, -0.0174245519f, -0.0169158181f, -0.0434703432f, -0.0412018734f, -0.10291493f, -0.0815802834f, -0.291798815f, 0.12853736f, 0.867985071f, 0.12853736f, -0.291798815f, -0.0815802834f, -0.10291493f, -0.0412018734f, -0.0434703432f, -0.0169158181f, -0.0174245519f, -0.00120685439f, -0.00185790351f, 0.000739820211f, 0.00348115163f, 0.00403964061f, 0.00738565346f, 0.0059498363f, 0.00709471907f, 0.00359836888f, 0.00239829423f, -0.00239939875f, 0.00105490299f, -0.00115408759f, 0.000387004474f, -0.000922510549f, -0.000490761534f, 0.000486321273f, 0.00123182396f, 0.000426393082f, 0.00105761539f, 0.000528585923f, 0.000779521386f, 0.000325922096f, 0.000405783384f, 0.00064980479f, 6.57207868e-05f, 0.000343042617f, 0.000315156714f, 0.000505661779f, -0.000375657333f, -0.000176194296f, 0.000258497752f, -0.00019335712f, -0.000393669542f, 0.000199153379f, 7.22121875e-05f, -7.63046297e-05f, -0.000189265329f, -5.30449356e-05f, -0.000109700138f, -0.000277430108f, -7.74786073e-05f, -0.000213776505f, -9.0016069e-05f, -0.000211983477f, -0.0001217694f, -0.000135789931f, -0.000437921422f, -0.000293583442f, -0.000225243676f, -0.000178986994f, -0.00012807433f, -0.000184234486f, -6.23148958e-05f, 0.00029801394f, 0.000153144347f },
    // ad_9_edd_2_model
    new float[] { 0.000153144347f, 0.00029801394f, -6.23148958e-05f, -0.000184234486f, -0.00012807433f, -0.000178986994f, -0.000225243676f, -0.000293583442f, -0.000437921422f, -0.000135789931f, -0.0001217694f, -0.000211983477f, -9.0016069e-05f, -0.000213776505f, -7.74786073e-05f, -0.000277430108f, -0.000109700138f, -5.30449356e-05f, -0.000189265329f, -7.63046297e-05f, 7.22121875e-05f, 0.000199153379f, -0.000393669542f, -0.00019335712f, 0.000258497752f, -0.000176194296f, -0.000375657333f, 0.000505661779f, 0.000315156714f, 0.000343042617f, 6.57207868e-05f, 0.00064980479f, 0.000405783384f, 0.000325922096f, 0.000779521386f, 0.000528585923f, 0.00105761539f, 0.000426393082f, 0.00123182396f, 0.000486321273f, -0.000490761534f, -0.000922510549f, 0.000387004474f, -0.00115408759f, 0.00105490299f, -0.00239939875f, 0.00239829423f, 0.00359836888f, 0.00709471907f, 0.0059498363f, 0.00738565346f, 0.00403964061f, 0.00348115163f, 0.000739820211f, -0.00185790351f, -0.00120685439f, -0.0174245519f, -0.0169158181f, -0.0434703432f, -0.0412018734f, -0.10291493f, -0.0815802834f, -0.291798815f, 0.12853736f, 0.867985071f, 0.12853736f, -0.291798815f, -0.0815802834f, -0.10291493f, -0.0412018734f, -0.0434703432f, -0.0169158181f, -0.0174245519f, -0.00120685439f, -0.00185790351f, 0.000739820211f, 0.00348115163f, 0.00403964061f, 0.00738565346f, 0.0059498363f, 0.00709471907f, 0.00359836888f, 0.00239829423f, -0.00239939875f, 0.00105490299f, -0.00115408759f, 0.000387004474f, -0.000922510549f, -0.000490761534f, 0.000486321273f, 0.00123182396f, 0.000426393082f, 0.00105761539f, 0.000528585923f, 0.000779521386f, 0.000325922096f, 0.000405783384f, 0.00064980479f, 6.57207868e-05f, 0.000343042617f, 0.000315156714f, 0.000505661779f, -0.000375657333f, -0.000176194296f, 0.000258497752f, -0.00019335712f, -0.000393669542f, 0.000199153379f, 7.22121875e-05f, -7.63046297e-05f, -0.000189265329f, -5.30449356e-05f, -0.000109700138f, -0.000277430108f, -7.74786073e-05f, -0.000213776505f, -9.0016069e-05f, -0.000211983477f, -0.0001217694f, -0.000135789931f, -0.000437921422f, -0.000293583442f, -0.000225243676f, -0.000178986994f, -0.00012807433f, -0.000184234486f, -6.23148958e-05f, 0.00029801394f, 0.000153144347f },
    // ad_9_edd_3_model
    new float[] { 0.000153144347f, 0.00029801394f, -6.23148958e-05f, -0.000184234486f, -0.00012807433f, -0.000178986994f, -0.000225243676f, -0.000293583442f, -0.000437921422f, -0.000135789931f, -0.0001217694f, -0.000211983477f, -9.0016069e-05f, -0.000213776505f, -7.74786073e-05f, -0.000277430108f, -0.000109700138f, -5.30449356e-05f, -0.000189265329f, -7.63046297e-05f, 7.22121875e-05f, 0.000199153379f, -0.000393669542f, -0.00019335712f, 0.000258497752f, -0.000176194296f, -0.000375657333f, 0.000505661779f, 0.000315156714f, 0.000343042617f, 6.57207868e-05f, 0.00064980479f, 0.000405783384f, 0.000325922096f, 0.000779521386f, 0.000528585923f, 0.00105761539f, 0.000426393082f, 0.00123182396f, 0.000486321273f, -0.000490761534f, -0.000922510549f, 0.000387004474f, -0.00115408759f, 0.00105490299f, -0.00239939875f, 0.00239829423f, 0.00359836888f, 0.00709471907f, 0.0059498363f, 0.00738565346f, 0.00403964061f, 0.00348115163f, 0.000739820211f, -0.00185790351f, -0.00120685439f, -0.0174245519f, -0.0169158181f, -0.0434703432f, -0.0412018734f, -0.10291493f, -0.0815802834f, -0.291798815f, 0.12853736f, 0.867985071f, 0.12853736f, -0.291798815f, -0.0815802834f, -0.10291493f, -0.0412018734f, -0.0434703432f, -0.0169158181f, -0.0174245519f, -0.00120685439f, -0.00185790351f, 0.000739820211f, 0.00348115163f, 0.00403964061f, 0.00738565346f, 0.0059498363f, 0.00709471907f, 0.00359836888f, 0.00239829423f, -0.00239939875f, 0.00105490299f, -0.00115408759f, 0.000387004474f, -0.000922510549f, -0.000490761534f, 0.000486321273f, 0.00123182396f, 0.000426393082f, 0.00105761539f, 0.000528585923f, 0.000779521386f, 0.000325922096f, 0.000405783384f, 0.00064980479f, 6.57207868e-05f, 0.000343042617f, 0.000315156714f, 0.000505661779f, -0.000375657333f, -0.000176194296f, 0.000258497752f, -0.00019335712f, -0.000393669542f, 0.000199153379f, 7.22121875e-05f, -7.63046297e-05f, -0.000189265329f, -5.30449356e-05f, -0.000109700138f, -0.000277430108f, -7.74786073e-05f, -0.000213776505f, -9.0016069e-05f, -0.000211983477f, -0.0001217694f, -0.000135789931f, -0.000437921422f, -0.000293583442f, -0.000225243676f, -0.000178986994f, -0.00012807433f, -0.000184234486f, -6.23148958e-05f, 0.00029801394f, 0.000153144347f },
};

    private sealed class CodecMunge : IDisposable {
        private readonly int _codec;
        private readonly int _rbsPattern;
        private int _sequence = 0;
        private readonly g726_state_t? _encoder;
        private readonly g726_state_t? _decoder;
        private readonly short[] _sample = new short[1];
        private readonly byte[] _encoded = new byte[2];

        public CodecMunge(int codec, int rbsPattern) {
            _codec = codec;
            _rbsPattern = rbsPattern;
            int rate = codec switch {
                MUNGE_CODEC_G726_40K => 40000,
                MUNGE_CODEC_G726_32K => 32000,
                MUNGE_CODEC_G726_24K => 24000,
                MUNGE_CODEC_G726_16K => 16000,
                _ => 0
            };
            if (rate != 0) {
                _encoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                    ?? throw new InvalidOperationException("Unable to initialize the G.726 encoder.");
                _decoder = g726_init(null, rate, G726_ENCODING_LINEAR, G726_PACKING_NONE)
                    ?? throw new InvalidOperationException("Unable to initialize the G.726 decoder.");
            }
        }

        public short Process(short value) {
            switch (_codec) {
                case MUNGE_CODEC_NONE:
                    return value;
                case MUNGE_CODEC_ALAW:
                    return alaw_to_linear(linear_to_alaw(value));
                case MUNGE_CODEC_ULAW: {
                        byte law = linear_to_ulaw(value);
                        if ((_rbsPattern & (1 << _sequence)) != 0) law &= 0xFE;
                        return ulaw_to_linear(law);
                    }
                case MUNGE_CODEC_G726_40K:
                case MUNGE_CODEC_G726_32K:
                case MUNGE_CODEC_G726_24K:
                case MUNGE_CODEC_G726_16K:
                    _sample[0] = value;
                    int bytes = g726_encode(_encoder!, _encoded, _sample, 1);
                    _sample[0] = 0;
                    _ = g726_decode(_decoder!, _sample, _encoded, bytes);
                    return _sample[0];
                default:
                    throw new InvalidOperationException($"Unknown SpanDSP line-model codec: {_codec}.");
            }
        }

        public void Dispose() {
            if (_encoder is not null) g726_free(_encoder);
            if (_decoder is not null) g726_free(_decoder);
        }
    }

    private sealed class LineDirection : IDisposable {
        private readonly float[] _filter;
        private readonly float[] _nearBuffer = new float[129];
        private readonly float[] _farBuffer = new float[129];
        private readonly float[] _bulkDelay = new float[8];
        private int _nearPointer;
        private int _farPointer;
        private int _bulkPointer;
        private readonly Awgn _nearNoise;
        private readonly Awgn _farNoise;
        private readonly CodecMunge _munge;

        public LineDirection(int model, float noise, int seed, float cpeEcho, float coEcho, int codec, int rbsPattern) {
            if ((uint)model >= (uint)LineModels.Length)
                throw new ArgumentOutOfRangeException(nameof(model), model, "SpanDSP line model must be in the range 0..19.");
            _filter = LineModels[model];
            _nearNoise = new Awgn(seed, noise - 3.02f);
            _farNoise = new Awgn(seed, noise - 3.02f);
            _munge = new CodecMunge(codec, rbsPattern);
            NearCoHybridEcho = MathF.Pow(10.0f, coEcho / 20.0f);
            FarCpeHybridEcho = MathF.Pow(10.0f, cpeEcho / 20.0f);
        }

        public float NearCoHybridEcho { get; }
        public float FarCpeHybridEcho { get; }

        public float FilterNear(float value) => Filter(value, _nearBuffer, ref _nearPointer) + _nearNoise.NextSample();
        public float FilterFar(float value) => Filter(value, _farBuffer, ref _farPointer) + _farNoise.NextSample();
        public short Munge(float value) => _munge.Process(Saturate(value));

        public float Delay(float value) {
            float output = _bulkDelay[_bulkPointer];
            _bulkDelay[_bulkPointer] = value;
            if (++_bulkPointer >= _bulkDelay.Length) _bulkPointer = 0;
            return output;
        }

        private float Filter(float value, float[] buffer, ref int pointer) {
            int p = pointer;
            buffer[p] = value;
            if (++p == buffer.Length) p = 0;
            pointer = p;
            float sum = 0.0f;
            for (int i = 0; i < _filter.Length; i++) {
                sum += _filter[i] * buffer[p];
                if (++p == buffer.Length) p = 0;
            }
            return sum;
        }

        public void Dispose() => _munge.Dispose();
    }

    private sealed class OneWayLineModelState : IDisposable {
        private readonly LineDirection _line;
        public OneWayLineModelState(int model, float noise, int codec, int rbsPattern)
            => _line = new LineDirection(model, noise, 1234567, -99.0f, -99.0f, codec, rbsPattern);

        public void Process(short[] output, short[] input, int samples) {
            for (int i = 0; i < samples; i++) {
                float value = _line.FilterNear(input[i]);
                value = _line.Munge(value);
                value = _line.Delay(value);
                value = _line.FilterFar(value);
                output[i] = Saturate(value);
            }
        }
        public void Dispose() => _line.Dispose();
    }

    private sealed class BothWaysLineModelState : IDisposable {
        private readonly LineDirection _line1;
        private readonly LineDirection _line2;
        private float _fout1;
        private float _fout2;

        public BothWaysLineModelState(int model1, float noise1, float cpe1, float co1,
                                      int model2, float noise2, float cpe2, float co2,
                                      int codec, int rbsPattern) {
            _line1 = new LineDirection(model1, noise1, 1234567, cpe1, co1, codec, rbsPattern);
            _line2 = new LineDirection(model2, noise2, 7654321, cpe2, co2, codec, rbsPattern);
        }

        public void Process(short[] output1, short[] input1, short[] output2, short[] input2, int samples) {
            for (int i = 0; i < samples; i++) {
                float in1 = input1[i];
                float in2 = input2[i];
                float tmp1 = in1 + _fout2 * _line1.NearCoHybridEcho;
                float tmp2 = in2 + _fout1 * _line2.NearCoHybridEcho;
                _fout1 = _line1.FilterNear(tmp1);
                _fout2 = _line2.FilterNear(tmp2);
                _fout1 = _line1.Munge(_fout1);
                _fout2 = _line2.Munge(_fout2);
                float out1 = _line1.Delay(_fout1) + in2 * _line1.FarCpeHybridEcho;
                float out2 = _line2.Delay(_fout2) + in1 * _line2.FarCpeHybridEcho;
                output1[i] = Saturate(_line1.FilterFar(out1));
                output2[i] = Saturate(_line2.FilterFar(out2));
            }
        }

        public void Dispose() { _line1.Dispose(); _line2.Dispose(); }
    }

    private static short Saturate(float value)
        => value >= short.MaxValue ? short.MaxValue : value <= short.MinValue ? short.MinValue : (short)MathF.Round(value);

}
