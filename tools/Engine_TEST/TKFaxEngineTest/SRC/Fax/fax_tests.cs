/*
 * TKFaxEngine - managed C# port
 *
 * fax_tests.cs - Tests for the audio and T.38 FAX modules.
 *
 * Original implementation written by Steve Underwood.
 * Copyright (C) 2005, 2006, 2009, 2010 Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 *
 * Fresh port from fax_tests.c. No earlier managed fax_tests implementation
 * was used. The chain parser, endpoint setup, T.30 information checks,
 * audio/T.38 streaming loop, packet repetition, TSB-85 integration,
 * transfer verification and T.33 round-trip tests follow the native test.
 */

#nullable enable
using FaxState = TKFaxEngine.FaxState;

using BitMiracle.LibTiff.Classic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngine.Daten.T30;
using TKFaxEngine.Daten.T38;
using TKFaxEngine.FaxImage;
using static TKFaxEngine.Daten.T30.T30;
using static TKFaxEngine.Daten.T30.T30Api;
using static TKFaxEngine.Daten.T30.T30Logging;
using static TKFaxEngine.Daten.T38.T38Core;
using static TKFaxEngine.Daten.T38.T38Gateway;
using static TKFaxEngine.Daten.T38.T38Terminal;
using static TKFaxEngine.FaxApi;
using static TKFaxEngine.FaxImage.ImageTranslateFitPadApi;
using static TKFaxEngine.FaxImage.t4_rx;
using static TKFaxEngine.FaxImage.t4_tx;
using ProtocolT30SupportedFeatures = TKFaxEngine.Daten.T30.T30SupportedFeatures;
using ProtocolT30SupportedModems = TKFaxEngine.Daten.T30.T30SupportedModems;

namespace TKFaxEngineTest.Fax.Test;

/// <summary>
/// Supplies the G.1050 network path used by the native test. Audio FAX,
/// T.38 terminal and gateway states are created directly by TKFaxEngine.
/// </summary>
public interface IFaxTestsRuntime {
    IFaxTestsG1050Path CreateG1050Path(
        int modelNumber,
        int speedPatternNumber,
        int packetSize,
        int packetsPerSecond);
}

/// <summary>Managed contract for the external spanDSP G.1050 module.</summary>
public interface IFaxTestsG1050Path : IDisposable {
    int Put(ReadOnlySpan<byte> packet, int sequenceNumber, double departureTime);

    bool TryGet(
        double currentTime,
        Span<byte> destination,
        out int length,
        out int sequenceNumber,
        out double departureTime,
        out double arrivalTime);
}

public static class fax_tests {
    public const string InputTiffFileName = "Test-Files/itu/fax/itutests.tif";
    public const string OutputTiffFileName = "fax_tests.tif";
    public const string PreparedTransmitTiffFileName = "fax_tests_tx_prepared.tif";
    public const string InputWaveFileName = "fax_cap.wav";
    public const string OutputWaveFileName = "fax_tests.wav";

    private const int SamplesPerChunk = 160;
    private const int SampleRate = 8_000;
    private const int MaximumChainElements = 7;
    private const int MaximumRuntimeLoops = 4_000_000;

    private static readonly ChainElement[] Chain =
    [
        .. Enumerable.Range(0, MaximumChainElements)
            .Select(static _ => new ChainElement())
    ];

    private static int chainElements;
    private static bool t38SimulateIncrementingRepeats;
    private static bool useReceiverNotReady = false;
    private static bool testLocalInterrupt = false;
    private static double when;

    /// <summary>
    /// Concrete linked-module binding. It must be assigned by the test runner
    /// before executing a path which contains AUDIO_FAX, T38_FAX, a gateway or
    /// a T.38 packet path.
    /// </summary>
    public static IFaxTestsRuntime? Runtime { get; set; }

    public static int main(string[] testArguments) {
        try {
            return tkfx_fax_tests_body_main(testArguments);
        } catch (FaxTestsExitException ex) {
            return ex.ExitCode;
        }
    }

    public static int tkfx_fax_tests_body_main(string[] testArguments) {
        Options options = ParseOptions(testArguments);

        if (options.CompletionCodeLookup >= 0) {
            Console.WriteLine(
                $"Result code {options.CompletionCodeLookup} is " +
                t30_completion_code_to_str(options.CompletionCodeLookup));
            return 0;
        }

        ResetChain(options.Path);
        ValidateInputDocument(options);
        PrepareTransmitDocument(options);

        Console.WriteLine($"Using T.38 version {options.T38Version}");
        if (options.UseEcm)
            Console.WriteLine("Using ECM");

        Pcm16WaveWriter? audioLog = options.LogAudio
            ? new Pcm16WaveWriter(OutputWaveFileName, 4, SampleRate)
            : null;

        try {
            SetupNodes(options);
            ConfigureNodes(options);
            ConnectTesterAndPassthroughNodes();
            ConfigureDocuments(options);
            RunStreamingLoop(options, audioLog);
            VerifyTransfer(options);
            T33Tests();
            Console.WriteLine("Tests passed");
            return 0;
        } finally {
            audioLog?.Dispose();
            ReleaseNodes();
        }
    }

    private static void ResetChain(string? path) {
        foreach (ChainElement element in Chain)
            element.Reset();

        if (string.IsNullOrWhiteSpace(path)) {
            chainElements = 2;
            Chain[0].NodeType = NodeType.AudioFax;
            Chain[1].NodeType = NodeType.AudioFax;
        } else {
            string[] elements = path.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (elements.Length is < 2 or > MaximumChainElements)
                Fail($"Invalid FAX path element count {elements.Length}.");

            chainElements = elements.Length;
            for (int i = 0; i < elements.Length; i++)
                Chain[i].NodeType = ParseNodeType(elements[i]);
        }

        Chain[0].Peer = chainElements - 1;
        Chain[chainElements - 1].Peer = 0;
        for (int i = 0; i < chainElements; i++)
            Chain[i].Tag = ((char)('A' + i)).ToString(CultureInfo.InvariantCulture);

        Console.Error.WriteLine(
            $"FAX_TEST START chain={string.Join("<->", Chain.Take(chainElements).Select(static x => NodeTypeToString(x.NodeType)))}");
    }

    private static NodeType ParseNodeType(string value) => value switch {
        "FAX" => NodeType.AudioFax,
        "T38" => NodeType.T38Fax,
        "T31" => NodeType.T31AudioFax,
        "tester" => NodeType.Tsb85AudioFax,
        "replay" => NodeType.ReplayAudioFax,
        "T38gateway" => NodeType.AudioToT38Gateway,
        "passthrough" => NodeType.Passthrough,
        _ => throw new FaxTestsExitException(2, $"Unknown FAX path element '{value}'.")
    };

    private static string NodeTypeToString(NodeType type) => type switch {
        NodeType.AudioFax => "AUDIO_FAX",
        NodeType.T38Fax => "T38_FAX",
        NodeType.T31AudioFax => "T31_AUDIO_FAX",
        NodeType.T31T38Fax => "T31_T38_FAX",
        NodeType.Tsb85AudioFax => "TSB85_AUDIO_FAX",
        NodeType.Tsb85T38Fax => "TSB85_T38_FAX",
        NodeType.ReplayAudioFax => "REPLAY_AUDIO_FAX",
        NodeType.ReplayT38Fax => "REPLAY_T38_FAX",
        NodeType.AudioToT38Gateway => "AUDIO_TO_T38_GATEWAY",
        NodeType.Passthrough => "FAX_NODE_PASSTHROUGH",
        NodeType.AudioChannel => "AUDIO_CHAN",
        NodeType.T38Channel => "T38_CHAN",
        _ => "UNKNOWN"
    };

    private static void ValidateInputDocument(Options options) {
        int source = options.UsePolledMode ? chainElements - 1 : 0;
        if (Chain[source].NodeType is not (NodeType.AudioFax or NodeType.T38Fax)) {
            Console.Error.WriteLine(
                $"FAX_TEST INPUT TIFF not used by source node={source} type={NodeTypeToString(Chain[source].NodeType)}");
            return;
        }

        if (!File.Exists(options.InputTiffFile))
            Fail($"FAX_TEST FAIL: input TIFF cannot be opened: {options.InputTiffFile}");

        int pages = GetTiffTotalPages(options.InputTiffFile);
        if (pages <= 0)
            Fail($"FAX_TEST FAIL: input TIFF has no readable pages: {options.InputTiffFile} pages={pages}");

        Console.Error.WriteLine(
            $"FAX_TEST INPUT TIFF node={source} path={options.InputTiffFile} pages={pages}");
    }

    /// <summary>
    /// Creates a sender-side TIFF whose pages all match the first page selected
    /// for transmission. The optional image_translate fit-and-pad API performs
    /// proportional scaling and white padding before T.30 opens the document.
    /// The original source TIFF and the T.4 transmitter remain unchanged.
    /// </summary>
    private static void PrepareTransmitDocument(Options options) {
        int transmitter = options.UsePolledMode ? chainElements - 1 : 0;
        if (Chain[transmitter].NodeType is not (NodeType.AudioFax or NodeType.T38Fax))
            return;

        string outputRoot = Environment.GetEnvironmentVariable("TKFAXENGINE_TEST_OUTPUT_DIR") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(outputRoot)) {
            Fail(
                "FAX_TEST FAIL: TKFAXENGINE_TEST_OUTPUT_DIR is not set; " +
                "the prepared transmit document must be written into the current run folder.");
        }

        string sourcePath = Path.GetFullPath(options.InputTiffFile);
        string outputDirectory = Path.Combine(outputRoot, "fax_tests");
        string preparedPath = Path.Combine(outputDirectory, PreparedTransmitTiffFileName);

        try {
            Directory.CreateDirectory(outputDirectory);
            if (File.Exists(preparedPath))
                File.Delete(preparedPath);

            using Tiff input = Tiff.Open(sourcePath, "r")
                ?? throw new InvalidDataException($"Cannot open input TIFF '{sourcePath}'.");

            int pageCount = input.NumberOfDirectories();
            if (pageCount <= 0)
                throw new InvalidDataException($"Input TIFF '{sourcePath}' contains no pages.");

            int targetPage = options.StartPage >= 0 ? options.StartPage : 0;
            if (targetPage < 0 || targetPage >= pageCount) {
                throw new InvalidDataException(
                    $"Selected start page {targetPage} is outside the TIFF page range 0..{pageCount - 1}.");
            }

            if (!input.SetDirectory(checked((short)targetPage))) {
                throw new InvalidDataException(
                    $"Cannot select target TIFF page {targetPage} in '{sourcePath}'.");
            }

            FaxTiffPageInfo target = ReadFaxTiffPageInfo(input, targetPage, sourcePath);
            ValidateBilevelFaxPage(target, targetPage, sourcePath);

            using Tiff output = Tiff.Open(preparedPath, "w")
                ?? throw new IOException($"Cannot create prepared TIFF '{preparedPath}'.");

            Console.Error.WriteLine(
                $"FAX_TEST TX PREPARE source={sourcePath} output={preparedPath} " +
                $"target_page={targetPage} target={target.Width}x{target.Length} " +
                $"resolution={target.XResolution.ToString(CultureInfo.InvariantCulture)}x" +
                $"{target.YResolution.ToString(CultureInfo.InvariantCulture)} " +
                "fit_width=1 fit_height=1");

            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++) {
                if (!input.SetDirectory(checked((short)pageIndex))) {
                    throw new InvalidDataException(
                        $"Cannot select TIFF page {pageIndex} in '{sourcePath}'.");
                }

                FaxTiffPageInfo page = ReadFaxTiffPageInfo(input, pageIndex, sourcePath);
                ValidateBilevelFaxPage(page, pageIndex, sourcePath);

                var rowSource = new BilevelTiffRowSource(
                    input,
                    page.Width,
                    page.Length,
                    page.Photometric,
                    page.FillOrder);

                ImageTranslateFitPadState? translate = image_translate_fit_and_pad_init(
                    null,
                    (int)ImageTranslateFormat.Bilevel,
                    target.Width,
                    target.Length,
                    1,
                    1,
                    (int)ImageTranslateFormat.Gray8Bit,
                    page.Width,
                    page.Length,
                    ReadBilevelTiffRowAsGray8,
                    rowSource);
                if (translate is null) {
                    throw new InvalidOperationException(
                        $"image_translate_fit_and_pad_init failed for TIFF page {pageIndex}.");
                }

                try {
                    SetPreparedFaxTiffPageFields(
                        output,
                        target,
                        pageIndex,
                        pageCount,
                        sourcePath);

                    int outputRowBytes = checked((target.Width + 7) / 8);
                    byte[] outputRow = new byte[outputRowBytes];
                    int contentWidth = image_translate_fit_and_pad_get_content_width(translate);
                    int contentLength = image_translate_fit_and_pad_get_content_length(translate);

                    Console.Error.WriteLine(
                        $"FAX_TEST TX PREPARE PAGE page={pageIndex} " +
                        $"source={page.Width}x{page.Length} " +
                        $"content={contentWidth}x{contentLength} " +
                        $"output={target.Width}x{target.Length}");

                    for (int row = 0; row < target.Length; row++) {
                        int translated = image_translate_fit_and_pad_row(
                            translate,
                            outputRow,
                            outputRow.Length);
                        if (translated != outputRowBytes) {
                            throw new InvalidDataException(
                                $"image_translate returned {translated} bytes for page {pageIndex}, " +
                                $"row {row}; expected {outputRowBytes}.");
                        }

                        if (!output.WriteScanline(outputRow, row, 0)) {
                            throw new IOException(
                                $"Cannot write prepared TIFF page {pageIndex}, row {row}, " +
                                $"file '{preparedPath}'.");
                        }
                    }

                    if (image_translate_fit_and_pad_row(
                            translate,
                            outputRow,
                            outputRow.Length) != 0) {
                        throw new InvalidDataException(
                            $"image_translate produced excess rows for TIFF page {pageIndex}.");
                    }

                    if (!output.WriteDirectory()) {
                        throw new IOException(
                            $"Cannot write prepared TIFF directory for page {pageIndex}, " +
                            $"file '{preparedPath}'.");
                    }
                } finally {
                    _ = image_translate_fit_and_pad_free(translate);
                }
            }

            options.PreparedInputTiffFile = preparedPath;
            Console.Error.WriteLine(
                $"FAX_TEST TX PREPARED path={preparedPath} pages={pageCount}");
        } catch (FaxTestsExitException) {
            throw;
        } catch (Exception ex) when (ex is IOException or
                                     UnauthorizedAccessException or
                                     InvalidDataException or
                                     InvalidOperationException or
                                     ArgumentException or
                                     OverflowException) {
            Fail($"FAX_TEST FAIL: transmit document preparation failed: {ex.Message}");
        }
    }

    private static FaxTiffPageInfo ReadFaxTiffPageInfo(
        Tiff input,
        int pageIndex,
        string sourcePath) {
        int width = GetTiffIntField(input, TiffTag.IMAGEWIDTH, 0);
        int length = GetTiffIntField(input, TiffTag.IMAGELENGTH, 0);
        ushort bitsPerSample = GetTiffUShortField(input, TiffTag.BITSPERSAMPLE, 1);
        ushort samplesPerPixel = GetTiffUShortField(input, TiffTag.SAMPLESPERPIXEL, 1);
        Photometric photometric = (Photometric)GetTiffUShortField(
            input,
            TiffTag.PHOTOMETRIC,
            (ushort)Photometric.MINISWHITE);
        FillOrder fillOrder = (FillOrder)GetTiffUShortField(
            input,
            TiffTag.FILLORDER,
            (ushort)FillOrder.MSB2LSB);
        float xResolution = GetTiffFloatField(input, TiffTag.XRESOLUTION, 204.0f);
        float yResolution = GetTiffFloatField(input, TiffTag.YRESOLUTION, 196.0f);
        ResUnit resolutionUnit = (ResUnit)GetTiffUShortField(
            input,
            TiffTag.RESOLUTIONUNIT,
            (ushort)ResUnit.INCH);

        if (width <= 0 || length <= 0) {
            throw new InvalidDataException(
                $"Invalid TIFF geometry on page {pageIndex} in '{sourcePath}': " +
                $"{width}x{length}.");
        }

        return new FaxTiffPageInfo(
            width,
            length,
            bitsPerSample,
            samplesPerPixel,
            photometric,
            fillOrder,
            xResolution,
            yResolution,
            resolutionUnit);
    }

    private static void ValidateBilevelFaxPage(
        FaxTiffPageInfo page,
        int pageIndex,
        string sourcePath) {
        if (page.BitsPerSample != 1 || page.SamplesPerPixel != 1) {
            throw new InvalidDataException(
                $"TIFF page {pageIndex} in '{sourcePath}' is not a 1-bit single-sample fax page: " +
                $"bits={page.BitsPerSample}, samples={page.SamplesPerPixel}.");
        }

        if (page.Photometric is not (Photometric.MINISWHITE or Photometric.MINISBLACK)) {
            throw new InvalidDataException(
                $"TIFF page {pageIndex} in '{sourcePath}' has unsupported photometric " +
                $"value {(int)page.Photometric}.");
        }

        if (page.FillOrder is not (FillOrder.MSB2LSB or FillOrder.LSB2MSB)) {
            throw new InvalidDataException(
                $"TIFF page {pageIndex} in '{sourcePath}' has unsupported fill order " +
                $"{(int)page.FillOrder}.");
        }
    }

    private static int ReadBilevelTiffRowAsGray8(
        object? userData,
        Span<byte> destination,
        int length) {
        if (userData is not BilevelTiffRowSource source ||
            source.Row >= source.Length ||
            length < source.Width ||
            destination.Length < source.Width) {
            return 0;
        }

        if (!source.Tiff.ReadScanline(source.PackedRow, source.Row, 0))
            return 0;

        for (int x = 0; x < source.Width; x++) {
            int bitIndex = source.FillOrder == FillOrder.LSB2MSB
                ? x & 7
                : 7 - (x & 7);
            int bit = (source.PackedRow[x >> 3] >> bitIndex) & 1;
            bool black = source.Photometric == Photometric.MINISWHITE
                ? bit != 0
                : bit == 0;
            destination[x] = black ? (byte)0 : byte.MaxValue;
        }

        source.Row++;
        return source.Width;
    }

    private static void SetPreparedFaxTiffPageFields(
        Tiff output,
        FaxTiffPageInfo target,
        int pageIndex,
        int pageCount,
        string sourcePath) {
        output.SetField(TiffTag.COMPRESSION, Compression.CCITTFAX4);
        output.SetField(TiffTag.FAXMODE, FaxMode.CLASSF);
        output.SetField(TiffTag.IMAGEWIDTH, target.Width);
        output.SetField(TiffTag.IMAGELENGTH, target.Length);
        output.SetField(TiffTag.BITSPERSAMPLE, 1);
        output.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        output.SetField(TiffTag.ORIENTATION, Orientation.TOPLEFT);
        output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        output.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISWHITE);
        output.SetField(TiffTag.FILLORDER, FillOrder.MSB2LSB);
        output.SetField(TiffTag.XRESOLUTION, target.XResolution);
        output.SetField(TiffTag.YRESOLUTION, target.YResolution);
        output.SetField(TiffTag.RESOLUTIONUNIT, target.ResolutionUnit);
        output.SetField(TiffTag.ROWSPERSTRIP, target.Length);
        output.SetField(TiffTag.PAGENUMBER, pageIndex, pageCount);
        output.SetField(TiffTag.CLEANFAXDATA, CleanFaxData.CLEAN);
        output.SetField(TiffTag.SOFTWARE, "TKFaxEngineTest image_translate fit-and-pad");
        output.SetField(
            TiffTag.IMAGEDESCRIPTION,
            $"Prepared from {Path.GetFileName(sourcePath)}, page {pageIndex + 1}/{pageCount}");
        output.SetField(
            TiffTag.DATETIME,
            DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    private static int GetTiffIntField(Tiff tiff, TiffTag tag, int fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToInt() : fallback;
    }

    private static ushort GetTiffUShortField(Tiff tiff, TiffTag tag, ushort fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToUShort() : fallback;
    }

    private static float GetTiffFloatField(Tiff tiff, TiffTag tag, float fallback) {
        FieldValue[]? field = tiff.GetField(tag);
        return field is { Length: > 0 } ? field[0].ToFloat() : fallback;
    }

    private static void SetupNodes(Options options) {
        IFaxTestsRuntime runtime = RequireRuntime();

        for (int i = 0; i < chainElements; i++) {
            ChainElement element = Chain[i];
            element.Path = runtime.CreateG1050Path(
                options.G1050ModelNumber,
                options.G1050SpeedPatternNumber,
                100,
                33);

            switch (element.NodeType) {
                case NodeType.AudioFax:
                    element.Fax = fax_init(null, i == 0);
                    element.T30 = fax_get_t30_state(element.Fax);
                    SetT30Callbacks(element.T30, i);
                    ConnectAudioBuffers(i);
                    ConfigureNoise(element, options);
                    break;

                case NodeType.T38Fax: {
                        element.T38Terminal = t38_terminal_init(
                            null,
                            i == 0,
                            TxPacketHandler,
                            i)
                            ?? throw new FaxTestsExitException(
                                2,
                                "Cannot start the T.38 terminal instance.");
                        element.T30 =
                            t38_terminal_get_t30_state(element.T38Terminal);
                        element.T38Core = t38_terminal_get_t38_core_state(element.T38Terminal);
                        SetT30Callbacks(element.T30, i);
                        element.T38Peer = DetermineT38Peer(i);
                        break;
                    }

                case NodeType.Tsb85AudioFax:
                    Tsb85Tests.Runtime = runtime;
                    FaxTesterState faxTester = fax_tester.faxtester_init(
                        null,
                        options.XmlFile,
                        options.XmlTestName[i == 0 ? 0 : 1])
                        ?? throw new FaxTestsExitException(
                            2,
                            "Cannot start the FAX tester instance.");
                    element.FaxTester = faxTester;
                    fax_tester.faxtester_set_transmit_on_idle(
                        faxTester,
                        1);
                    ConnectAudioBuffers(i);
                    element.T38Peer = DetermineT38Peer(i);
                    ConfigureNoise(element, options);
                    break;

                case NodeType.ReplayAudioFax:
                    element.Replay = Pcm16WaveReader.OpenMono(options.ReplayFile);
                    ConnectAudioBuffers(i);
                    break;

                case NodeType.AudioToT38Gateway:
                    element.T38Gateway = t38_gateway_init(
                        null,
                        TxPacketHandler,
                        i)
                        ?? throw new FaxTestsExitException(
                            2,
                            "Cannot start T.38 gateway instance.");
                    element.T38Core = t38_gateway_get_t38_core_state(element.T38Gateway);
                    element.T38Peer = DetermineT38Peer(i);
                    ConnectGatewayAudioBuffers(i);
                    ConfigureNoise(element, options);
                    break;

                case NodeType.Passthrough:
                    break;

                case NodeType.T31AudioFax:
                case NodeType.T31T38Fax:
                case NodeType.Tsb85T38Fax:
                case NodeType.ReplayT38Fax:
                    Fail(
                        $"{NodeTypeToString(element.NodeType)} is incomplete in the native fax_tests.c path and cannot be executed.");
                    break;

                default:
                    Fail($"Unsupported node type {element.NodeType}.");
                    break;
            }
        }
    }

    private static void ConnectAudioBuffers(int index) {
        int neighbour = index + (index == 0 ? 1 : -1);
        if ((uint)neighbour >= (uint)chainElements)
            Fail($"Audio node {index} has no adjacent audio channel.");
        Chain[index].AudioInput = Chain[neighbour].AudioBuffers[0];
        Chain[index].AudioOutput = Chain[index].AudioBuffers[0];
    }

    private static void ConnectGatewayAudioBuffers(int index) {
        ChainElement element = Chain[index];
        if (index == 0) {
            element.AudioInput = null;
        } else if (Chain[index - 1].NodeType is NodeType.T38Fax or NodeType.AudioToT38Gateway) {
            if (index + 1 >= chainElements)
                Fail($"Gateway node {index} has no audio-side neighbour.");
            element.AudioInput = Chain[index + 1].AudioBuffers[0];
        } else {
            element.AudioInput = Chain[index - 1].AudioBuffers[0];
        }
        element.AudioOutput = element.AudioBuffers[0];
    }

    private static int DetermineT38Peer(int index) {
        if (index == 0)
            return index + 1;

        return Chain[index - 1].NodeType is NodeType.T38Fax or NodeType.AudioToT38Gateway
            ? index - 1
            : index + 1;
    }

    private static void ConfigureNoise(ChainElement element, Options options) {
        if (options.NoiseLevel <= -99)
            return;

        element.Noise = new Awgn(1_234_567, options.NoiseLevel);
        element.SignalScaling = Math.Pow(10.0, options.SignalLevel / 20.0);
        Console.WriteLine($"Signal scaling {element.SignalScaling:F6}");
    }

    private static void ConfigureNodes(Options options) {
        for (int i = 0; i < chainElements; i++) {
            ChainElement element = Chain[i];
            if (element.T30 is not null)
                ConfigureT30(element.T30, i, options);

            switch (element.NodeType) {
                case NodeType.AudioFax:
                    fax_set_transmit_on_idle(element.Fax!, options.UseTransmitOnIdle ? 1 : 0);
                    fax_set_tep_mode(element.Fax!, options.UseTep ? 1 : 0);
                    break;

                case NodeType.T38Fax:
                    t38_set_t38_version(element.T38Core!, options.T38Version);
                    switch (options.Transport) {
                        case T38Transport.Udptl:
                        case T38Transport.Rtp:
                            t38_terminal_set_fill_bit_removal(
                                element.T38Terminal!,
                                options.RemoveFillBits);
                            t38_terminal_set_tep_mode(
                                element.T38Terminal!,
                                options.UseTep);
                            break;
                        case T38Transport.Tcp:
                        case T38Transport.TcpTpkt:
                            t38_terminal_set_fill_bit_removal(element.T38Terminal!, true);
                            t38_terminal_set_config(
                                element.T38Terminal!,
                                (int)(T38TerminalOptions.NoPacing |
                                      T38TerminalOptions.NoIndicators));
                            t38_terminal_set_tep_mode(element.T38Terminal!, false);
                            break;
                    }
                    break;

                case NodeType.AudioToT38Gateway:
                    t38_gateway_set_transmit_on_idle(
                        element.T38Gateway!,
                        options.UseTransmitOnIdle);
                    t38_gateway_set_supported_modems(
                        element.T38Gateway!,
                        options.SupportedModems);
                    t38_gateway_set_fill_bit_removal(
                        element.T38Gateway!,
                        options.RemoveFillBits);
                    t38_gateway_set_real_time_frame_handler(
                        element.T38Gateway!,
                        RealTimeGatewayFrameHandler,
                        i);
                    t38_gateway_set_ecm_capability(
                        element.T38Gateway!,
                        options.UseEcm);
                    t38_set_t38_version(element.T38Core!, options.T38Version);
                    break;
            }
        }
    }

    private static void ConfigureT30(T30State state, int index, Options options) {
        int digit = index + 1;
        string ident = string.Concat(Enumerable.Repeat(digit.ToString(CultureInfo.InvariantCulture), 8));
        _ = t30_set_tx_ident(state, ident);
        Chain[Chain[index].Peer].ExpectedRxInfo.Ident = ident;

        _ = t30_set_tx_sub_address(state, $"Sub-address {digit}");
        _ = t30_set_tx_sender_ident(state, $"Sender ID {digit}");
        _ = t30_set_tx_password(state, $"Password {digit}");
        _ = t30_set_tx_polled_sub_address(state, $"Polled sub-add {digit}");
        _ = t30_set_tx_selective_polling_address(state, $"Select poll add {digit}");
        _ = t30_set_tx_page_header_info(state, options.PageHeaderInfo);
        if (options.PageHeaderTimeZone is not null)
            _ = t30_set_tx_page_header_tz(state, options.PageHeaderTimeZone);

        if (index != 0) {
            byte[] nsf =
            [
                0x50, 0x00, 0x00, 0x00,
                .. Encoding.ASCII.GetBytes("TKFaxEngine"),
                0x00
            ];
            _ = t30_set_tx_nsf(state, nsf);
            Chain[Chain[index].Peer].ExpectedRxInfo.Nsf = nsf;
        }

        _ = t30_set_supported_modems(state, options.SupportedModems);
        _ = t30_set_supported_t30_features(
            state,
            (int)(ProtocolT30SupportedFeatures.Identification |
                  ProtocolT30SupportedFeatures.SelectivePolling |
                  ProtocolT30SupportedFeatures.SubAddressing));
        _ = t30_set_supported_image_sizes(
            state,
            T4_SUPPORT_WIDTH_215MM |
            T4_SUPPORT_WIDTH_255MM |
            T4_SUPPORT_WIDTH_303MM |
            T4_SUPPORT_LENGTH_US_LETTER |
            T4_SUPPORT_LENGTH_US_LEGAL |
            T4_SUPPORT_LENGTH_UNLIMITED);

        _ = t30_set_supported_bilevel_resolutions(
            state,
            GetBilevelResolutionMask(
                options.AllowedBilevelResolutions[index == 0 ? 0 : 1]));

        _ = t30_set_supported_colour_resolutions(
            state,
            options.ColourEnabled
                ? T4_RESOLUTION_100_100 |
                  T4_RESOLUTION_200_200 |
                  T4_RESOLUTION_300_300 |
                  T4_RESOLUTION_400_400 |
                  T4_RESOLUTION_600_600 |
                  T4_RESOLUTION_1200_1200
                : 0);

        _ = t30_set_supported_output_compressions(
            state,
            options.T37LikeOutput
                ? T4_COMPRESSION_T85 |
                  T4_COMPRESSION_T85_L0 |
                  T4_COMPRESSION_T6 |
                  T4_COMPRESSION_T42_T81
                : T4_COMPRESSION_T6 | T4_COMPRESSION_JPEG);

        _ = t30_set_ecm_capability(state, options.UseEcm);
        _ = t30_set_supported_compressions(
            state,
            T4_COMPRESSION_T4_1D |
            T4_COMPRESSION_T4_2D |
            T4_COMPRESSION_T6 |
            T4_COMPRESSION_T85 |
            T4_COMPRESSION_T85_L0 |
            T4_COMPRESSION_T43 |
            T4_COMPRESSION_T45 |
            T4_COMPRESSION_T42_T81 |
            T4_COMPRESSION_SYCC_T81 |
            T4_COMPRESSION_GRAYSCALE |
            T4_COMPRESSION_COLOUR |
            T4_COMPRESSION_12BIT |
            T4_COMPRESSION_COLOUR_TO_GRAY |
            T4_COMPRESSION_GRAY_TO_BILEVEL |
            T4_COMPRESSION_COLOUR_TO_BILEVEL |
            T4_COMPRESSION_RESCALING);
        _ = t30_set_minimum_scan_line_time(state, options.ScanLineTime);
    }

    private static int GetBilevelResolutionMask(int mode) => mode switch {
        0 =>
            T4_RESOLUTION_R8_STANDARD |
            T4_RESOLUTION_R8_FINE |
            T4_RESOLUTION_R8_SUPERFINE |
            T4_RESOLUTION_R16_SUPERFINE |
            T4_RESOLUTION_200_100 |
            T4_RESOLUTION_200_200 |
            T4_RESOLUTION_200_400 |
            T4_RESOLUTION_300_300 |
            T4_RESOLUTION_300_600 |
            T4_RESOLUTION_400_400 |
            T4_RESOLUTION_400_800 |
            T4_RESOLUTION_600_600 |
            T4_RESOLUTION_600_1200 |
            T4_RESOLUTION_1200_1200,
        1 =>
            T4_RESOLUTION_R8_STANDARD |
            T4_RESOLUTION_R8_FINE |
            T4_RESOLUTION_R8_SUPERFINE |
            T4_RESOLUTION_R16_SUPERFINE,
        2 =>
            T4_RESOLUTION_200_100 |
            T4_RESOLUTION_200_200 |
            T4_RESOLUTION_200_400 |
            T4_RESOLUTION_300_300 |
            T4_RESOLUTION_300_600 |
            T4_RESOLUTION_400_400 |
            T4_RESOLUTION_400_800 |
            T4_RESOLUTION_600_600 |
            T4_RESOLUTION_600_1200 |
            T4_RESOLUTION_1200_1200,
        3 =>
            T4_RESOLUTION_R8_STANDARD |
            T4_RESOLUTION_R8_FINE |
            T4_RESOLUTION_200_100 |
            T4_RESOLUTION_200_200,
        4 => T4_RESOLUTION_R8_STANDARD | T4_RESOLUTION_200_100,
        _ => throw new FaxTestsExitException(2, $"Unknown bilevel resolution mode {mode}.")
    };

    private static void ConnectTesterAndPassthroughNodes() {
        for (int i = 0; i < chainElements; i++) {
            ChainElement element = Chain[i];
            switch (element.NodeType) {
                case NodeType.Tsb85AudioFax:
                    ChainElement peer = Chain[element.Peer];
                    element.FaxTester!.FarFax = peer.Fax;
                    element.FaxTester.FarT38 = peer.T38Terminal;
                    element.FaxTester.FarT30 = peer.T30;
                    element.FaxTester.FarTag = (char)('A' + element.Peer);
                    while (fax_tester.faxtester_next_step(element.FaxTester) == 0) {
                    }
                    break;

                case NodeType.Passthrough:
                    if (i == 0 || i + 1 >= chainElements)
                        Fail("A passthrough node must be between two audio nodes.");
                    if (ReferenceEquals(Chain[i - 1].AudioInput, element.AudioBuffers[0]))
                        Chain[i - 1].AudioInput = Chain[i + 1].AudioBuffers[0];
                    if (ReferenceEquals(Chain[i + 1].AudioInput, element.AudioBuffers[0]))
                        Chain[i + 1].AudioInput = Chain[i - 1].AudioBuffers[0];
                    break;
            }
        }
    }

    private static void ConfigureDocuments(Options options) {
        int transmitter = options.UsePolledMode ? chainElements - 1 : 0;
        int receiver = options.UsePolledMode ? 0 : chainElements - 1;

        if (Chain[transmitter].NodeType is NodeType.AudioFax or NodeType.T38Fax) {
            T30State t30 = Chain[transmitter].T30
                ?? throw new FaxTestsExitException(
                    2,
                    $"TX T.30 state is null for node {transmitter}.");
            string transmitFile = options.PreparedInputTiffFile ?? options.InputTiffFile;
            Console.Error.WriteLine(
                $"FAX_TEST TX FILE node={transmitter} path={transmitFile} source_path={options.InputTiffFile} " +
                $"start_page={options.StartPage} end_page={options.EndPage}");
            t30_set_tx_file(
                t30,
                transmitFile,
                options.StartPage,
                options.EndPage);
        }

        if (Chain[receiver].NodeType is NodeType.AudioFax or NodeType.T38Fax) {
            T30State t30 = Chain[receiver].T30
                ?? throw new FaxTestsExitException(
                    2,
                    $"RX T.30 state is null for node {receiver}.");
            Console.Error.WriteLine(
                $"FAX_TEST RX FILE node={receiver} path={OutputTiffFileName}");
            t30_set_rx_file(t30, OutputTiffFileName, -1);
        }
    }

    private static void RunStreamingLoop(Options options, Pcm16WaveWriter? audioLog) {
        short[][] feedbackHistory =
        [
            .. Enumerable.Range(0, 8)
                .Select(static _ => new short[SamplesPerChunk])
        ];
        short[] interleavedAudio = new short[SamplesPerChunk * 4];
        byte[] packet = new byte[1_024];
        int historyPointer = 0;
        int dropFrame = options.DropFrameRate;
        when = 0.0;

        LogRuntimeState(0, true);
        for (ulong loop = 1; loop <= MaximumRuntimeLoops; loop++) {
            Array.Clear(interleavedAudio);

            for (int i = 0; i < chainElements; i++) {
                ChainElement element = Chain[i];
                switch (element.NodeType) {
                    case NodeType.AudioFax:
                        ProcessAudioFaxNode(i, element, options, interleavedAudio, feedbackHistory, historyPointer);
                        break;

                    case NodeType.T38Fax:
                        element.Completed = t38_terminal_send_timeout(
                            element.T38Terminal!,
                            SamplesPerChunk) != 0;
                        DrainT38Path(element, packet);
                        break;

                    case NodeType.Tsb85AudioFax:
                        ProcessFaxTesterNode(i, element, options, interleavedAudio);
                        break;

                    case NodeType.ReplayAudioFax:
                        element.AudioOutput!.Length = element.Replay!.Read(
                            element.AudioOutput.Samples);
                        if (element.AudioOutput.Length == 0)
                            element.Completed = true;
                        break;

                    case NodeType.AudioToT38Gateway:
                        ProcessGatewayNode(
                            element,
                            options,
                            packet,
                            feedbackHistory,
                            historyPointer,
                            ref dropFrame);
                        break;

                    case NodeType.Passthrough:
                        break;
                }
            }

            audioLog?.WriteFrames(interleavedAudio);
            when += (double)SamplesPerChunk / SampleRate;
            LogRuntimeState(loop, false);

            for (int endpoint = 0; endpoint < chainElements; endpoint += chainElements - 1) {
                ChainElement element = Chain[endpoint];
                if (element.PhaseEReached && !element.Succeeded) {
                    LogRuntimeState(loop, true);
                    Fail($"FAX_TEST node={endpoint} reached phase E with a failure result.");
                }
            }

            if (Chain[0].Completed && Chain[chainElements - 1].Completed) {
                LogRuntimeState(loop, true);
                return;
            }

            historyPointer = (historyPointer + 1) & 3;
        }

        Fail($"FAX_TEST exceeded {MaximumRuntimeLoops} streaming iterations.");
    }

    private static void ProcessAudioFaxNode(
        int index,
        ChainElement element,
        Options options,
        short[] interleavedAudio,
        short[][] feedbackHistory,
        int historyPointer) {
        AudioBuffer input = element.AudioInput
            ?? throw new FaxTestsExitException(2, $"Audio node {index} has no input buffer.");
        AudioBuffer output = element.AudioOutput!;

        if (options.LogAudio)
            Interleave(input, interleavedAudio, index == 0 ? 0 : 2);

        _ = fax_rx(element.Fax!, input.Samples, input.Length);
        if (t30_call_active(element.T30!) == 0) {
            element.Completed = true;
            return;
        }

        output.Length = fax_tx(element.Fax!, output.Samples, SamplesPerChunk);
        PadTransmitBlock(output, options.UseTransmitOnIdle);
        ApplyNoise(element, output);

        if (options.LogAudio)
            Interleave(output, interleavedAudio, index == 0 ? 1 : 3);

        if (options.FeedbackAudio)
            ApplyFeedback(output, feedbackHistory[historyPointer]);
    }

    private static void ProcessFaxTesterNode(
        int index,
        ChainElement element,
        Options options,
        short[] interleavedAudio) {
        AudioBuffer input = element.AudioInput!;
        AudioBuffer output = element.AudioOutput!;

        if (options.LogAudio)
            Interleave(input, interleavedAudio, index == 0 ? 0 : 2);

        _ = fax_tester.faxtester_rx(
            element.FaxTester!,
            input.Samples,
            input.Length);
        output.Length = fax_tester.faxtester_tx(
            element.FaxTester!,
            output.Samples,
            SamplesPerChunk);
        ApplyNoise(element, output);

        if (options.LogAudio)
            Interleave(output, interleavedAudio, index == 0 ? 1 : 3);

        FaxTesterState tester = element.FaxTester!;
        if (tester.TestForCallClear && !tester.FarEndClearedCall) {
            tester.CallClearTimer += output.Length;
            if (tester.FarT30 is not null && t30_call_active(tester.FarT30) == 0) {
                int milliseconds = tester.CallClearTimer / 8;
                if (milliseconds < tester.TimeInMilliseconds || milliseconds > tester.TimeoutMilliseconds)
                    Fail(
                        $"Far end clear time {milliseconds}ms outside {tester.TimeInMilliseconds}ms..{tester.TimeoutMilliseconds}ms.");
                tester.FarEndClearedCall = true;
                tester.TestForCallClear = false;
                while (fax_tester.faxtester_next_step(tester) == 0) {
                }
            }
        }
    }

    private static void ProcessGatewayNode(
        ChainElement element,
        Options options,
        byte[] packet,
        short[][] feedbackHistory,
        int historyPointer,
        ref int dropFrame) {
        AudioBuffer input = element.AudioInput
            ?? throw new FaxTestsExitException(2, "Gateway has no audio input buffer.");
        AudioBuffer output = element.AudioOutput!;

        if (options.DropFrameRate != 0 && --dropFrame == 0) {
            dropFrame = options.DropFrameRate;
            _ = t38_gateway_rx_fillin(element.T38Gateway!, SamplesPerChunk);
        } else {
            _ = t38_gateway_rx(element.T38Gateway!, input.Samples, input.Length);
        }

        output.Length = t38_gateway_tx(
            element.T38Gateway!,
            output.Samples,
            SamplesPerChunk);
        PadTransmitBlock(output, options.UseTransmitOnIdle);
        ApplyNoise(element, output);

        if (options.FeedbackAudio)
            ApplyFeedback(output, feedbackHistory[historyPointer]);

        DrainT38Path(element, packet);
    }

    private static void PadTransmitBlock(AudioBuffer output, bool transmitOnIdle) {
        if (transmitOnIdle || output.Length >= SamplesPerChunk)
            return;
        output.Samples.AsSpan(output.Length, SamplesPerChunk - output.Length).Clear();
        output.Length = SamplesPerChunk;
    }

    private static void ApplyNoise(ChainElement element, AudioBuffer output) {
        if (element.Noise is null)
            return;

        for (int i = 0; i < output.Length; i++) {
            int sample = (int)Math.Round(output.Samples[i] * element.SignalScaling) +
                         element.Noise.NextSample();
            output.Samples[i] = (short)Math.Clamp(sample, short.MinValue, short.MaxValue);
        }
    }

    private static void ApplyFeedback(AudioBuffer output, short[] history) {
        for (int i = 0; i < output.Length; i++) {
            int value = output.Samples[i] + (history[i] >> 1);
            output.Samples[i] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
        output.Samples.AsSpan(0, Math.Min(output.Length, history.Length)).CopyTo(history);
    }

    private static void Interleave(AudioBuffer source, short[] destination, int channel) {
        int count = Math.Min(source.Length, SamplesPerChunk);
        for (int i = 0; i < count; i++)
            destination[4 * i + channel] = source.Samples[i];
    }

    private static int TxPacketHandler(
        T38CoreState core,
        object? userData,
        ReadOnlyMemory<byte> packet,
        int count) {
        int channel = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        ChainElement element = Chain[channel];

        if (t38SimulateIncrementingRepeats) {
            for (int i = 0; i < count; i++) {
                int sequence = element.T38SubstituteSequence;
                if (element.Path!.Put(packet.Span, sequence, when) < 0)
                    Console.WriteLine($"Lost packet {sequence}");
                element.T38SubstituteSequence = (sequence + 1) & 0xFFFF;
            }
        } else {
            for (int i = 0; i < count; i++) {
                if (element.Path!.Put(packet.Span, core.TxSequenceNumber, when) < 0)
                    Console.WriteLine($"Lost packet {core.TxSequenceNumber}");
            }
        }
        return 0;
    }

    private static void DrainT38Path(ChainElement element, byte[] packet) {
        while (element.Path!.TryGet(
                   when,
                   packet,
                   out int length,
                   out int sequence,
                   out _,
                   out _)) {
            int peer = element.T38Peer;
            if ((uint)peer >= (uint)chainElements || Chain[peer].T38Core is null)
                Fail($"T.38 node has invalid peer {peer}.");
            _ = t38_core_rx_ifp_packet(
                Chain[peer].T38Core!,
                packet,
                length,
                unchecked((ushort)sequence));
        }
    }

    private static void VerifyTransfer(Options options) {
        int expectedPages = GetTiffTotalPages(options.InputTiffFile);
        if (options.EndPage >= 0 && expectedPages > options.EndPage + 1)
            expectedPages = options.EndPage + 1;
        if (options.StartPage >= 0)
            expectedPages -= options.StartPage;

        int[] endpoints = [0, chainElements - 1];
        foreach (int endpoint in endpoints) {
            ChainElement element = Chain[endpoint];
            if (!element.PhaseEReached || !element.Succeeded || element.T30 is null)
                Fail($"FAX endpoint {element.Tag} did not complete successfully.");

            var statistics = new T30Statistics();
            t30_get_transfer_statistics(element.T30, statistics);
            bool isReceiver =
                (!options.UsePolledMode && endpoint != 0) ||
                (options.UsePolledMode && endpoint == 0);

            if (isReceiver) {
                if (statistics.PagesTransmitted != 0 ||
                    statistics.PagesReceived != expectedPages) {
                    Fail(
                        $"Endpoint {element.Tag}: expected RX {expectedPages}/TX 0 pages, got RX {statistics.PagesReceived}/TX {statistics.PagesTransmitted}.");
                }
            } else if (statistics.PagesTransmitted != expectedPages ||
                       statistics.PagesReceived != 0) {
                Fail(
                    $"Endpoint {element.Tag}: expected TX {expectedPages}/RX 0 pages, got TX {statistics.PagesTransmitted}/RX {statistics.PagesReceived}.");
            }
        }
    }

    private static void ReleaseNodes() {
        for (int i = 0; i < chainElements; i++) {
            ChainElement element = Chain[i];
            try {
                if (element.Fax is not null)
                    _ = fax_free(element.Fax);
                if (element.T38Terminal is not null)
                    _ = t38_terminal_free(element.T38Terminal);
                if (element.FaxTester is not null)
                    _ = fax_tester.faxtester_free(element.FaxTester);
                element.Replay?.Dispose();
                if (element.T38Gateway is not null)
                    _ = t38_gateway_free(element.T38Gateway);
                element.Path?.Dispose();
            } finally {
                element.Reset();
            }
        }
    }

    private static void SetT30Callbacks(T30State state, int channel) {
        t30_set_phase_b_handler(state, PhaseBHandler, channel);
        t30_set_phase_d_handler(state, PhaseDHandler, channel);
        t30_set_phase_e_handler(state, PhaseEHandler, channel);
        t30_set_real_time_frame_handler(state, RealTimeT30FrameHandler, channel);
        t30_set_document_handler(state, DocumentHandler, channel);
    }

    private static int PhaseBHandler(object? userData, int result) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        ChainElement element = Chain[index];
        T30State state = element.T30!;
        char channel = (char)('A' + index);
        Console.WriteLine(
            $"{channel}: Phase B handler - (0x{result:X}) {t30_frametype(unchecked((byte)result))}");

        int status = (int)T30Error.Ok;
        status = CompareText(
            channel,
            "ident",
            element.ExpectedRxInfo.Ident,
            t30_get_rx_ident(state),
            (int)T30Error.IdentUnacceptable,
            status);
        status = CompareText(
            channel,
            "sub-address",
            element.ExpectedRxInfo.SubAddress,
            t30_get_rx_sub_address(state),
            (int)T30Error.SubUnacceptable,
            status);
        status = CompareText(
            channel,
            "polled sub-address",
            element.ExpectedRxInfo.PolledSubAddress,
            t30_get_rx_polled_sub_address(state),
            (int)T30Error.PsaUnacceptable,
            status);
        status = CompareText(
            channel,
            "selective polling address",
            element.ExpectedRxInfo.SelectivePollingAddress,
            t30_get_rx_selective_polling_address(state),
            (int)T30Error.SepUnacceptable,
            status);
        status = CompareText(
            channel,
            "sender ident",
            element.ExpectedRxInfo.SenderIdent,
            t30_get_rx_sender_ident(state),
            (int)T30Error.SidUnacceptable,
            status);
        status = CompareText(
            channel,
            "password",
            element.ExpectedRxInfo.Password,
            t30_get_rx_password(state),
            (int)T30Error.PwdUnacceptable,
            status);

        CompareBinary(channel, "NSF", element.ExpectedRxInfo.Nsf, t30_get_rx_nsf(state));
        CompareBinary(channel, "NSC", element.ExpectedRxInfo.Nsc, t30_get_rx_nsc(state));
        CompareBinary(channel, "NSS", element.ExpectedRxInfo.Nss, t30_get_rx_nss(state));
        return status;
    }

    private static int CompareText(
        char channel,
        string label,
        string? expected,
        string? actual,
        int failureStatus,
        int currentStatus) {
        if (actual is not null)
            Console.WriteLine($"{channel}: Phase B: remote {label} '{actual}'");

        if (!string.IsNullOrEmpty(expected) &&
            !string.Equals(expected, actual, StringComparison.Ordinal)) {
            Console.WriteLine(
                $"{channel}: Phase B: remote {label} incorrect or missing - expected '{expected}'");
            return failureStatus;
        }
        return currentStatus;
    }

    private static void CompareBinary(
        char channel,
        string label,
        byte[] expected,
        ReadOnlyMemory<byte> actual) {
        if (actual.Length != 0)
            Console.WriteLine($"{channel}: Phase B: {label} {actual.Length} bytes");
        if (expected.Length != 0 && !actual.Span.SequenceEqual(expected)) {
            Console.WriteLine(
                $"{channel}: Phase B: remote {label} incorrect or missing - expected {expected.Length} bytes");
        }
    }

    private static int PhaseDHandler(object? userData, int result) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        T30State state = Chain[index].T30!;
        char channel = (char)('A' + index);
        Console.WriteLine(
            $"{channel}: Phase D handler - (0x{result:X}) {t30_frametype(unchecked((byte)result))}");

        if (useReceiverNotReady)
            _ = t30_set_receiver_not_ready(state, 3);

        if (testLocalInterrupt) {
            if (index == 0) {
                Console.WriteLine($"{channel}: Initiating interrupt request");
                t30_local_interrupt_request(state, 1);
            } else if (result is T30Frame.Pip or T30Frame.PriMps or T30Frame.PriEom or T30Frame.PriEop) {
                Console.WriteLine($"{channel}: Accepting interrupt request");
                t30_local_interrupt_request(state, 1);
            }
        }
        return (int)T30Error.Ok;
    }

    private static void PhaseEHandler(object? userData, int result) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        char channel = (char)('A' + index);
        Console.WriteLine(
            $"{channel}: Phase E handler - ({result}) {t30_completion_code_to_str(result)}");
        Chain[index].Succeeded = result == (int)T30Error.Ok;
        Chain[index].PhaseEReached = true;
    }

    private static void RealTimeT30FrameHandler(
        object? userData,
        bool incoming,
        ReadOnlyMemory<byte> message) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        byte frameType = message.Length > 2 ? message.Span[2] : (byte)0;
        Console.WriteLine(
            $"{(char)('A' + index)}: Real time frame handler - " +
            $"{(incoming ? "line->T.30" : "T.30->line")}, " +
            $"{t30_frametype(frameType)}, length = {message.Length}");
    }

    private static int DocumentHandler(object? userData, int documentEvent) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        Console.WriteLine($"{(char)('A' + index)}: Document handler - event {documentEvent}");
        return 0;
    }

    private static void RealTimeGatewayFrameHandler(
        object? userData,
        bool incoming,
        ReadOnlyMemory<byte> message) {
        int index = Convert.ToInt32(userData, CultureInfo.InvariantCulture);
        byte frameType = message.Length > 2 ? message.Span[2] : (byte)0;
        Console.WriteLine(
            $"{(char)('A' + index)}: Real time gateway frame handler - " +
            $"{(incoming ? "PSTN->T.38" : "T.38->PSTN")}, " +
            $"{t30_frametype(frameType)}, length = {message.Length}");
    }

    private static void LogRuntimeState(ulong loopNumber, bool force) {
        if (!force && loopNumber % 500 != 0)
            return;

        for (int i = 0; i < chainElements; i++) {
            ChainElement element = Chain[i];
            if (element.T30 is null) {
                Console.Error.WriteLine(
                    $"FAX_TEST STATE loop={loopNumber} sim_s={when:F3} node={i} " +
                    $"tag={element.Tag} type={NodeTypeToString(element.NodeType)} " +
                    $"completed={(element.Completed ? 1 : 0)} succeeded={(element.Succeeded ? 1 : 0)} phase_e={(element.PhaseEReached ? 1 : 0)} t30=none");
                continue;
            }

            var statistics = new T30Statistics();
            t30_get_transfer_statistics(element.T30, statistics);
            Console.Error.WriteLine(
                $"FAX_TEST STATE loop={loopNumber} sim_s={when:F3} node={i} " +
                $"tag={element.Tag} type={NodeTypeToString(element.NodeType)} " +
                $"completed={(element.Completed ? 1 : 0)} succeeded={(element.Succeeded ? 1 : 0)} " +
                $"phase_e={(element.PhaseEReached ? 1 : 0)} active={t30_call_active(element.T30)} " +
                $"phase={element.T30.Phase} next_phase={element.T30.NextPhase} state={element.T30.State} " +
                $"step={element.T30.Step} operation={element.T30.OperationInProgress} " +
                $"rx_signal={(element.T30.RxSignalPresent ? 1 : 0)} rx_trained={(element.T30.RxTrained ? 1 : 0)} " +
                $"pages_tx={statistics.PagesTransmitted} pages_rx={statistics.PagesReceived} " +
                $"bit_rate={statistics.BitRate} ecm={(statistics.ErrorCorrectingMode ? 1 : 0)}");
        }
    }

    private static void T33Tests() {
        string[] packets =
        [
            "#1234567890#1234",
            "1234#5678#8910",
            "#6174444100#1234#567",
            "1234#5678##2032223",
            "#2037445555##6446666",
            "#2037445555#1234##6446666#5678"
        ];

        Console.WriteLine("T.33 sub-address packing/unpacking tests");
        Span<byte> field = stackalloc byte[T30State.MaxIdentLength + 1];
        foreach (string packet in packets) {
            Console.WriteLine($"'{packet}'");
            byte[] encoded = Encoding.ASCII.GetBytes(packet + "\0");
            string rebuilt = string.Empty;

            for (int fieldNumber = 0; fieldNumber < 100; fieldNumber++) {
                int type = t33_sub_address_extract_field(field, encoded, fieldNumber);
                if (type <= 0) {
                    if (type == (int)T33FieldType.None)
                        break;
                    Fail("Bad sub-address field.");
                }

                int zero = field.IndexOf((byte)0);
                string value = Encoding.ASCII.GetString(field[..(zero < 0 ? field.Length : zero)]);
                T33FieldType fieldType = (T33FieldType)type;
                Console.WriteLine(fieldType == T33FieldType.Sst ? $"SST '{value}'" : $"    EXT '{value}'");
                rebuilt = t33_sub_address_add_field(rebuilt, value, fieldType);
            }

            if (!string.Equals(packet, rebuilt, StringComparison.Ordinal))
                Fail($"Re-encode mismatch '{packet}' '{rebuilt}'.");
        }
    }

    private static int GetTiffTotalPages(string path) {
        t4_tx_state_t state = t4_tx_init(null, path, -1, -1)
            ?? throw new FaxTestsExitException(2, $"Cannot open TIFF document '{path}'.");
        try {
            return t4_tx_get_pages_in_file(state);
        } finally {
            _ = t4_tx_free(state);
        }
    }

    private static IFaxTestsRuntime RequireRuntime() =>
        Runtime ?? throw new FaxTestsExitException(
            2,
            "fax_tests.Runtime must provide the G.1050 network path used by the test.");

    private static Options ParseOptions(string[] testArguments) {
        var options = new Options();
        int argumentCount = testArguments.Length;
        int resolutionSlot = 0;
        int xmlSlot = 0;

        for (int i = 0; i < argumentCount; i++) {
            string argument = testArguments[i];
            if (string.IsNullOrEmpty(argument) || argument[0] != '-' || argument == "-")
                continue;

            string option = argument[..2];
            string? inlineValue = argument.Length > 2 ? argument[2..] : null;
            string NextValue() {
                if (!string.IsNullOrEmpty(inlineValue))
                    return inlineValue;
                if (++i >= argumentCount)
                    throw new FaxTestsExitException(2, $"Missing value for option {option}.");
                return testArguments[i];
            }

            switch (option) {
                case "-7": options.T37LikeOutput = true; break;
                case "-b":
                    options.AllowedBilevelResolutions[resolutionSlot] = ParseInt(NextValue(), option);
                    resolutionSlot ^= 1;
                    break;
                case "-c": options.CompletionCodeLookup = ParseInt(NextValue(), option); break;
                case "-C": options.ColourEnabled = true; break;
                case "-d": options.ReplayFile = NextValue(); break;
                case "-D": options.DropFrameRate = ParseInt(NextValue(), option); break;
                case "-e": options.UseEcm = true; break;
                case "-f": options.FeedbackAudio = true; break;
                case "-F": options.RemoveFillBits = true; break;
                case "-g": Fail("Graphical monitoring not available in this managed test port."); break;
                case "-H": options.PageHeaderInfo = NextValue(); break;
                case "-i": options.InputTiffFile = NextValue(); break;
                case "-I": options.IncrementingRepeats = true; break;
                case "-l": options.LogAudio = true; break;
                case "-m": options.SupportedModems = ParseInt(NextValue(), option); break;
                case "-M": {
                        string value = NextValue();
                        options.G1050ModelNumber = char.ToUpperInvariant(value[0]) - 'A' + 1;
                        break;
                    }
                case "-n": options.NoiseLevel = ParseInt(NextValue(), option); break;
                case "-p": options.Path = NextValue(); break;
                case "-P": options.UsePolledMode = true; break;
                case "-s": options.G1050SpeedPatternNumber = ParseInt(NextValue(), option); break;
                case "-S": options.ScanLineTime = ParseInt(NextValue(), option); break;
                case "-t": options.UseTep = true; break;
                case "-T": options.StartPage = 0; options.EndPage = ParseInt(NextValue(), option); break;
                case "-u": options.Transport = ParseTransport(NextValue()); break;
                case "-v": options.T38Version = ParseInt(NextValue(), option); break;
                case "-x": options.XmlTestName[xmlSlot] = NextValue(); xmlSlot ^= 1; break;
                case "-X": options.XmlFile = NextValue(); break;
                case "-z": options.PageHeaderTimeZone = NextValue(); break;
                default: Fail($"Unknown option '{argument}'."); break;
            }
        }

        t38SimulateIncrementingRepeats = options.IncrementingRepeats;
        return options;
    }

    private static int ParseInt(string text, string option) {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            throw new FaxTestsExitException(2, $"Invalid numeric value '{text}' for {option}.");
        return value;
    }

    private static T38Transport ParseTransport(string text) =>
        text.ToLowerInvariant() switch {
            "udptl" => T38Transport.Udptl,
            "rtp" => T38Transport.Rtp,
            "tcp" => T38Transport.Tcp,
            "tcp-tpkt" => T38Transport.TcpTpkt,
            _ => throw new FaxTestsExitException(2, "Unknown T.38 transport mode.")
        };

    [DoesNotReturn]
    private static void Fail(string message) {
        Console.Error.WriteLine(message);
        throw new FaxTestsExitException(2, message);
    }

    private enum NodeType {
        AudioFax = 1,
        T38Fax,
        T31AudioFax,
        T31T38Fax,
        Tsb85AudioFax,
        Tsb85T38Fax,
        ReplayAudioFax,
        ReplayT38Fax,
        AudioToT38Gateway,
        Passthrough,
        AudioChannel,
        T38Channel
    }

    private enum T38Transport {
        Udptl,
        Rtp,
        Tcp,
        TcpTpkt
    }

    private sealed class Options {
        public bool T37LikeOutput { get; set; }
        public int[] AllowedBilevelResolutions { get; } = new int[2];
        public int CompletionCodeLookup { get; set; } = -1;
        public bool ColourEnabled { get; set; }
        public string ReplayFile { get; set; } = InputWaveFileName;
        public int DropFrameRate { get; set; }
        public bool UseEcm { get; set; }
        public bool FeedbackAudio { get; set; }
        public bool RemoveFillBits { get; set; }
        public string? PageHeaderInfo { get; set; }
        public string InputTiffFile { get; set; } = InputTiffFileName;
        public string? PreparedInputTiffFile { get; set; }
        public bool IncrementingRepeats { get; set; }
        public bool LogAudio { get; set; }
        public int SupportedModems { get; set; } =
            (int)(ProtocolT30SupportedModems.V27Ter | ProtocolT30SupportedModems.V29 | ProtocolT30SupportedModems.V17);
        public int G1050ModelNumber { get; set; }
        public int G1050SpeedPatternNumber { get; set; } = 1;
        public int SignalLevel { get; set; }
        public int NoiseLevel { get; set; } = -99;
        public string? Path { get; set; }
        public bool UsePolledMode { get; set; }
        public int ScanLineTime { get; set; }
        public bool UseTep { get; set; }
        public int StartPage { get; set; } = -1;
        public int EndPage { get; set; } = -1;
        public T38Transport Transport { get; set; } = T38Transport.Udptl;
        public int T38Version { get; set; } = 1;
        public string XmlFile { get; set; } = "Test-Files/tsb85.xml";
        public string[] XmlTestName { get; } = ["MRGN01", "MRGN01"];
        public string? PageHeaderTimeZone { get; set; }
        public bool UseTransmitOnIdle { get; set; } = true;
    }

    private readonly record struct FaxTiffPageInfo(
        int Width,
        int Length,
        ushort BitsPerSample,
        ushort SamplesPerPixel,
        Photometric Photometric,
        FillOrder FillOrder,
        float XResolution,
        float YResolution,
        ResUnit ResolutionUnit);

    private sealed class BilevelTiffRowSource {
        public BilevelTiffRowSource(
            Tiff tiff,
            int width,
            int length,
            Photometric photometric,
            FillOrder fillOrder) {
            Tiff = tiff;
            Width = width;
            Length = length;
            Photometric = photometric;
            FillOrder = fillOrder;
            int scanlineSize = tiff.ScanlineSize();
            PackedRow = new byte[Math.Max(checked((width + 7) / 8), scanlineSize)];
        }

        public Tiff Tiff { get; }
        public int Width { get; }
        public int Length { get; }
        public Photometric Photometric { get; }
        public FillOrder FillOrder { get; }
        public byte[] PackedRow { get; }
        public int Row { get; set; }
    }

    private sealed class AudioBuffer {
        public short[] Samples { get; } = new short[SamplesPerChunk];
        public int Length { get; set; }
    }

    private sealed class ExchangedInfo {
        public string? Ident { get; set; }
        public string? SubAddress { get; set; }
        public string? PolledSubAddress { get; set; }
        public string? SelectivePollingAddress { get; set; }
        public string? SenderIdent { get; set; }
        public string? Password { get; set; }
        public byte[] Nsf { get; set; } = [];
        public byte[] Nsc { get; set; } = [];
        public byte[] Nss { get; set; } = [];

        public void Clear() {
            Ident = SubAddress = PolledSubAddress = null;
            SelectivePollingAddress = SenderIdent = Password = null;
            Nsf = [];
            Nsc = [];
            Nss = [];
        }
    }

    private sealed class ChainElement {
        public NodeType NodeType { get; set; }
        public FaxState? Fax { get; set; }
        public T38TerminalState? T38Terminal { get; set; }
        public FaxTesterState? FaxTester { get; set; }
        public T38GatewayState? T38Gateway { get; set; }
        public Pcm16WaveReader? Replay { get; set; }
        public T30State? T30 { get; set; }
        public T38CoreState? T38Core { get; set; }
        public IFaxTestsG1050Path? Path { get; set; }
        public AudioBuffer[] AudioBuffers { get; } = [new(), new()];
        public AudioBuffer? AudioInput { get; set; }
        public AudioBuffer? AudioOutput { get; set; }
        public ExchangedInfo ExpectedRxInfo { get; } = new();
        public Awgn? Noise { get; set; }
        public double SignalScaling { get; set; } = 1.0;
        public int Peer { get; set; }
        public int T38Peer { get; set; }
        public int T38SubstituteSequence { get; set; }
        public bool PhaseEReached { get; set; }
        public bool Completed { get; set; }
        public bool Succeeded { get; set; }
        public string Tag { get; set; } = string.Empty;

        public void Reset() {
            NodeType = 0;
            Fax = null;
            T38Terminal = null;
            FaxTester = null;
            T38Gateway = null;
            Replay = null;
            T30 = null;
            T38Core = null;
            Path = null;
            AudioInput = null;
            AudioOutput = null;
            foreach (AudioBuffer buffer in AudioBuffers) {
                Array.Clear(buffer.Samples);
                buffer.Length = 0;
            }
            ExpectedRxInfo.Clear();
            Noise = null;
            SignalScaling = 1.0;
            Peer = 0;
            T38Peer = 0;
            T38SubstituteSequence = 0;
            PhaseEReached = false;
            Completed = false;
            Succeeded = false;
            Tag = string.Empty;
        }
    }

    public sealed class FaxTestsExitException : Exception {
        public FaxTestsExitException(int exitCode)
            : this(exitCode, $"fax_tests requested exit code {exitCode}.") {
        }

        public FaxTestsExitException(int exitCode, string message)
            : base(message) {
            ExitCode = exitCode;
        }

        public int ExitCode { get; }
    }

    private sealed class Pcm16WaveReader : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryReader reader;
        private readonly long dataEnd;
        private readonly int channels;

        private Pcm16WaveReader(
            FileStream stream,
            BinaryReader reader,
            long dataEnd,
            int channels) {
            this.stream = stream;
            this.reader = reader;
            this.dataEnd = dataEnd;
            this.channels = channels;
        }

        public static Pcm16WaveReader OpenMono(string path) {
            FileStream stream = File.OpenRead(path);
            var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true);
            if (ReadFourCc(reader) != "RIFF")
                throw new InvalidDataException($"'{path}' is not RIFF.");
            _ = reader.ReadUInt32();
            if (ReadFourCc(reader) != "WAVE")
                throw new InvalidDataException($"'{path}' is not WAVE.");

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
            return new Pcm16WaveReader(
                stream,
                reader,
                dataStart + dataLength,
                channels);
        }

        public int Read(short[] destination) {
            int frames = 0;
            while (frames < destination.Length &&
                   stream.Position + channels * 2L <= dataEnd) {
                destination[frames++] = reader.ReadInt16();
                for (int channel = 1; channel < channels; channel++)
                    _ = reader.ReadInt16();
            }
            return frames;
        }

        public void Dispose() {
            reader.Dispose();
            stream.Dispose();
        }

        private static string ReadFourCc(BinaryReader reader) =>
            Encoding.ASCII.GetString(reader.ReadBytes(4));
    }

    private sealed class Pcm16WaveWriter : IDisposable {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly short channels;
        private readonly int sampleRate;
        private long frames;
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
            frames += interleavedSamples.Length / channels;
        }

        public void Dispose() {
            if (disposed)
                return;
            disposed = true;
            long dataBytes = frames * channels * sizeof(short);
            stream.Position = 0;
            WriteHeader(dataBytes);
            writer.Dispose();
            stream.Dispose();
        }

        private void WriteHeader(long dataBytes) {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(checked((uint)(36 + dataBytes)));
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)channels);
            writer.Write((uint)sampleRate);
            writer.Write(checked((uint)(sampleRate * channels * sizeof(short))));
            writer.Write(checked((ushort)(channels * sizeof(short))));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(checked((uint)dataBytes));
        }
    }
}
