/*
 * TKFaxEngineFX - managed C# port
 *
 * C# conversion of t30_local.h.
 */

using TKFaxEngine.FaxImage;

namespace TKFaxEngine.Daten.T30;

public static partial class T30 {
public static int t30_build_dis_or_dtc(T30State state) {
        ArgumentNullException.ThrowIfNull(state);
        Span<byte> frame = state.LocalDisDtcFrame;
        frame.Clear();
        frame[0] = AddressField;
        frame[1] = ControlFinal;
        frame[2] = (byte)(T30Frame.Dis | (state.DisReceived ? 1 : 0));

        if ((state.IafMode & T30IafMode.T37) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisT37);
        if ((state.IafMode & T30IafMode.T38) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisT38);

        if ((state.SupportedModems & T30SupportedModems.V27Ter) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisModemType2);
        if ((state.SupportedModems & T30SupportedModems.V29) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisModemType1);
        if ((state.SupportedModems & T30SupportedModems.V17) != 0)
            frame[4] |= T30DisBits.Bit6 | T30DisBits.Bit4 | T30DisBits.Bit3;

        t4_image_support_t imageSizes = (t4_image_support_t)state.SupportedImageSizes;
        if ((imageSizes & t4_image_support_t.T4_SUPPORT_WIDTH_303MM) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis215mm255mm303mmWidthCapable);
        else if ((imageSizes & t4_image_support_t.T4_SUPPORT_WIDTH_255MM) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis215mm255mmWidthCapable);

        if ((imageSizes & t4_image_support_t.T4_SUPPORT_LENGTH_UNLIMITED) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisUnlimitedLengthCapable);
        else if ((imageSizes & t4_image_support_t.T4_SUPPORT_LENGTH_B4) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisA4B4LengthCapable);
        if ((imageSizes & t4_image_support_t.T4_SUPPORT_LENGTH_US_LETTER) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisNorthAmericanLetterCapable);
        if ((imageSizes & t4_image_support_t.T4_SUPPORT_LENGTH_US_LEGAL) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisNorthAmericanLegalCapable);

        set_ctrl_bits(frame, T30ControlBit.DisMinScanLineTimeCapability1,
            state.LocalMinimumScanTimeCode, 4);

        t4_image_compression_t compressions = (t4_image_compression_t)state.SupportedCompressions;
        if ((compressions & t4_image_compression_t.T4_COMPRESSION_T4_2D) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis2dCapable);
        if ((compressions & t4_image_compression_t.T4_COMPRESSION_NONE) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisUncompressedCapable);

        if (state.EcmAllowed) {
            set_ctrl_bit(frame, T30ControlBit.DisEcmCapable);
            if ((compressions & t4_image_compression_t.T4_COMPRESSION_T6) != 0)
                set_ctrl_bit(frame, T30ControlBit.DisT6Capable);
            if ((compressions & t4_image_compression_t.T4_COMPRESSION_T85) != 0) {
                set_ctrl_bit(frame, T30ControlBit.DisT85Capable);
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_T85_L0) != 0)
                    set_ctrl_bit(frame, T30ControlBit.DisT85L0Capable);
            }
            if ((compressions & (t4_image_compression_t.T4_COMPRESSION_COLOUR | t4_image_compression_t.T4_COMPRESSION_GRAYSCALE)) != 0) {
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_COLOUR) != 0)
                    set_ctrl_bit(frame, T30ControlBit.DisFullColourCapable);
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_T42_T81) != 0)
                    set_ctrl_bit(frame, T30ControlBit.DisT81Capable);
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_T43) != 0) {
                    set_ctrl_bit(frame, T30ControlBit.DisT81Capable);
                    set_ctrl_bit(frame, T30ControlBit.DisT43Capable);
                }
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_T45) != 0)
                    set_ctrl_bit(frame, T30ControlBit.DisT45Capable);
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_SYCC_T81) != 0) {
                    set_ctrl_bit(frame, T30ControlBit.DisT81Capable);
                    set_ctrl_bit(frame, T30ControlBit.DisSyccT81Capable);
                }
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_12BIT) != 0)
                    set_ctrl_bit(frame, T30ControlBit.Dis12bitCapable);
                if ((compressions & t4_image_compression_t.T4_COMPRESSION_NO_SUBSAMPLING) != 0)
                    set_ctrl_bit(frame, T30ControlBit.DisNoSubsampling);
            }
        }

        if ((state.SupportedFeatures & T30SupportedFeatures.FieldNotValid) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisFnvCapable);
        if ((state.SupportedFeatures & T30SupportedFeatures.MultipleSelectivePolling) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisMultipleSelectivePollingCapable);
        if ((state.SupportedFeatures & T30SupportedFeatures.PolledSubAddressing) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisPolledSubaddressingCapable);
        if ((state.SupportedFeatures & T30SupportedFeatures.SelectivePolling) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisSelectivePollingCapable);
        if ((state.SupportedFeatures & T30SupportedFeatures.SubAddressing) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisSubaddressingCapable);
        if ((state.SupportedFeatures & T30SupportedFeatures.Identification) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisPassword);
        if ((state.SupportedFeatures & T30SupportedFeatures.InternetSelectivePollingAddress) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisInternetSelectivePollingAddress);
        if ((state.SupportedFeatures & T30SupportedFeatures.InternetRoutingAddress) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisInternetRoutingAddress);

        if (!string.IsNullOrEmpty(state.TxFile))
            set_ctrl_bit(frame, T30ControlBit.DisReadyToTransmitDataFile);

        t4_image_resolution_t bilevel = (t4_image_resolution_t)state.SupportedBilevelResolutions;
        t4_image_resolution_t colour = (t4_image_resolution_t)state.SupportedColourResolutions;
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_1200_1200) != 0) {
            set_ctrl_bit(frame, T30ControlBit.Dis12001200Capable);
            if ((colour & t4_image_resolution_t.T4_RESOLUTION_1200_1200) != 0)
                set_ctrl_bit(frame, T30ControlBit.DisColourGray12001200Capable);
        }
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_600_1200) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis6001200Capable);
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_600_600) != 0) {
            set_ctrl_bit(frame, T30ControlBit.Dis600600Capable);
            if ((colour & t4_image_resolution_t.T4_RESOLUTION_600_600) != 0)
                set_ctrl_bit(frame, T30ControlBit.DisColourGray600600Capable);
        }
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_400_800) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis400800Capable);
        if ((bilevel & (t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE | t4_image_resolution_t.T4_RESOLUTION_400_400)) != 0) {
            set_ctrl_bit(frame, T30ControlBit.Dis400400Capable);
            if ((colour & t4_image_resolution_t.T4_RESOLUTION_400_400) != 0)
                set_ctrl_bit(frame, T30ControlBit.DisColourGray300300400400Capable);
        }
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_300_600) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis300600Capable);
        if ((bilevel & t4_image_resolution_t.T4_RESOLUTION_300_300) != 0) {
            set_ctrl_bit(frame, T30ControlBit.Dis300300Capable);
            if ((colour & t4_image_resolution_t.T4_RESOLUTION_300_300) != 0)
                set_ctrl_bit(frame, T30ControlBit.DisColourGray300300400400Capable);
        }
        if ((bilevel & (t4_image_resolution_t.T4_RESOLUTION_200_400 | t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE)) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis200400Capable);
        if ((bilevel & (t4_image_resolution_t.T4_RESOLUTION_R8_FINE | t4_image_resolution_t.T4_RESOLUTION_200_200)) != 0)
            set_ctrl_bit(frame, T30ControlBit.Dis200200Capable);
        if ((colour & t4_image_resolution_t.T4_RESOLUTION_100_100) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisColourGray100100Capable);

        if ((bilevel & (t4_image_resolution_t.T4_RESOLUTION_R8_STANDARD | t4_image_resolution_t.T4_RESOLUTION_R8_FINE |
                        t4_image_resolution_t.T4_RESOLUTION_R8_SUPERFINE | t4_image_resolution_t.T4_RESOLUTION_R16_SUPERFINE)) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisMetricResolutionPreferred);
        if ((bilevel & (t4_image_resolution_t.T4_RESOLUTION_200_100 | t4_image_resolution_t.T4_RESOLUTION_200_200 |
                        t4_image_resolution_t.T4_RESOLUTION_200_400 | t4_image_resolution_t.T4_RESOLUTION_300_300 |
                        t4_image_resolution_t.T4_RESOLUTION_300_600 | t4_image_resolution_t.T4_RESOLUTION_400_400 |
                        t4_image_resolution_t.T4_RESOLUTION_400_800 | t4_image_resolution_t.T4_RESOLUTION_600_600 |
                        t4_image_resolution_t.T4_RESOLUTION_600_1200 | t4_image_resolution_t.T4_RESOLUTION_1200_1200)) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisInchResolutionPreferred);

        if ((state.IafMode & T30IafMode.FlowControl) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisT38FlowControlCapable);
        if ((state.IafMode & T30IafMode.ContinuousFlow) != 0)
            set_ctrl_bit(frame, T30ControlBit.DisT38FaxCapable);

        if (!string.IsNullOrEmpty(state.RxFile))
            set_ctrl_bit(frame, T30ControlBit.DisReadyToReceiveFaxDocument);
        if (!string.IsNullOrEmpty(state.TxFile))
            set_ctrl_bit(frame, T30ControlBit.DisReadyToTransmitFaxDocument);

        state.LocalDisDtcLength = 19;
        return 0;
    }
}
