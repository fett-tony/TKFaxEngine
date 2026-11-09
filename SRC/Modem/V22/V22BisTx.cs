/*
 * TKFaxEngine - a series of DSP components for telephony
 *
 * V22BisTx.cs - managed C# port of v22bis.h and v22bis_tx.c
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>
 * Copyright (C) 2004 Steve Underwood
 *
 * This file is distributed under the GNU Lesser General Public License
 * version 2.1, matching the original source files.
 */

#nullable enable

namespace TKFaxEngine.Modem.V22;

public sealed partial class V22BisState {
    internal const float TxPulseShaperGain = 1.0f;

    internal static readonly V22BisComplex[] Constellation =
    {
        new( 1.0f,  1.0f),
        new( 3.0f,  1.0f),
        new( 1.0f,  3.0f),
        new( 3.0f,  3.0f),
        new(-1.0f,  1.0f),
        new(-1.0f,  3.0f),
        new(-3.0f,  1.0f),
        new(-3.0f,  3.0f),
        new(-1.0f, -1.0f),
        new(-3.0f, -1.0f),
        new(-1.0f, -3.0f),
        new(-3.0f, -3.0f),
        new( 1.0f, -1.0f),
        new( 1.0f, -3.0f),
        new( 3.0f, -1.0f),
        new( 3.0f, -3.0f)
    };

    public V22BisState(
        int bitRate,
        V22BisOptions options,
        bool callingParty,
        V22BisGetBitHandler? getBit,
        object? getBitUserData,
        V22BisPutBitHandler? putBit,
        object? putBitUserData) {
        Configure(bitRate, options, callingParty, getBit, getBitUserData, putBit, putBitUserData);
    }

    internal void Configure(
        int bitRate,
        V22BisOptions options,
        bool callingParty,
        V22BisGetBitHandler? getBit,
        object? getBitUserData,
        V22BisPutBitHandler? putBit,
        object? putBitUserData) {
        if (bitRate != 1200 && bitRate != 2400) {
            throw new ArgumentOutOfRangeException(nameof(bitRate), "V.22bis supports 1200 or 2400 bit/s.");
        }

        BitRate = bitRate;
        Options = options;
        CallingParty = callingParty;
        GetBitHandler = getBit;
        GetBitUserData = getBitUserData;
        PutBitHandler = putBit;
        PutBitUserData = putBitUserData;
        StatusHandler = null;
        StatusUserData = null;

        Tx.CarrierPhaseRate = V22BisDsp.PhaseRate(callingParty ? LowCarrierFrequency : HighCarrierFrequency);
        Tx.GuardTonePhaseRate = 0;
        if (!callingParty) {
            switch ((int)options & 0xFF) {
                case (int)V22BisOptions.GuardTone550Hz:
                    Tx.GuardTonePhaseRate = V22BisDsp.PhaseRate(550.0f);
                    break;
                case (int)V22BisOptions.GuardTone1800Hz:
                    Tx.GuardTonePhaseRate = V22BisDsp.PhaseRate(1800.0f);
                    break;
            }
        }

        SetTransmitPower(-14.0f);
        Restart(bitRate);
    }

    public int Transmit(Span<short> output) {
        ThrowIfDisposed();
        if (Tx.Shutdown > 10) {
            return 0;
        }

        int sample;
        for (sample = 0; sample < output.Length; sample++) {
            Tx.BaudPhase += 3;
            if (Tx.BaudPhase >= 40) {
                Tx.BaudPhase -= 40;
                V22BisComplex symbol = GetBaud();
                Tx.RrcFilterReal[Tx.RrcFilterStep] = symbol.Real;
                Tx.RrcFilterImaginary[Tx.RrcFilterStep] = symbol.Imaginary;
                if (++Tx.RrcFilterStep >= TxFilterSteps) {
                    Tx.RrcFilterStep = 0;
                }
            }

            int phase = TxPulseShaperCoefficientSets - 1 - Tx.BaudPhase;
            float xReal = CircularDot(Tx.RrcFilterReal, TxPulseShaper[phase], Tx.RrcFilterStep);
            float xImaginary = CircularDot(Tx.RrcFilterImaginary, TxPulseShaper[phase], Tx.RrcFilterStep);
            V22BisComplex carrier = V22BisDsp.NextComplex(ref Tx.CarrierPhase, Tx.CarrierPhaseRate);
            float amplitude = (xReal * carrier.Real - xImaginary * carrier.Imaginary) * Tx.Gain;

            if (Tx.GuardTonePhaseRate != 0 &&
                (Tx.RrcFilterReal[Tx.RrcFilterStep] != 0.0f || Tx.RrcFilterImaginary[Tx.RrcFilterStep] != 0.0f)) {
                amplitude += V22BisDsp.NextModulated(
                    ref Tx.GuardTonePhase,
                    Tx.GuardTonePhaseRate,
                    Tx.GuardToneGain);
            }

            output[sample] = SaturateToInt16(amplitude);
        }

        return sample;
    }

    public void SetTransmitPower(float powerDbm0) {
        ThrowIfDisposed();
        float signalPower;
        float guardTonePower;
        if (Tx.GuardTonePhaseRate == V22BisDsp.PhaseRate(550.0f)) {
            signalPower = powerDbm0 - 1.0f;
            guardTonePower = signalPower - 3.0f;
        } else if (Tx.GuardTonePhaseRate == V22BisDsp.PhaseRate(1800.0f)) {
            signalPower = powerDbm0 - 0.55f;
            guardTonePower = signalPower - 6.0f;
        } else {
            signalPower = powerDbm0;
            guardTonePower = -9999.0f;
        }

        Tx.Gain = 0.4490f * DbToAmplitudeRatio(signalPower - Dbm0MaxSinePower) * 32768.0f / TxPulseShaperGain;
        Tx.GuardToneGain = DbToAmplitudeRatio(guardTonePower - Dbm0MaxSinePower) * 32768.0f;
    }

    public int Restart(int bitRate) {
        ThrowIfDisposed();
        if (bitRate != 1200 && bitRate != 2400) {
            return -1;
        }

        BitRate = bitRate;
        NegotiatedBitRate = 1200;
        if (RestartTransmitter() != 0) {
            return -1;
        }
        return RestartReceiver();
    }

    public int RequestRetrain(int bitRate) {
        ThrowIfDisposed();
        if (bitRate != 1200 && bitRate != 2400) {
            return -1;
        }

        if (Rx.Training != V22BisRxTrainingStage.NormalOperation ||
            Tx.Training != V22BisTxTrainingStage.NormalOperation ||
            NegotiatedBitRate != 2400) {
            return -1;
        }

        Logging.Write("+++ Initiating a retrain");
        Rx.PatternRepeats = 0;
        Rx.TrainingCount = 0;
        Rx.SixteenWayDecisions = false;
        Rx.BitsPerSymbol = 2;
        Rx.SixteenWayTransitionCount = 0;
        Rx.ScrambledOnes2400Count = 0;
        Rx.Training = V22BisRxTrainingStage.ScrambledOnesAt1200;
        Tx.TrainingCount = 0;
        Tx.Training = V22BisTxTrainingStage.Unscrambled0011;
        ResetEqualizerCoefficients();
        ReportStatusChange(V22BisSignalStatus.ModemRetrainOccurred);
        return 0;
    }

    public int RequestRemoteLoopback(bool enable) {
        ThrowIfDisposed();
        _ = enable;
        return -1;
    }

    public void SetGetBitHandler(V22BisGetBitHandler? handler, object? userData) {
        ThrowIfDisposed();
        GetBitHandler = handler;
        GetBitUserData = userData;
        if (Tx.Training == V22BisTxTrainingStage.NormalOperation) {
            Tx.CurrentGetBit = handler;
        }
    }

    public void SetPutBitHandler(V22BisPutBitHandler? handler, object? userData) {
        ThrowIfDisposed();
        PutBitHandler = handler;
        PutBitUserData = userData;
    }

    public void SetModemStatusHandler(V22BisStatusHandler? handler, object? userData) {
        ThrowIfDisposed();
        StatusHandler = handler;
        StatusUserData = userData;
    }

    private int RestartTransmitter() {
        Array.Clear(Tx.RrcFilterReal, 0, Tx.RrcFilterReal.Length);
        Array.Clear(Tx.RrcFilterImaginary, 0, Tx.RrcFilterImaginary.Length);
        Tx.RrcFilterStep = 0;
        Tx.ScrambleRegister = 0;
        Tx.ScramblerPatternCount = 0;
        Tx.Training = CallingParty
            ? V22BisTxTrainingStage.InitialSilence
            : V22BisTxTrainingStage.InitialTimedSilence;
        Tx.TrainingCount = 0;
        Tx.CarrierPhase = 0;
        Tx.GuardTonePhase = 0;
        Tx.BaudPhase = 0;
        Tx.ConstellationState = 0;
        Tx.CurrentGetBit = FakeGetBit;
        Tx.Shutdown = 0;
        return 0;
    }

    private static int FakeGetBit(object? userData) {
        _ = userData;
        return 1;
    }

    private int Scramble(int bit) {
        if (Tx.ScramblerPatternCount >= 64) {
            bit ^= 1;
            Tx.ScramblerPatternCount = 0;
        }

        int output = (bit ^ (int)(Tx.ScrambleRegister >> 13) ^ (int)(Tx.ScrambleRegister >> 16)) & 1;
        Tx.ScrambleRegister = unchecked((Tx.ScrambleRegister << 1) | (uint)output);
        if (output == 1) {
            Tx.ScramblerPatternCount++;
        } else {
            Tx.ScramblerPatternCount = 0;
        }
        return output;
    }

    private int GetScrambledBit() {
        int bit = Tx.CurrentGetBit?.Invoke(GetBitUserData) ?? V22BisSignalStatus.EndOfData;
        if (bit == V22BisSignalStatus.EndOfData) {
            Tx.CurrentGetBit = FakeGetBit;
            Tx.Shutdown = 1;
            bit = 1;
        }
        return Scramble(bit);
    }

    private V22BisComplex GetTrainingSymbol() {
        switch (Tx.Training) {
            case V22BisTxTrainingStage.InitialTimedSilence:
                Tx.TrainingCount++;
                if (Tx.TrainingCount >= MillisecondsToSymbols(75)) {
                    Tx.TrainingCount = 0;
                    if ((Options & V22BisOptions.UseUnscrambledZeroes) != 0) {
                        Logging.Write("+++ starting U00 1200");
                        Tx.Training = V22BisTxTrainingStage.UnscrambledZeroes;
                    } else {
                        Logging.Write("+++ starting U11 1200");
                        Tx.Training = V22BisTxTrainingStage.UnscrambledOnes;
                    }
                }
                Tx.ConstellationState = 0;
                return default;

            case V22BisTxTrainingStage.InitialSilence:
                Tx.ConstellationState = 0;
                return default;

            case V22BisTxTrainingStage.UnscrambledOnes:
                Tx.ConstellationState = (Tx.ConstellationState + PhaseSteps[3]) & 3;
                return Constellation[(Tx.ConstellationState << 2) | 0x01];

            case V22BisTxTrainingStage.UnscrambledZeroes:
                Tx.ConstellationState = (Tx.ConstellationState + PhaseSteps[0]) & 3;
                return Constellation[(Tx.ConstellationState << 2) | 0x01];

            case V22BisTxTrainingStage.Unscrambled0011:
                Tx.ConstellationState =
                    (Tx.ConstellationState + PhaseSteps[3 * (Tx.TrainingCount & 1)]) & 3;
                Tx.TrainingCount++;
                if (Tx.TrainingCount >= MillisecondsToSymbols(100)) {
                    Logging.Write("+++ starting S11 after U0011");
                    if (CallingParty) {
                        Tx.TrainingCount = 0;
                        Tx.Training = V22BisTxTrainingStage.ScrambledOnes1200;
                    } else {
                        Tx.TrainingCount = MillisecondsToSymbols(756 - (600 - 100));
                        Tx.Training = V22BisTxTrainingStage.TimedScrambledOnes1200;
                    }
                }
                return Constellation[(Tx.ConstellationState << 2) | 0x01];

            case V22BisTxTrainingStage.TimedScrambledOnes1200:
                Tx.TrainingCount++;
                if (Tx.TrainingCount >= MillisecondsToSymbols(756)) {
                    if (NegotiatedBitRate == 2400) {
                        Logging.Write("+++ starting S1111 (C)");
                        Tx.TrainingCount = 0;
                        Tx.Training = V22BisTxTrainingStage.ScrambledOnes2400;
                    } else {
                        Logging.Write("+++ Tx normal operation (1200)");
                        Tx.TrainingCount = 0;
                        Tx.Training = V22BisTxTrainingStage.NormalOperation;
                        ReportStatusChange(V22BisSignalStatus.TrainingSucceeded);
                        Tx.CurrentGetBit = GetBitHandler;
                    }
                }
                goto case V22BisTxTrainingStage.ScrambledOnes1200;

            case V22BisTxTrainingStage.ScrambledOnes1200:
                int bits1200 = Scramble(1);
                bits1200 = (bits1200 << 1) | Scramble(1);
                Tx.ConstellationState = (Tx.ConstellationState + PhaseSteps[bits1200]) & 3;
                return Constellation[(Tx.ConstellationState << 2) | 0x01];

            case V22BisTxTrainingStage.ScrambledOnes2400:
                int quadrantBits = Scramble(1);
                quadrantBits = (quadrantBits << 1) | Scramble(1);
                Tx.ConstellationState = (Tx.ConstellationState + PhaseSteps[quadrantBits]) & 3;
                int pointBits = Scramble(1);
                pointBits = (pointBits << 1) | Scramble(1);
                Tx.TrainingCount++;
                if (Tx.TrainingCount >= MillisecondsToSymbols(200)) {
                    Logging.Write("+++ Tx normal operation (2400)");
                    Tx.TrainingCount = 0;
                    Tx.Training = V22BisTxTrainingStage.NormalOperation;
                    ReportStatusChange(V22BisSignalStatus.TrainingSucceeded);
                    Tx.CurrentGetBit = GetBitHandler;
                }
                return Constellation[(Tx.ConstellationState << 2) | pointBits];

            case V22BisTxTrainingStage.Parked:
            case V22BisTxTrainingStage.NormalOperation:
            default:
                return default;
        }
    }

    private V22BisComplex GetBaud() {
        if (Tx.Training != V22BisTxTrainingStage.NormalOperation) {
            return GetTrainingSymbol();
        }

        if (Tx.Shutdown != 0) {
            Tx.Shutdown++;
            if (Tx.Shutdown > 10) {
                return default;
            }
        }

        int bits = GetScrambledBit();
        bits = (bits << 1) | GetScrambledBit();
        Tx.ConstellationState = (Tx.ConstellationState + PhaseSteps[bits]) & 3;
        if (NegotiatedBitRate == 1200) {
            bits = 0x01;
        } else {
            bits = GetScrambledBit();
            bits = (bits << 1) | GetScrambledBit();
        }
        return Constellation[(Tx.ConstellationState << 2) | bits];
    }

    private static float DbToAmplitudeRatio(float decibels) => MathF.Pow(10.0f, decibels / 20.0f);

    internal static readonly float[][] TxPulseShaper =
    {
        new float[] {
            -0.0047225778f, -0.0084017803f, -0.0087512712f, 0.0088069184f,
            0.511344338f, 0.5113443379f, 0.0088069183f, -0.0087512713f,
            -0.0084017804f
        },
        new float[] {
            -0.0044560618f, -0.0089299803f, -0.0111430058f, 0.0023375914f,
            0.5628832678f, 0.4603563095f, 0.0144879368f, -0.0063308256f,
            -0.0077375837f
        },
        new float[] {
            -0.004095576f, -0.0093085526f, -0.0134608698f, -0.0048652138f,
            0.6146394096f, 0.4102392982f, 0.0193418847f, -0.0039255915f,
            -0.0069531334f
        },
        new float[] {
            -0.0036459239f, -0.0095262937f, -0.0156592365f, -0.0127304055f,
            0.666268476f, 0.3612970646f, 0.0233456693f, -0.0015775347f,
            -0.0060659402f
        },
        new float[] {
            -0.0031137075f, -0.0095747072f, -0.0176928207f, -0.0211706529f,
            0.7174187175f, 0.3138144545f, 0.0264912753f, 0.0006739941f,
            -0.0050949167f
        },
        new float[] {
            -0.0025072439f, -0.0094482419f, -0.0195175138f, -0.0300826323f,
            0.7677341876f, 0.2680550875f, 0.028784996f, 0.0027928498f,
            -0.0040599953f
        },
        new float[] {
            -0.0018364497f, -0.0091444835f, -0.0210912326f, -0.0393475015f,
            0.8168580988f, 0.2242593163f, 0.0302465047f, 0.0047466057f,
            -0.0029817394f
        },
        new float[] {
            -0.0011126915f, -0.0086642933f, -0.022374767f, -0.0488316051f,
            0.8644362339f, 0.1826424754f, 0.0309077828f, 0.0065069844f,
            -0.0018809534f
        },
        new float[] {
            -0.0003486069f, -0.0080118919f, -0.0233326129f, -0.0583874086f,
            0.9101203735f, 0.1433934355f, 0.0308119288f, 0.0080502012f,
            -0.0007782987f
        },
        new float[] {
            0.0004421024f, -0.0071948838f, -0.0239337749f, -0.0678546569f,
            0.953571701f, 0.1066734725f, 0.0300118652f, 0.0093572183f,
            0.0003060773f
        },
        new float[] {
            0.0012449022f, -0.0062242203f, -0.0241525253f, -0.0770617505f,
            0.9944641461f, 0.0726154624f, 0.0285689687f, 0.0104139084f,
            0.0013528931f
        },
        new float[] {
            0.002044678f, -0.0051141006f, -0.0239691028f, -0.0858273268f,
            1.0324876292f, 0.0413234009f, 0.0265516432f, 0.0112111267f,
            0.0023440603f
        },
        new float[] {
            0.0028260046f, -0.003881811f, -0.0233703397f, -0.0939620349f,
            1.0673511678f, 0.0128722504f, 0.0240338606f, 0.0117446955f,
            0.0032629808f
        },
        new float[] {
            0.003573427f, -0.0025475009f, -0.0223502003f, -0.1012704845f,
            1.0987858104f, -0.0126918924f, 0.0210936884f, 0.0120153024f,
            0.0040948092f
        },
        new float[] {
            0.0042717488f, -0.0011339026f, -0.020910223f, -0.1075533516f,
            1.1265473618f, -0.0353513151f, 0.0178118295f, 0.0120283182f,
            0.0048266775f
        },
        new float[] {
            0.0049063228f, 0.0003340074f, -0.0190598496f, -0.1126096167f,
            1.1504188697f, -0.0551159095f, 0.0142701913f, 0.0117935391f,
            0.0054478776f
        },
        new float[] {
            0.0054633384f, 0.0018293973f, -0.0168166358f, -0.1162389117f,
            1.1702128427f, -0.0720221048f, 0.010550505f, 0.0113248618f,
            0.005950001f
        },
        new float[] {
            0.0059301001f, 0.0033240149f, -0.0142063325f, -0.1182439493f,
            1.1857731729f, -0.0861315367f, 0.0067330149f, 0.0106398965f,
            0.0063270333f
        },
        new float[] {
            0.0062952925f, 0.0047886625f, -0.0112628316f, -0.118433005f,
            1.196976741f, -0.0975294719f, 0.0028952508f, 0.0097595295f,
            0.0065754026f
        },
        new float[] {
            0.0065492257f, 0.0061937044f, -0.0080279717f, -0.1166224228f,
            1.2037346856f, -0.1063230135f, -0.000889099f, 0.0087074424f,
            0.0066939837f
        },
        new float[] {
            0.0066840571f, 0.0075095982f, -0.0045512015f, -0.1126391135f,
            1.2059933196f, -0.1126391136f, -0.0045512015f, 0.0075095982f,
            0.0066840571f
        },
        new float[] {
            0.0066939837f, 0.0087074424f, -0.0008890989f, -0.1063230133f,
            1.2037346856f, -0.1166224229f, -0.0080279717f, 0.0061937043f,
            0.0065492257f
        },
        new float[] {
            0.0065754026f, 0.0097595295f, 0.0028952508f, -0.0975294718f,
            1.196976741f, -0.1184330051f, -0.0112628316f, 0.0047886624f,
            0.0062952925f
        },
        new float[] {
            0.0063270333f, 0.0106398965f, 0.006733015f, -0.0861315366f,
            1.1857731728f, -0.1182439494f, -0.0142063325f, 0.0033240148f,
            0.0059301001f
        },
        new float[] {
            0.0059500011f, 0.0113248618f, 0.0105505051f, -0.0720221047f,
            1.1702128427f, -0.1162389118f, -0.0168166358f, 0.0018293973f,
            0.0054633383f
        },
        new float[] {
            0.0054478776f, 0.0117935392f, 0.0142701913f, -0.0551159094f,
            1.1504188696f, -0.1126096168f, -0.0190598496f, 0.0003340074f,
            0.0049063228f
        },
        new float[] {
            0.0048266775f, 0.0120283182f, 0.0178118296f, -0.035351315f,
            1.1265473617f, -0.1075533517f, -0.020910223f, -0.0011339027f,
            0.0042717488f
        },
        new float[] {
            0.0040948093f, 0.0120153025f, 0.0210936884f, -0.0126918922f,
            1.0987858104f, -0.1012704846f, -0.0223502004f, -0.002547501f,
            0.003573427f
        },
        new float[] {
            0.0032629808f, 0.0117446956f, 0.0240338606f, 0.0128722504f,
            1.0673511678f, -0.0939620349f, -0.0233703397f, -0.003881811f,
            0.0028260046f
        },
        new float[] {
            0.0023440604f, 0.0112111268f, 0.0265516433f, 0.041323401f,
            1.0324876291f, -0.0858273269f, -0.0239691029f, -0.0051141007f,
            0.002044678f
        },
        new float[] {
            0.0013528931f, 0.0104139084f, 0.0285689687f, 0.0726154626f,
            0.994464146f, -0.0770617506f, -0.0241525253f, -0.0062242203f,
            0.0012449021f
        },
        new float[] {
            0.0003060773f, 0.0093572184f, 0.0300118653f, 0.1066734727f,
            0.9535717008f, -0.067854657f, -0.0239337749f, -0.0071948838f,
            0.0004421024f
        },
        new float[] {
            -0.0007782987f, 0.0080502012f, 0.0308119288f, 0.1433934356f,
            0.9101203734f, -0.0583874087f, -0.0233326129f, -0.008011892f,
            -0.0003486069f
        },
        new float[] {
            -0.0018809534f, 0.0065069844f, 0.0309077829f, 0.1826424756f,
            0.8644362338f, -0.0488316052f, -0.0223747671f, -0.0086642933f,
            -0.0011126915f
        },
        new float[] {
            -0.0029817393f, 0.0047466058f, 0.0302465047f, 0.2242593164f,
            0.8168580986f, -0.0393475016f, -0.0210912327f, -0.0091444836f,
            -0.0018364498f
        },
        new float[] {
            -0.0040599952f, 0.0027928498f, 0.0287849961f, 0.2680550877f,
            0.7677341874f, -0.0300826324f, -0.0195175138f, -0.009448242f,
            -0.002507244f
        },
        new float[] {
            -0.0050949167f, 0.0006739941f, 0.0264912753f, 0.3138144546f,
            0.7174187174f, -0.021170653f, -0.0176928207f, -0.0095747072f,
            -0.0031137075f
        },
        new float[] {
            -0.0060659402f, -0.0015775347f, 0.0233456693f, 0.3612970648f,
            0.6662684759f, -0.0127304056f, -0.0156592365f, -0.0095262938f,
            -0.0036459239f
        },
        new float[] {
            -0.0069531333f, -0.0039255914f, 0.0193418848f, 0.4102392984f,
            0.6146394095f, -0.0048652138f, -0.0134608698f, -0.0093085527f,
            -0.004095576f
        },
        new float[] {
            -0.0077375836f, -0.0063308256f, 0.0144879368f, 0.4603563097f,
            0.5628832676f, 0.0023375914f, -0.0111430058f, -0.0089299803f,
            -0.0044560618f
        }
    };
}

public static partial class V22BisApi {
    public static V22BisState? v22bis_init(
        V22BisState? state,
        int bitRate,
        int options,
        bool callingParty,
        V22BisGetBitHandler? getBit,
        object? getBitUserData,
        V22BisPutBitHandler? putBit,
        object? putBitUserData) {
        if (bitRate != 1200 && bitRate != 2400) {
            return null;
        }

        if (state == null) {
            return new V22BisState(
                bitRate,
                (V22BisOptions)options,
                callingParty,
                getBit,
                getBitUserData,
                putBit,
                putBitUserData);
        }

        state.Configure(
            bitRate,
            (V22BisOptions)options,
            callingParty,
            getBit,
            getBitUserData,
            putBit,
            putBitUserData);
        return state;
    }

    public static int v22bis_tx(V22BisState state, short[] output, int length) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        if ((uint)length > (uint)output.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }
        return state.Transmit(output.AsSpan(0, length));
    }

    public static void v22bis_tx_power(V22BisState state, float power) => state.SetTransmitPower(power);
    public static int v22bis_restart(V22BisState state, int bitRate) => state.Restart(bitRate);
    public static int v22bis_request_retrain(V22BisState state, int bitRate) => state.RequestRetrain(bitRate);
    public static int v22bis_remote_loopback(V22BisState state, bool enable) => state.RequestRemoteLoopback(enable);
    public static int v22bis_get_current_bit_rate(V22BisState state) => state.NegotiatedBitRate;
    public static int v22bis_release(V22BisState state) { ArgumentNullException.ThrowIfNull(state); return 0; }
    public static int v22bis_free(V22BisState state) { state.Dispose(); return 0; }
    public static V22BisLoggingState v22bis_get_logging_state(V22BisState state) => state.Logging;

    public static void v22bis_set_get_bit(
        V22BisState state,
        V22BisGetBitHandler? handler,
        object? userData) => state.SetGetBitHandler(handler, userData);

    public static void v22bis_set_put_bit(
        V22BisState state,
        V22BisPutBitHandler? handler,
        object? userData) => state.SetPutBitHandler(handler, userData);

    public static void v22bis_set_modem_status_handler(
        V22BisState state,
        V22BisStatusHandler? handler,
        object? userData) => state.SetModemStatusHandler(handler, userData);
}
