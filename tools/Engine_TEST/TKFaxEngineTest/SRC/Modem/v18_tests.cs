/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * v18_tests.cs - complete managed port of v18_tests.c.
 *
 * Original test written by Steve Underwood.
 * Licensed under the GNU General Public License version 2.
 */

#nullable enable
using SpanLogState = TKFaxEngine.SpanLogState;

using System.Globalization;
using System.Text;
using TKFaxEngine.Audio;
using TKFaxEngine.Modem;
using TKFaxEngine.Modem.V18;
using static TKFaxEngine.Audio.g711;
using static TKFaxEngine.Audio.g726;
using static TKFaxEngine.Audio.tone_generate;
using static TKFaxEngine.LoggingApi;
using static TKFaxEngine.Modem.FskApi;
using static TKFaxEngine.Modem.V18.V18Api;

namespace TKFaxEngineTest.Modem.Test;

public static class v18_tests {
    private const string OUTPUT_FILE_NAME = "v18.wav";
    private const int SAMPLES_PER_CHUNK = 160;
    private const int CHUNKS_PER_SECOND = 50;
    private const int TESTER = 0;
    private const int TUT = 1;
    private const int V18_AUTOMODING_GLOBAL = 0;
    private const int V18_AUTOMODING_NONE = 1;
    private const int V18_AUTOMODING_USA = 16;
    private const int ASYNC_PARITY_EVEN = 1;
    private const int SIG_STATUS_CARRIER_UP = -2;
    private const int FSK_FRAME_MODE_FRAMED = 2;

    private static bool log_audio;
    private static WaveFile? outhandle;
    private static readonly byte[][] result = CreateJagged<byte>(2, 1024);
    private static bool unexpected_echo;
    private static string? decode_test_file;
    private static bool good_message_received;
    private static BothWaysLineModelState? model;
    private static int rbs_pattern = 0;
    private static int line_model_no = 0;
    private static float echo_level_cpe1 = -99.0f;
    private static float echo_level_co1 = -99.0f;
    private static float echo_level_cpe2 = -99.0f;
    private static float echo_level_co2 = -99.0f;
    private static float noise_level = -70.0f;
    private static int channel_codec = MUNGE_CODEC_NONE;
    private static readonly V18State?[] v18 = new V18State?[2];
    private const string qbf_tx = "The quick Brown Fox Jumps Over The Lazy dog 0123456789!@#$%^&*()'";
    private const string qbf_rx = "THE QUICK BROWN FOX JUMPS OVER THE LAZY DOG 0123456789!X$$/'+.()'";
    private const string full_baudot_rx = "\b \n\n\n\r?\n\n\n  !\"$$/+'().+,-./0123456789:;(=)?XABCDEFGHIJKLMNOPQRSTUVWXYZ(/)' 'ABCDEFGHIJKLMNOPQRSTUVWXYZ(!) ";
    private static int x_06_seq;
    private static int x_06_timeout;

    private sealed record TestEntry(string Title, Func<int>? Run);

    private static readonly TestEntry[] TestList =
    {
        new("III.3.2.1 Operational requirements tests", null),
        new("MISC-01         4 (1)       No disconnection test", test_misc_01),
        new("MISC-02         4 (2)       Automatic resumption of automoding", test_misc_02),
        new("MISC-03         4 (2)       Retention of selected mode on loss of signal", test_misc_03),
        new("MISC-04         4 (4)       Detection of BUSY tone", test_misc_04),
        new("MISC-05         4 (4)       Detection of RINGING", test_misc_05),
        new("MISC-06         4 (4)       LOSS OF CARRIER indication", test_misc_06),
        new("MISC-07         4 (4)       Call progress indication", test_misc_07),
        new("MISC-08         4 (5)       Circuit 135 test", test_misc_08),
        new("MISC-09         4 (6)       Connection procedures", test_misc_09),
        new("III.3.2.2 Automode originate tests", null),
        new("ORG-01          5.1.1       CI & XCI signal coding and cadence", test_org_01),
        new("ORG-02          5.1.3       ANS signal detection", test_org_02),
        new("ORG-03          5.2.3.1     End of ANS signal detection", test_org_03),
        new("ORG-04          5.1.3.2     ANS tone followed by TXP", test_org_04),
        new("ORG-05          5.1.3.3     ANS tone followed by 1650Hz", test_org_05),
        new("ORG-06          5.1.3.4     ANS tone followed by 1300Hz", test_org_06),
        new("ORG-07          5.1.3       ANS tone followed by no tone", test_org_07),
        new("ORG-08          5.1.4       Bell 103 (2225Hz signal) detection", test_org_08),
        new("ORG-09          5.1.5       V.21 (1650Hz signal) detection", test_org_09),
        new("ORG-10          5.1.6       V.23 (1300Hz signal) detection", test_org_10),
        new("ORG-11          5.1.7       V.23 (390Hz signal) detection", test_org_11),
        new("ORG-12 a to d   5.1.8       5 bit mode (Baudot) detection tests", test_org_12),
        new("ORG-13          5.1.9       DTMF signal detection", test_org_13),
        new("ORG-14          5.1.10      EDT rate detection", test_org_14),
        new("ORG-15          5.1.10.1    Rate detection test", test_org_15),
        new("ORG-16          5.1.10.2    980Hz detection", test_org_16),
        new("ORG-17          5.1.10.3    Loss of signal after 980Hz", test_org_17),
        new("ORG-18          5.1.10.3    Tr Timer", test_org_18),
        new("ORG-19          5.1.11      Bell 103 (1270Hz signal) detection", test_org_19),
        new("ORG-20                      Immunity to network tones", test_org_20),
        new("ORG-21 a to b               Immunity to other non-textphone modems", test_org_21),
        new("ORG-22                      Immunity to FAX tones", test_org_22),
        new("ORG-23                      Immunity to voice", test_org_23),
        new("ORG-24          5.1.2       ANSam detection", test_org_24),
        new("ORG-25          6.1         V.8 originate call", test_org_25),
        new("III.3.2.3 Automode answer tests", null),
        new("ANS-01          5.2.1       Ta timer", test_ans_01),
        new("ANS-02          5.2.2       CI signal detection", test_ans_02),
        new("ANS-03          5.2.2.1     Early termination of ANS tone", test_ans_03),
        new("ANS-04          5.2.2.2     Tt timer", test_ans_04),
        new("ANS-05          5.2.3.2     ANS tone followed by 980Hz", test_ans_05),
        new("ANS-06          5.2.3.2     ANS tone followed by 1300Hz", test_ans_06),
        new("ANS-07          5.2.3.3     ANS tone followed by 1650Hz", test_ans_07),
        new("ANS-08          5.2.4.1     980Hz followed by 1650Hz", test_ans_08),
        new("ANS-09 a to d   5.2.4.2     980Hz calling tone detection", test_ans_09),
        new("ANS-10          5.2.4.3     V.21 detection by timer", test_ans_10),
        new("ANS-11          5.2.4.4.1   EDT detection by rate", test_ans_11),
        new("ANS-12          5.2.4.4.2   V.21 detection by rate", test_ans_12),
        new("ANS-13          5.2.4.4.3   Tr timer", test_ans_13),
        new("ANS-14          5.2.4.5     Te timer", test_ans_14),
        new("ANS-15 a to d   5.2.5       5 bit mode (Baudot) detection tests", test_ans_15),
        new("ANS-16          5.2.6       DTMF signal detection", test_ans_16),
        new("ANS-17          5.2.7       Bell 103 (1270Hz signal) detection", test_ans_17),
        new("ANS-18          5.2.8       Bell 103 (2225Hz signal) detection", test_ans_18),
        new("ANS-19          5.2.9       V.21 reverse mode (1650Hz) detection", test_ans_19),
        new("ANS-20 a to d   5.2.10      1300Hz calling tone discrimination", test_ans_20),
        new("ANS-21          5.2.11      V.23 reverse mode (1300Hz) detection", test_ans_21),
        new("ANS-22                      1300Hz with XCI Test", test_ans_22),
        new("ANS-23          5.2.12      Stimulate mode country settings", test_ans_23),
        new("ANS-24          5.2.12.1    Stimulate carrierless mode probe message", test_ans_24),
        new("ANS-25          5.2.12.1.1  Interrupted carrierless mode probe", test_ans_25),
        new("ANS-26          5.2.12.2    Stimulate carrier mode probe time", test_ans_26),
        new("ANS-27          5.2.12.2.1  V.23 mode (390Hz) detection", test_ans_27),
        new("ANS-28          5.2.12.2.2  Interrupted carrier mode probe", test_ans_28),
        new("ANS-29          5.2.12.2.2  Stimulate mode response during probe", test_ans_29),
        new("ANS-30                      Immunity to network tones", test_ans_30),
        new("ANS-31                      Immunity to FAX calling tones", test_ans_31),
        new("ANS-32                      Immunity to voice", test_ans_32),
        new("ANS-33          5.2.2.1     V.8 CM detection and V.8 answering", test_ans_33),
        new("III.3.2.4 Automode monitor tests", null),
        new("MON-01          5.3         ANS-02, but with mode monitor as defined in 5.3", test_mon_01),
        new("MON-02          5.3         ANS-03, but with mode monitor as defined in 5.3", test_mon_02),
        new("MON-03          5.3         ANS-04, but with mode monitor as defined in 5.3", test_mon_03),
        new("MON-04          5.3         ANS-05, but with mode monitor as defined in 5.3", test_mon_04),
        new("MON-05          5.3         ANS-06, but with mode monitor as defined in 5.3", test_mon_05),
        new("MON-06          5.3         ANS-07, but with mode monitor as defined in 5.3", test_mon_06),
        new("MON-07          5.3         ANS-08, but with mode monitor as defined in 5.3", test_mon_07),
        new("MON-08          5.3         ANS-09, but with mode monitor as defined in 5.3", test_mon_08),
        new("MON-09          5.3         ANS-11, but with mode monitor as defined in 5.3", test_mon_09),
        new("MON-10          5.3         ANS-12, but with mode monitor as defined in 5.3", test_mon_10),
        new("MON-11          5.3         ANS-13, but with mode monitor as defined in 5.3", test_mon_11),
        new("MON-12          5.3         ANS-14, but with mode monitor as defined in 5.3", test_mon_12),
        new("MON-13          5.3         ANS-15, but with mode monitor as defined in 5.3", test_mon_13),
        new("MON-14          5.3         ANS-16, but with mode monitor as defined in 5.3", test_mon_14),
        new("MON-15          5.3         ANS-17, but with mode monitor as defined in 5.3", test_mon_15),
        new("MON-16          5.3         ANS-18, but with mode monitor as defined in 5.3", test_mon_16),
        new("MON-17          5.3         ANS-19, but with mode monitor as defined in 5.3", test_mon_17),
        new("MON-18          5.3         ANS-21, but with mode monitor as defined in 5.3", test_mon_18),
        new("MON-19          5.3         ANS-22, but with mode monitor as defined in 5.3", test_mon_19),
        new("MON-20          5.3         ANS-30, but with mode monitor as defined in 5.3", test_mon_20),
        new("MON-21          5.3         Automode monitor Ta timer", test_mon_21),
        new("MON-22 a to d   5.3         Automode monitor 1300Hz calling tone discrimination", test_mon_22),
        new("MON-23 a to d   5.3         Automode monitor 980Hz calling tone discrimination", test_mon_23),
        new("III.3.2.5 ITU-T V.18 annexes tests", null),
        new("X-01            A.1         Baudot carrier timing and receiver disabling", test_x_01),
        new("X-02            A.2         Baudot bit rate confirmation", test_x_02),
        new("X-03            A.3         Baudot probe bit rate confirmation", test_x_03),
        new("X-04            A.4         5 Bit to T.50 character conversion", test_x_04),
        new("X-05            B.1         DTMF receiver disabling", test_x_05),
        new("X-06            B.2         DTMF character conversion", test_x_06),
        new("X-07            C.1         EDT carrier timing and receiver disabling", test_x_07),
        new("X-08            C.2-3       EDT bit rate and character structure", test_x_08),
        new("X-09            E           V.23 calling mode character format", test_x_09),
        new("X-10            E           V.23 answer mode character format", test_x_10),
        new("X-11            F.4-5       V.21 character structure", test_x_11),
        new("X-12            G.1-3       V.18 mode", test_x_12),
        new("", null),
    };

    public static int main(string[] testArguments) {
        try {
            string? match = null;
            int testStandard = -1;
            int index = 0;
            while (index < testArguments.Length) {
                string option = testArguments[index];
                if (option == "-d")
                    decode_test_file = NextArgument(testArguments, ref index, option);
                else if (option == "-l")
                    log_audio = true;
                else if (option == "-s")
                    testStandard = int.Parse(NextArgument(testArguments, ref index, option), CultureInfo.InvariantCulture);
                else if (option.StartsWith('-'))
                    Exit(2);
                else
                    break;
                index++;
            }

            if (decode_test_file is not null)
                return decode_test_data_file(testStandard, decode_test_file);

            if (index < testArguments.Length)
                match = testArguments[index];

            if (log_audio)
                outhandle = WaveFile.OpenWrite(OUTPUT_FILE_NAME, 2);

            bool hit = false;
            foreach (TestEntry entry in TestList) {
                if (entry.Title.Length == 0)
                    break;

                bool selected = entry.Run is not null
                    && (match is null
                        || (entry.Title.StartsWith(match, StringComparison.Ordinal)
                            && entry.Title.Length > match.Length
                            && entry.Title[match.Length] == ' '));

                if (selected) {
                    hit = true;
                    Console.WriteLine(entry.Title);
                    int resultCode = entry.Run!();
                    if (resultCode < 0) {
                        Console.WriteLine("    Test failed");
                        Exit(2);
                    }
                    if (resultCode == 0)
                        Console.WriteLine("    Test passed");
                } else if (match is null) {
                    Console.WriteLine(entry.Title);
                }
            }

            if (!hit) {
                Console.WriteLine("Test not found");
                Exit(2);
            }

            if (log_audio && outhandle is not null && outhandle.Close() != 0) {
                Console.Error.WriteLine($"    Cannot close audio file '{OUTPUT_FILE_NAME}'");
                Exit(2);
            }

            Console.WriteLine("Tests passed");
            return 0;
        } catch (V18TestsExitException ex) {
            return ex.ExitCode;
        }
    }
    private static int silence_pad(short[] amp, int actual_samples, int target_samples) {
        if ((actual_samples < target_samples)) {
            Array.Clear(amp, actual_samples, (target_samples - actual_samples));
            actual_samples = target_samples;
        }
        return actual_samples;
    }

    private static void write_audio_log(short[][] amp, int samples) {
        short[] out_amp = new short[(2 * SAMPLES_PER_CHUNK)];
        int out_frames = 0;
        int i = 0;
        if (log_audio) {
            for (i = 0; i < samples; i++) {
                out_amp[((2 * i) + TESTER)] = amp[TESTER][i];
                out_amp[((2 * i) + TUT)] = amp[TUT][i];
            }
            out_frames = outhandle!.WriteFrames(out_amp, samples);
            if ((out_frames != samples)) {
                Errorf("    Error writing audio file\n");
                Exit(2);
            }
        }
    }

    private static void report_status(object? user_data, int status) {
        Printf("V.18 status report %s (%d)\n", v18_status_to_str(status), status);
    }

    private static void put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        if ((CompareCString(msg, qbf_rx) != 0)) {
            Printf("Result:\n%s\n", Encoding.Latin1.GetString(msg));
            Printf("Reference result:\n%s\n", qbf_rx);
        } else {
            good_message_received = true;
        }
    }

    private static void basic_tests(int mode) {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        Printf("Basic testing %s\n", v18_mode_to_str(mode));
        v18[TESTER] = v18_init(null, true, mode, V18_AUTOMODING_NONE, put_text_msg, null, report_status, null);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, mode, V18_AUTOMODING_NONE, put_text_msg, null, report_status, null);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        good_message_received = true;
        push = 0;
        if ((v18_put(v18[TESTER], qbf_tx, -1) != qbf_tx.Length)) {
            Printf("V.18 put failed\n");
            Exit(2);
        }
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                if (((samples = v18_tx(v18[TESTER], amp[TESTER], SAMPLES_PER_CHUNK)) == 0))
                    push = 10;
            } else {
                samples = 0;
                if ((--push == 0)) {
                    if (!good_message_received) {
                        Printf("No message received\n");
                        Exit(2);
                    }
                    good_message_received = false;
                    if ((v18_put(v18[TESTER], qbf_tx, -1) != qbf_tx.Length)) {
                        Printf("V.18 put failed\n");
                        Exit(2);
                    }
                }
            }
            samples = silence_pad(amp[TESTER], samples, SAMPLES_PER_CHUNK);
            if (((samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK)) == 0))
                push = 10;
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
    }

    private static void misc_01_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_01() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_01_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_01_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < ((10 * 60) * CHUNKS_PER_SECOND); i++) {
            for (j = 0; j < 2; j++) {
                samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK);
                samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_02_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_02() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_02_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_02_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_03_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_03() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_03_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_03_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_04_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_04() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_04_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_04_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_05_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_05() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_05_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_05_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_06_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_06() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_06_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, misc_06_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_07_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_07() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_07_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_07_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_08_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_08() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_08_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_08_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void misc_09_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_misc_09() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_09_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, misc_09_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_01_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static void org_01_put_text_byte(object? user_data, int byteValue) {
        Printf("0x%x received\n", byteValue);
    }

    private static int test_org_01() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        FskRxState fsk_rx_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_01_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1650, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        fsk_rx_state = fsk_rx_init(fsk_rx_state, FskApi.preset_fsk_specs[FSK_V23CH1], FSK_FRAME_MODE_FRAMED, org_01_put_text_byte, null);
        fsk_rx_set_frame_parameters(fsk_rx_state, 7, ASYNC_PARITY_EVEN, 1);
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                if (((samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK)) == 0))
                    push = 10;
            }
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
            fsk_rx(fsk_rx_state, amp[TUT], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_02_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static void org_02_put_text_byte(object? user_data, int ch) {
    }

    private static int test_org_02() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        FskRxState fsk_rx_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_02_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        fsk_rx_state = fsk_rx_init(fsk_rx_state, FskApi.preset_fsk_specs[FSK_V21CH2], FSK_FRAME_MODE_FRAMED, org_02_put_text_byte, v18[TUT]);
        fsk_rx_set_frame_parameters(fsk_rx_state, 7, ASYNC_PARITY_EVEN, 1);
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
            fsk_rx(fsk_rx_state, amp[TUT], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_03_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_03() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_03_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1650, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                if (((samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK)) == 0))
                    push = 10;
            }
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_04_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_04() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_04_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1650, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_05_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_05() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_05_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_05_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_06_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_06() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, org_06_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1300, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                if (((samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK)) == 0))
                    push = 10;
            }
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_07_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_07() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_07_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_07_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_08_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_08() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, org_08_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2225, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_09_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_09() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_09_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1650, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_10_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_10() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, org_10_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1300, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
            samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
            samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_11_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_11() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, org_11_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, org_11_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_12_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        AppendCString(result[i], msg);
        if (((i == TUT) && (CompareCString(result[i], "0123456789ABCDEF") == 0)))
            v18_put(v18[TUT], "abcdef", -1);
    }

    private static int test_org_12() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        int rate = 0;
        int[] rates = new int[4];
        for (rate = 0; rate < 3; rate++) {
            v18[TUT] = v18_init(null, true, rates[rate], V18_AUTOMODING_NONE, org_12_put_text_msg, TUT, report_status, TUT);
            logging = v18_get_logging_state(v18[TUT]);
            span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
            span_log_set_tag(logging, "TUT");
            v18[TESTER] = v18_init(null, false, rates[rate], V18_AUTOMODING_NONE, org_12_put_text_msg, TESTER, report_status, TESTER);
            logging = v18_get_logging_state(v18[TESTER]);
            span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
            span_log_set_tag(logging, "Tester");
            if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
                Errorf("    Failed to create line model\n");
                Exit(2);
            }
            push = 0;
            result[TESTER][0] = (result[TUT][0] = 0);
            v18_put(v18[TESTER], "0123456789abcdef", -1);
            for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
                if ((push == 0)) {
                    for (j = 0; j < 2; j++) {
                        if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                            push = 0;
                        samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                    }
                }
                write_audio_log(amp, samples);
                model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
                v18_rx(v18[TESTER], model_amp[TUT], samples);
                v18_rx(v18[TUT], model_amp[TESTER], samples);
            }
            Printf("Received string is '%s'\n", result[TUT]);
            if ((CompareCString(result[TUT], "0123456789ABCDEF") == 0)) {
                Printf("Test passed\n");
            }
            v18_free(v18[TESTER]);
            v18_free(v18[TUT]);
        }
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_13_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_13() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_13_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_13_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_14_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_14() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, org_14_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, org_14_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TESTER], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_15_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_15() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, org_15_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, org_15_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_16_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_16() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int current_mode = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_16_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_16_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        current_mode = v18_get_current_mode(v18[TUT]);
        if ((current_mode != V18_MODE_V21TEXTPHONE)) {
            Printf("Test failed - mode is %d instead of %d\n", current_mode, V18_MODE_V21TEXTPHONE);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_17_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_17() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_17_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_17_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_18_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_18() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_18_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, org_18_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_19_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static void org_19_put_text_byte(object? user_data, int byteValue) {
        V18State? s = null;
        s = (V18State?)(user_data);
        if ((byteValue == SIG_STATUS_CARRIER_UP))
            v18_put(s, "TEST", 4);
        Printf("0x%x received\n", byteValue);
    }

    private static int test_org_19() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        FskRxState fsk_rx_state = null!;
        int samples = 0;
        int i = 0;
        v18[TUT] = v18_init(null, false, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, org_19_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 1270, -10, 0, 0, 5000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        fsk_rx_state = fsk_rx_init(fsk_rx_state, FskApi.preset_fsk_specs[FSK_BELL103CH2], FSK_FRAME_MODE_FRAMED, org_19_put_text_byte, v18[TUT]);
        fsk_rx_set_frame_parameters(fsk_rx_state, 7, ASYNC_PARITY_EVEN, 1);
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TUT][0] = 0;
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            {
                samples = v18_tx(v18[TUT], amp[TUT], SAMPLES_PER_CHUNK);
                samples = silence_pad(amp[TUT], samples, SAMPLES_PER_CHUNK);
                samples = tone_gen(tone_state, amp[TESTER], SAMPLES_PER_CHUNK);
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
            fsk_rx(fsk_rx_state, amp[TUT], samples);
        }
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_20_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_20() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_20_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_20_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_21_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_21() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, org_21_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, org_21_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_22_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_22() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_22_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_22_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_23_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_23() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_23_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_23_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_24_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_24() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_24_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_24_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void org_25_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_org_25() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TUT] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_25_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        v18[TESTER] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, org_25_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_01_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_01() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_01_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_01_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_02_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_02() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_02_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_02_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_03_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_03() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_03_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_03_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_04_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_04() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_04_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_04_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_05_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_05() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_05_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_05_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_06_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_06() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_06_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_06_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_07_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_07() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_07_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_07_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_08_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_08() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_08_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_08_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_09_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_09() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_09_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_09_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_10_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_10() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_10_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_10_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_11_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_11() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, ans_11_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_EDT, V18_AUTOMODING_GLOBAL, ans_11_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TESTER], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_12_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_12() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_12_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_12_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TESTER], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_13_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_13() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_13_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_13_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_14_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_14() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_14_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_14_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_15_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_15() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, ans_15_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, ans_15_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TESTER], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_16_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_16() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_16_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_16_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_17_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_17() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_17_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_17_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_18_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_18() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_18_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_18_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_19_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_19() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_19_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, ans_19_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_20_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_20() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_20_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_20_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_21_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_21() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_21_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_21_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_22_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_22() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_22_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_22_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_23_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_23() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_23_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_23_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_24_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_24() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_24_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_24_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_25_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_25() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_25_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_25_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_26_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_26() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        tone_gen_descriptor_t tone_desc = null!;
        tone_gen_state_t tone_state = null!;
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_26_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_26_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        tone_desc = tone_gen_descriptor_init(tone_desc, 2100, -10, 0, 0, 2000, 0, 0, 0, 0)!;
        tone_state = tone_gen_init(tone_state, tone_desc!)!;
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_27_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_27() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_27_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, ans_27_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_28_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_28() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_28_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_BELL103, V18_AUTOMODING_GLOBAL, ans_28_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_29_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_29() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_29_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_29_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_30_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_30() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_30_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_30_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_31_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_31() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_31_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_31_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_32_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_32() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_32_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_32_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void ans_33_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_ans_33() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_33_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, ans_33_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_01() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_02() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_03() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_04() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_05() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_06() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_07() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_08() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_09() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_10() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_11() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_12() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_13() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_14() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_15() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_16() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_17() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_18() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_19() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static int test_mon_20() {
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void mon_21_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_mon_21() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, mon_21_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, mon_21_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void mon_22_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_mon_22() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, mon_22_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, mon_22_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void mon_23_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_mon_23() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, mon_23_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V21TEXTPHONE, V18_AUTOMODING_GLOBAL, mon_23_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void x_01_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        Printf("1-1-%d %d '%s'\n", i, len, Encoding.Latin1.GetString(msg));
        AppendCString(result[i], msg);
        if ((i == TESTER))
            v18_put(v18[TESTER], "abcdefghij", 10);
    }

    private static int test_x_01() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        string? reference = null;
        v18[TESTER] = v18_init(null, true, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_NONE, x_01_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_NONE, x_01_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "z", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 0;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        reference = "CDEFGHIJ";
        Printf("Result TESTER:\n%s\n", result[TESTER]);
        Printf("Result TUT:\n%s\n", result[TUT]);
        Printf("Reference result:\n%s\n", reference);
        if ((CompareCString(result[TUT], reference) != 0))
            return -1;
        return 1;
    }

    private static void x_02_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_02() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, x_02_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_GLOBAL, x_02_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void x_03_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_03() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_USA, x_03_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_WEITBRECHT_5BIT_4545, V18_AUTOMODING_USA, x_03_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 1;
    }

    private static void x_04_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        AppendCString(result[i], msg);
        if ((i == TESTER)) {
            Printf("Unexpected ECHO received (%d) '%s'\n", len, Encoding.Latin1.GetString(msg));
            unexpected_echo = true;
        } else {
            Printf("1-1 %d '%s'\n", len, Encoding.Latin1.GetString(msg));
        }
    }

    private static int test_x_04() {
        byte[] msg = new byte[1024];
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, (V18_MODE_WEITBRECHT_5BIT_4545 | V18_MODE_REPETITIVE_SHIFTS_OPTION), V18_AUTOMODING_GLOBAL, x_04_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, (V18_MODE_WEITBRECHT_5BIT_4545 | V18_MODE_REPETITIVE_SHIFTS_OPTION), V18_AUTOMODING_GLOBAL, x_04_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        result[TESTER][0] = (result[TUT][0] = 0);
        unexpected_echo = false;
        for (i = 0; i < 127; i++)
            msg[i] = (byte)(i + 1);
        msg[127] = 0;
        v18_put(v18[TESTER], msg, 127);
        for (i = 0; i < 2000; i++) {
            for (j = 0; j < 2; j++) {
                samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK);
                samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Result:\n%s\n", result[TESTER]);
        Printf("Result:\n%s\n", result[TUT]);
        Printf("Reference result:\n%s\n", full_baudot_rx);
        if ((unexpected_echo || (CompareCString(result[TUT], full_baudot_rx) != 0)))
            return -1;
        return 0;
    }

    private static void x_05_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        AppendCString(result[i], msg);
        if ((i == TESTER)) {
            v18_put(v18[TESTER], "behknqtwz", 9);
        } else {
        }
    }

    private static int test_x_05() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        string? reference = null;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_NONE, x_05_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_NONE, x_05_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "e", 1);
        for (i = 0; i < 1000; i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        reference = "knqtwz";
        Printf("Result TESTER:\n%s\n", result[TESTER]);
        Printf("Result TUT:\n%s\n", result[TUT]);
        Printf("Reference result:\n%s\n", reference);
        if ((CompareCString(result[TUT], reference) != 0))
            return -1;
        return 0;
    }

    private static void x_06_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        byte[] buf = new byte[2];
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        AppendCString(result[i], msg);
        if ((i == TUT)) {
            v18_put(v18[i], msg, len);
        } else {
            if ((x_06_seq <= 127)) {
                buf[0] = (byte)x_06_seq++;
                buf[1] = 0;
                v18_put(v18[i], buf, 1);
                x_06_timeout = MillisecondsToSamples(1500);
            }
        }
    }

    private static int test_x_06() {
        byte[] msg = new byte[128];
        string? reference = null;
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_NONE, x_06_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_NONE, x_06_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        x_06_seq = 1;
        x_06_timeout = 0;
        msg[0] = (byte)x_06_seq++;
        msg[1] = 0;
        v18_put(v18[TESTER], msg, 1);
        x_06_timeout = MillisecondsToSamples(1500);
        for (i = 0; i < ((5 * 60) * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
            if ((x_06_timeout > 0)) {
                x_06_timeout -= samples;
                if ((x_06_timeout <= 0)) {
                    if ((x_06_seq <= 127)) {
                        msg[0] = (byte)x_06_seq++;
                        msg[1] = 0;
                        v18_put(v18[TESTER], msg, 1);
                        x_06_timeout = MillisecondsToSamples(1500);
                    }
                }
            }
        }
        reference = "\b \n\n\n?\n\n\n  !%+().+,-.0123456789:;(=)?XABCDEFGHIJKLMNOPQRSTUVWXYZ\xC6\xD8\xC5 abcdefghijklmnopqrstuvwxyz\xE6\xF8\xE5 \b";
        Printf("Result TESTER:\n%s\n", result[TESTER]);
        Printf("Result TUT:\n%s\n", result[TUT]);
        Printf("Reference result:\n%s\n", reference);
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        if (((CompareCString(result[TESTER], reference) != 0) || (CompareCString(result[TUT], reference) != 0)))
            return -1;
        return 0;
    }

    private static void x_07_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        int i = 0;
        i = Convert.ToInt32(user_data, CultureInfo.InvariantCulture);
        AppendCString(result[i], msg);
        if ((i == TUT)) {
        } else {
            v18_put(v18[i], "abcdef", -1);
        }
    }

    private static int test_x_07() {
        string? reference = null;
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_EDT, V18_AUTOMODING_NONE, x_07_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_EDT, V18_AUTOMODING_NONE, x_07_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "X", 1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        reference = "abcde";
        Printf("Result TESTER:\n%s\n", result[TESTER]);
        Printf("Result TUT:\n%s\n", result[TUT]);
        Printf("Reference result:\n%s\n", reference);
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void x_08_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_08() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_EDT, V18_AUTOMODING_NONE, x_08_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_EDT, V18_AUTOMODING_NONE, x_08_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void x_09_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_09() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_NONE, x_09_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_NONE, x_09_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void x_10_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_10() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, x_10_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_V23VIDEOTEX, V18_AUTOMODING_GLOBAL, x_10_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TESTER], "abcdef", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void x_11_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_11() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, x_11_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, x_11_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef\r\n", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void x_12_put_text_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
    }

    private static int test_x_12() {
        SpanLogState logging;
        short[][] amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        short[][] model_amp = CreateJagged<short>(2, SAMPLES_PER_CHUNK);
        int samples = 0;
        int push = 0;
        int i = 0;
        int j = 0;
        v18[TESTER] = v18_init(null, true, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, x_12_put_text_msg, TESTER, report_status, TESTER);
        logging = v18_get_logging_state(v18[TESTER]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "Tester");
        v18[TUT] = v18_init(null, false, V18_MODE_DTMF, V18_AUTOMODING_GLOBAL, x_12_put_text_msg, TUT, report_status, TUT);
        logging = v18_get_logging_state(v18[TUT]);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "TUT");
        if (((model = BothWaysLineModelState.Create(line_model_no, noise_level, echo_level_cpe1, echo_level_co1, line_model_no, noise_level, echo_level_cpe2, echo_level_co2, channel_codec, rbs_pattern)) == null)) {
            Errorf("    Failed to create line model\n");
            Exit(2);
        }
        push = 0;
        result[TESTER][0] = (result[TUT][0] = 0);
        v18_put(v18[TUT], "abcdef\r\n", -1);
        for (i = 0; i < (60 * CHUNKS_PER_SECOND); i++) {
            if ((push == 0)) {
                for (j = 0; j < 2; j++) {
                    if (((samples = v18_tx(v18[j], amp[j], SAMPLES_PER_CHUNK)) == 0))
                        push = 10;
                    samples = silence_pad(amp[j], samples, SAMPLES_PER_CHUNK);
                }
            }
            write_audio_log(amp, samples);
            model!.Process(model_amp[TESTER], amp[TESTER], model_amp[TUT], amp[TUT], samples);
            v18_rx(v18[TESTER], model_amp[TUT], samples);
            v18_rx(v18[TUT], model_amp[TESTER], samples);
        }
        v18_free(v18[TESTER]);
        v18_free(v18[TUT]);
        Printf("Test not yet implemented\n");
        return 0;
    }

    private static void put_v18_msg(object? user_data, ReadOnlySpan<byte> msg) {
        int len = msg.Length;
        byte[] buf = new byte[1024];
        CopyBytes(buf, msg, len);
        buf[len] = 0;
        Printf("Received (%d bytes) '%s'\n", len, buf);
    }

    private static int decode_test_data_file(int mode, string filename) {
        short[] amp = new short[SAMPLES_PER_CHUNK];
        WaveFile? inhandle = null;
        int len = 0;
        V18State? v18_state = null;
        SpanLogState logging;
        Printf("Decoding as '%s'\n", v18_mode_to_str(mode));
        if (string.IsNullOrWhiteSpace(filename)) {
            Errorf("    No audio input file was specified\n");
            Exit(2);
        }
        if (((inhandle = WaveFile.OpenRead(filename)) == null)) {
            Errorf("    Cannot open audio file '%s'\n", filename);
            Exit(2);
        }
        v18_state = v18_init(null, false, mode, V18_AUTOMODING_GLOBAL, put_v18_msg, null, report_status, null);
        logging = v18_get_logging_state(v18_state);
        span_log_set_level(logging, (((SPAN_LOG_SHOW_TAG | SPAN_LOG_SHOW_SEVERITY) | SPAN_LOG_SHOW_PROTOCOL) | SPAN_LOG_FLOW));
        span_log_set_tag(logging, "");
        for (; ; )
        {
            if (((len = inhandle!.ReadFrames(amp, SAMPLES_PER_CHUNK)) <= 0))
                break;
            v18_rx(v18_state, amp, len);
        }
        if ((inhandle!.Close() != 0)) {
            Errorf("    Cannot close audio file '%s'\n", decode_test_file);
            Exit(2);
        }
        v18_free(v18_state);
        return 0;
    }

    private static SpanLogState v18_get_logging_state(V18State? state)
        => V18Api.v18_get_logging_state(state ?? throw new InvalidOperationException("V.18 state is not initialized."));

    private static int v18_tx(V18State? state, short[] samples, int maximumLength)
        => V18Api.v18_tx(state ?? throw new InvalidOperationException("V.18 state is not initialized."), samples, maximumLength);

    private static int v18_rx(V18State? state, short[] samples, int length)
        => V18Api.v18_rx(state ?? throw new InvalidOperationException("V.18 state is not initialized."), samples, length);

    private static int v18_put(V18State? state, string message, int length)
        => V18Api.v18_put(state ?? throw new InvalidOperationException("V.18 state is not initialized."), message, length);

    private static int v18_put(V18State? state, byte[] message, int length)
        => V18Api.v18_put(state ?? throw new InvalidOperationException("V.18 state is not initialized."), message.AsSpan(0, Math.Min(length, message.Length)));

    private static int v18_put(V18State? state, ReadOnlySpan<byte> message, int length)
        => V18Api.v18_put(
            state ?? throw new InvalidOperationException("V.18 state is not initialized."),
            message[..Math.Min(length, message.Length)]);

    private static int v18_get_current_mode(V18State? state)
        => V18Api.v18_get_current_mode(state ?? throw new InvalidOperationException("V.18 state is not initialized."));

    private static string NextArgument(string[] testArguments, ref int index, string option) {
        index++;
        if (index >= testArguments.Length)
            throw new ArgumentException($"Missing argument for {option}");
        return testArguments[index];
    }

    private static int MillisecondsToSamples(int milliseconds) => milliseconds * 8;

    private static T[][] CreateJagged<T>(int rows, int columns) {
        var value = new T[rows][];
        for (int i = 0; i < rows; i++)
            value[i] = new T[columns];
        return value;
    }

    private static int CompareCString(ReadOnlySpan<byte> bytes, string text)
        => string.CompareOrdinal(Encoding.Latin1.GetString(bytes).TrimEnd('\0'), text);

    private static int CompareCString(byte[] bytes, string text)
        => string.CompareOrdinal(ReadCString(bytes), text);

    private static int CompareCString(string left, string right)
        => string.CompareOrdinal(left, right);

    private static int ComparePrefix(string left, string right, int length)
        => string.CompareOrdinal(left, 0, right, 0, Math.Min(length, Math.Min(left.Length, right.Length)));

    private static void AppendCString(byte[] destination, ReadOnlySpan<byte> source) {
        int offset = Array.IndexOf(destination, (byte)0);
        if (offset < 0) offset = destination.Length;
        int length = Math.Min(source.Length, destination.Length - offset - 1);
        source[..length].CopyTo(destination.AsSpan(offset));
        if (offset + length < destination.Length)
            destination[offset + length] = 0;
    }

    private static void CopyBytes(byte[] destination, ReadOnlySpan<byte> source, int length)
        => source[..Math.Min(length, Math.Min(source.Length, destination.Length))].CopyTo(destination);

    private static string ReadCString(byte[] bytes) {
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        return Encoding.Latin1.GetString(bytes, 0, length);
    }

    private static void Printf(string format, params object?[] args)
        => Console.Write(CFormat(format, args));

    private static void Errorf(string format, params object?[] args)
        => Console.Error.Write(CFormat(format, args));

    private static string CFormat(string format, params object?[] args) {
        var output = new StringBuilder(format.Length + args.Length * 8);
        int argument = 0;
        for (int i = 0; i < format.Length; i++) {
            if (format[i] != '%' || i + 1 >= format.Length) {
                output.Append(format[i]);
                continue;
            }
            if (format[i + 1] == '%') {
                output.Append('%');
                i++;
                continue;
            }
            int j = i + 1;
            while (j < format.Length && "-+ #0".Contains(format[j])) j++;
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
            while (j < format.Length && "hlLzjt".Contains(format[j])) j++;
            if (j >= format.Length || argument >= args.Length)
                break;
            char specifier = format[j];
            object? value = args[argument++];
            string rendered = specifier switch {
                's' => value switch {
                    byte[] b => ReadCString(b),
                    ReadOnlyMemory<byte> m => Encoding.Latin1.GetString(m.Span),
                    _ => value?.ToString() ?? "(null)"
                },
                'c' => Convert.ToChar(value, CultureInfo.InvariantCulture).ToString(),
                'd' or 'i' => Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                'u' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                'x' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString("x", CultureInfo.InvariantCulture),
                'X' => Convert.ToUInt64(value, CultureInfo.InvariantCulture).ToString("X", CultureInfo.InvariantCulture),
                'f' or 'F' => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(precision >= 0 ? $"F{precision}" : "F6", CultureInfo.InvariantCulture),
                'g' or 'G' => Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString(specifier == 'g' ? "g" : "G", CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty
            };
            if (width > rendered.Length)
                rendered = rendered.PadLeft(width);
            output.Append(rendered);
            i = j;
        }
        return output.ToString();
    }

    private static void Exit(int exitCode) => throw new V18TestsExitException(exitCode);

    private sealed class V18TestsExitException : Exception {
        public V18TestsExitException(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    private sealed class WaveFile : IDisposable {
        private readonly FileStream _stream;
        private readonly bool _write;
        private readonly int _channels;
        private int _dataLength;

        private WaveFile(FileStream stream, bool write, int channels) {
            _stream = stream;
            _write = write;
            _channels = channels;
            if (write)
                WriteHeader(0);
            else
                ReadHeader();
        }

        public static WaveFile OpenWrite(string path, int channels)
            => new(new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read), true, channels);

        public static WaveFile OpenRead(string path)
            => new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), false, 1);

        public int WriteFrames(short[] samples, int frames) {
            if (!_write) return 0;
            int count = Math.Min(samples.Length, frames * _channels);
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
            if (_write) return 0;
            int requested = Math.Min(samples.Length, frames * _channels);
            Span<byte> pair = stackalloc byte[2];
            int count = 0;
            while (count < requested && _stream.Read(pair) == 2) {
                samples[count++] = (short)(pair[0] | (pair[1] << 8));
            }
            return count / _channels;
        }

        public int Close() {
            Dispose();
            return 0;
        }

        public void Dispose() {
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

        private void ReadHeader() {
            using var reader = new BinaryReader(_stream, Encoding.ASCII, leaveOpen: true);
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
                throw new InvalidDataException("Not a RIFF wave file.");
            _ = reader.ReadInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE")
                throw new InvalidDataException("Not a WAVE file.");
            while (_stream.Position + 8 <= _stream.Length) {
                string id = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int size = reader.ReadInt32();
                if (id == "fmt ") {
                    short format = reader.ReadInt16();
                    short channels = reader.ReadInt16();
                    int rate = reader.ReadInt32();
                    _ = reader.ReadInt32();
                    _ = reader.ReadInt16();
                    short bits = reader.ReadInt16();
                    if (format != 1 || channels != 1 || rate != 8000 || bits != 16)
                        throw new InvalidDataException("Expected 8 kHz mono 16-bit PCM.");
                    if (size > 16) _stream.Position += size - 16;
                } else if (id == "data") {
                    return;
                } else {
                    _stream.Position += size;
                }
            }
            throw new InvalidDataException("Wave data chunk not found.");
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

        public static BothWaysLineModelState? Create(int model1, float noise1, float cpe1, float co1,
                                                     int model2, float noise2, float cpe2, float co2,
                                                     int codec, int rbsPattern)
            => new(model1, noise1, cpe1, co1, model2, noise2, cpe2, co2, codec, rbsPattern);

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
