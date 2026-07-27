/*
 * TKFaxEngine - managed C# port
 *
 * t81_t82_arith_coding.cs
 *
 * Combined and ported from t81_t82_arith_coding.c and
 * t81_t82_arith_coding.h.
 *
 * Original implementation by Steve Underwood.
 * Licensed under the GNU Lesser General Public License version 2.1.
 */

#nullable enable

namespace TKFaxEngine.FaxImage;

/// <summary>
/// Shared constants and the ITU-T T.81/T.82 QM-coder probability table.
/// </summary>
public static class T81T82ArithmeticCoding {
    /// <summary>Number of independent QM-coder probability contexts.</summary>
    public const int ContextCount = 4096;

    /// <summary>The decoder needs more PSCD bytes before it can continue.</summary>
    public const int NeedMoreData = -1;

    /// <summary>
    /// The decoder reached a marker and would have to continue with zero padding.
    /// This value is returned once when <see cref="T81T82ArithmeticDecoder.NoPadding"/>
    /// is enabled.
    /// </summary>
    public const int PaddingStarted = -2;

    internal const byte StuffByte = 0x00;
    internal const byte EscapeByte = 0xFF;

    internal readonly struct Probability {
        public Probability(ushort lsz, byte nlps, byte nmps) {
            Lsz = lsz;
            Nlps = nlps;
            Nmps = nmps;
        }

        public ushort Lsz { get; }
        public byte Nlps { get; }
        public byte Nmps { get; }
    }

    // ITU-T T.82, Table 24.
    internal static readonly Probability[] ProbabilityTable =
    {
        new(0x5A1D, 129, 1),
        new(0x2586, 14, 2),
        new(0x1114, 16, 3),
        new(0x080B, 18, 4),
        new(0x03D8, 20, 5),
        new(0x01DA, 23, 6),
        new(0x00E5, 25, 7),
        new(0x006F, 28, 8),
        new(0x0036, 30, 9),
        new(0x001A, 33, 10),
        new(0x000D, 35, 11),
        new(0x0006, 9, 12),
        new(0x0003, 10, 13),
        new(0x0001, 12, 13),
        new(0x5A7F, 143, 15),
        new(0x3F25, 36, 16),
        new(0x2CF2, 38, 17),
        new(0x207C, 39, 18),
        new(0x17B9, 40, 19),
        new(0x1182, 42, 20),
        new(0x0CEF, 43, 21),
        new(0x09A1, 45, 22),
        new(0x072F, 46, 23),
        new(0x055C, 48, 24),
        new(0x0406, 49, 25),
        new(0x0303, 51, 26),
        new(0x0240, 52, 27),
        new(0x01B1, 54, 28),
        new(0x0144, 56, 29),
        new(0x00F5, 57, 30),
        new(0x00B7, 59, 31),
        new(0x008A, 60, 32),
        new(0x0068, 62, 33),
        new(0x004E, 63, 34),
        new(0x003B, 32, 35),
        new(0x002C, 33, 9),
        new(0x5AE1, 165, 37),
        new(0x484C, 64, 38),
        new(0x3A0D, 65, 39),
        new(0x2EF1, 67, 40),
        new(0x261F, 68, 41),
        new(0x1F33, 69, 42),
        new(0x19A8, 70, 43),
        new(0x1518, 72, 44),
        new(0x1177, 73, 45),
        new(0x0E74, 74, 46),
        new(0x0BFB, 75, 47),
        new(0x09F8, 77, 48),
        new(0x0861, 78, 49),
        new(0x0706, 79, 50),
        new(0x05CD, 48, 51),
        new(0x04DE, 50, 52),
        new(0x040F, 50, 53),
        new(0x0363, 51, 54),
        new(0x02D4, 52, 55),
        new(0x025C, 53, 56),
        new(0x01F8, 54, 57),
        new(0x01A4, 55, 58),
        new(0x0160, 56, 59),
        new(0x0125, 57, 60),
        new(0x00F6, 58, 61),
        new(0x00CB, 59, 62),
        new(0x00AB, 61, 63),
        new(0x008F, 61, 32),
        new(0x5B12, 193, 65),
        new(0x4D04, 80, 66),
        new(0x412C, 81, 67),
        new(0x37D8, 82, 68),
        new(0x2FE8, 83, 69),
        new(0x293C, 84, 70),
        new(0x2379, 86, 71),
        new(0x1EDF, 87, 72),
        new(0x1AA9, 87, 73),
        new(0x174E, 72, 74),
        new(0x1424, 72, 75),
        new(0x119C, 74, 76),
        new(0x0F6B, 74, 77),
        new(0x0D51, 75, 78),
        new(0x0BB6, 77, 79),
        new(0x0A40, 77, 48),
        new(0x5832, 208, 81),
        new(0x4D1C, 88, 82),
        new(0x438E, 89, 83),
        new(0x3BDD, 90, 84),
        new(0x34EE, 91, 85),
        new(0x2EAE, 92, 86),
        new(0x299A, 93, 87),
        new(0x2516, 86, 71),
        new(0x5570, 216, 89),
        new(0x4CA9, 95, 90),
        new(0x44D9, 96, 91),
        new(0x3E22, 97, 92),
        new(0x3824, 99, 93),
        new(0x32B4, 99, 94),
        new(0x2E17, 93, 86),
        new(0x56A8, 223, 96),
        new(0x4F46, 101, 97),
        new(0x47E5, 102, 98),
        new(0x41CF, 103, 99),
        new(0x3C3D, 104, 100),
        new(0x375E, 99, 93),
        new(0x5231, 105, 102),
        new(0x4C0F, 106, 103),
        new(0x4639, 107, 104),
        new(0x415E, 103, 99),
        new(0x5627, 233, 106),
        new(0x50E7, 108, 107),
        new(0x4B85, 109, 103),
        new(0x5597, 110, 109),
        new(0x504F, 111, 107),
        new(0x5A10, 238, 111),
        new(0x5522, 112, 109),
        new(0x59EB, 240, 111),
    };

    internal static void ValidateContext(int context) {
        if ((uint)context >= ContextCount) {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                context,
                $"The arithmetic-coder context must be between 0 and {ContextCount - 1}.");
        }
    }
}

/// <summary>
/// ITU-T T.81/T.82 QM arithmetic encoder.
/// </summary>
public sealed class T81T82ArithmeticEncoder {
    private readonly Action<byte> _outputByteHandler;
    private readonly byte[] _states = new byte[T81T82ArithmeticCoding.ContextCount];

    private uint _a;
    private uint _c;
    private int _stackedEscapeBytes;
    private int _bitCounter;
    private int _buffer;

    /// <summary>
    /// Creates an encoder and initializes all probability contexts.
    /// </summary>
    /// <param name="outputByteHandler">
    /// Receives encoded bytes. Escape bytes are delivered together with the
    /// required stuffed zero byte.
    /// </param>
    public T81T82ArithmeticEncoder(Action<byte> outputByteHandler) {
        _outputByteHandler = outputByteHandler
            ?? throw new ArgumentNullException(nameof(outputByteHandler));

        Restart(reuseProbabilityStates: false);
    }

    /// <summary>
    /// Reinitializes the arithmetic registers.
    /// </summary>
    /// <param name="reuseProbabilityStates">
    /// Keep the current 4096 context states when true; reset them when false.
    /// </param>
    public void Restart(bool reuseProbabilityStates = false) {
        if (!reuseProbabilityStates)
            Array.Clear(_states);

        _c = 0;
        _a = 0x10000;
        _stackedEscapeBytes = 0;
        _bitCounter = 11;
        _buffer = -1;
    }

    /// <summary>
    /// Encodes one binary pixel/symbol in the specified probability context.
    /// </summary>
    public void Encode(int context, int pixel) {
        T81T82ArithmeticCoding.ValidateContext(context);

        if ((uint)pixel > 1)
            throw new ArgumentOutOfRangeException(nameof(pixel), pixel, "The encoded value must be 0 or 1.");

        uint stateIndex = (uint)(_states[context] & 0x7F);
        T81T82ArithmeticCoding.Probability probability =
            T81T82ArithmeticCoding.ProbabilityTable[(int)stateIndex];

        unchecked {
            if ((((pixel << 7) ^ _states[context]) & 0x80) != 0) {
                // T.82 Figure 23 - CODELPS.
                _a -= probability.Lsz;
                if (_a >= probability.Lsz) {
                    _c += _a;
                    _a = probability.Lsz;
                }

                _states[context] = (byte)((_states[context] & 0x80) ^ probability.Nlps);
                RenormalizeEncoder();
            } else {
                // T.82 Figure 24 - CODEMPS.
                _a -= probability.Lsz;
                if (_a < 0x8000) {
                    if (_a < probability.Lsz) {
                        _c += _a;
                        _a = probability.Lsz;
                    }

                    _states[context] = (byte)((_states[context] & 0x80) | probability.Nmps);
                    RenormalizeEncoder();
                }
            }
        }
    }

    /// <summary>
    /// Flushes the remaining arithmetic-code bytes to the output callback.
    /// </summary>
    public void Flush() {
        unchecked {
            // T.82 Figures 28, 29 and 30.
            uint temp = (_c + _a - 1u) & 0xFFFF0000u;
            _c = temp < _c ? temp + 0x8000u : temp;
            _c <<= _bitCounter;

            if (_c > 0x07FFFFFFu) {
                if (_buffer >= 0)
                    OutputStuffedByte(_buffer + 1);

                // Only output zero bytes when a non-zero byte follows.
                if ((_c & 0x07FFF800u) != 0) {
                    while (_stackedEscapeBytes > 0) {
                        OutputStuffedByte(0x00);
                        _stackedEscapeBytes--;
                    }
                }
            } else {
                // This intentionally follows the proven native implementation.
                if (_buffer >= 0)
                    OutputStuffedByte(_buffer);

                while (_stackedEscapeBytes > 0) {
                    OutputStuffedByte(0xFF);
                    _stackedEscapeBytes--;
                }
            }

            // Suppress trailing zero bytes.
            if ((_c & 0x07FFF800u) != 0) {
                OutputStuffedByte((int)((_c >> 19) & 0xFF));
                if ((_c & 0x0007F800u) != 0)
                    OutputStuffedByte((int)((_c >> 11) & 0xFF));
            }
        }
    }

    /// <summary>Copies the 4096 probability states to a caller-provided buffer.</summary>
    public void CopyProbabilityStatesTo(Span<byte> destination) {
        if (destination.Length < _states.Length)
            throw new ArgumentException($"The destination must contain at least {_states.Length} bytes.", nameof(destination));

        _states.AsSpan().CopyTo(destination);
    }

    /// <summary>Loads all 4096 probability states from a caller-provided buffer.</summary>
    public void LoadProbabilityStates(ReadOnlySpan<byte> source) {
        if (source.Length < _states.Length)
            throw new ArgumentException($"The source must contain at least {_states.Length} bytes.", nameof(source));

        source[.._states.Length].CopyTo(_states);
    }

    private void OutputStuffedByte(int value) {
        byte output = unchecked((byte)value);
        _outputByteHandler(output);

        if (output == T81T82ArithmeticCoding.EscapeByte)
            _outputByteHandler(T81T82ArithmeticCoding.StuffByte);
    }

    private void ByteOut() {
        uint temp = _c >> 19;

        if (temp > 0xFF) {
            if (_buffer >= 0)
                OutputStuffedByte(_buffer + 1);

            while (_stackedEscapeBytes > 0) {
                // Carry propagation changes buffered 0xFF bytes to 0x00.
                _outputByteHandler(0x00);
                _stackedEscapeBytes--;
            }

            _buffer = (int)(temp & 0xFF);
        } else if (temp == 0xFF) {
            _stackedEscapeBytes++;
        } else {
            if (_buffer >= 0)
                OutputStuffedByte(_buffer);

            while (_stackedEscapeBytes > 0) {
                OutputStuffedByte(0xFF);
                _stackedEscapeBytes--;
            }

            _buffer = (int)temp;
        }

        _c &= 0x7FFFF;
        _bitCounter = 8;
    }

    private void RenormalizeEncoder() {
        unchecked {
            do {
                _a <<= 1;
                _c <<= 1;
                _bitCounter--;

                if (_bitCounter == 0)
                    ByteOut();
            }
            while (_a < 0x8000);
        }
    }
}

/// <summary>
/// ITU-T T.81/T.82 QM arithmetic decoder.
/// </summary>
public sealed class T81T82ArithmeticDecoder {
    private readonly byte[] _states = new byte[T81T82ArithmeticCoding.ContextCount];

    private byte[] _input = Array.Empty<byte>();
    private int _inputOffset;

    private uint _a;
    private uint _c;
    private int _bitCounter;
    private bool _startup;

    /// <summary>Creates a decoder and resets all probability contexts.</summary>
    public T81T82ArithmeticDecoder() {
        Restart(reuseProbabilityStates: false);
    }

    /// <summary>
    /// When enabled, the first marker that would start implicit zero padding
    /// causes <see cref="T81T82ArithmeticCoding.PaddingStarted"/> to be returned.
    /// The flag is automatically cleared after that return.
    /// </summary>
    public bool NoPadding { get; set; }

    /// <summary>Number of unread bytes in the currently buffered PSCD input.</summary>
    public int RemainingInputBytes => _input.Length - _inputOffset;

    /// <summary>Number of bytes consumed from the currently assigned PSCD block.</summary>
    internal int Consumed => _inputOffset;

    /// <summary>
    /// Reinitializes the arithmetic registers while preserving the currently
    /// assigned input block and offset, as in the native implementation.
    /// </summary>
    public void Restart(bool reuseProbabilityStates = false) {
        if (!reuseProbabilityStates)
            Array.Clear(_states);

        _c = 0;
        _a = 1;
        _bitCounter = 0;
        _startup = true;
        NoPadding = false;
    }

    /// <summary>Replaces the current PSCD input with a new block.</summary>
    public void SetInput(ReadOnlySpan<byte> input) {
        _input = input.ToArray();
        _inputOffset = 0;
    }

    /// <summary>
    /// Appends another PSCD block while retaining all unread bytes. This also
    /// preserves a terminal 0xFF that could not yet be paired with its next byte.
    /// </summary>
    public void AppendInput(ReadOnlySpan<byte> input) {
        if (input.IsEmpty)
            return;

        int remaining = RemainingInputBytes;
        byte[] combined = new byte[remaining + input.Length];

        if (remaining > 0)
            _input.AsSpan(_inputOffset, remaining).CopyTo(combined);

        input.CopyTo(combined.AsSpan(remaining));
        _input = combined;
        _inputOffset = 0;
    }

    /// <summary>
    /// Decodes one binary pixel/symbol.
    /// </summary>
    /// <returns>
    /// 0 or 1 on success, <see cref="T81T82ArithmeticCoding.NeedMoreData"/> when
    /// more input is required, or <see cref="T81T82ArithmeticCoding.PaddingStarted"/>
    /// when <see cref="NoPadding"/> requested notification at the first marker.
    /// </returns>
    public int Decode(int context) {
        T81T82ArithmeticCoding.ValidateContext(context);

        unchecked {
            // T.82 Figure 35 - RENORMD.
            while (_a < 0x8000 || _startup) {
                while (_bitCounter <= 8 && _bitCounter >= 0) {
                    if (_inputOffset >= _input.Length)
                        return T81T82ArithmeticCoding.NeedMoreData;

                    if (_input[_inputOffset] == T81T82ArithmeticCoding.EscapeByte) {
                        if (_inputOffset + 1 >= _input.Length)
                            return T81T82ArithmeticCoding.NeedMoreData;

                        if (_input[_inputOffset + 1] == T81T82ArithmeticCoding.StuffByte) {
                            _c |= 0xFFu << (8 - _bitCounter);
                            _bitCounter += 8;
                            _inputOffset += 2;
                        } else {
                            // A marker terminates the PSCD. Continue with zero padding.
                            _bitCounter = -1;
                            if (NoPadding) {
                                NoPadding = false;
                                return T81T82ArithmeticCoding.PaddingStarted;
                            }
                        }
                    } else {
                        _c |= (uint)_input[_inputOffset++] << (8 - _bitCounter);
                        _bitCounter += 8;
                    }
                }

                _a <<= 1;
                _c <<= 1;

                if (_bitCounter >= 0)
                    _bitCounter--;

                if (_a == 0x10000)
                    _startup = false;
            }

            // T.82 Figure 32 - DECODE.
            uint stateIndex = (uint)(_states[context] & 0x7F);
            T81T82ArithmeticCoding.Probability probability =
                T81T82ArithmeticCoding.ProbabilityTable[(int)stateIndex];

            _a -= probability.Lsz;
            int pixel;

            if ((_c >> 16) >= _a) {
                // T.82 Figure 33 - LPS_EXCHANGE.
                if (_a < probability.Lsz) {
                    _c -= _a << 16;
                    _a = probability.Lsz;
                    pixel = _states[context] >> 7;
                    _states[context] = (byte)((_states[context] & 0x80) | probability.Nmps);
                } else {
                    _c -= _a << 16;
                    _a = probability.Lsz;
                    pixel = 1 - (_states[context] >> 7);
                    _states[context] = (byte)((_states[context] & 0x80) ^ probability.Nlps);
                }
            } else {
                if (_a < 0x8000) {
                    // T.82 Figure 34 - MPS_EXCHANGE.
                    if (_a < probability.Lsz) {
                        pixel = 1 - (_states[context] >> 7);
                        _states[context] = (byte)((_states[context] & 0x80) ^ probability.Nlps);
                    } else {
                        pixel = _states[context] >> 7;
                        _states[context] = (byte)((_states[context] & 0x80) | probability.Nmps);
                    }
                } else {
                    pixel = _states[context] >> 7;
                }
            }

            return pixel;
        }
    }

    /// <summary>Copies the 4096 probability states to a caller-provided buffer.</summary>
    public void CopyProbabilityStatesTo(Span<byte> destination) {
        if (destination.Length < _states.Length)
            throw new ArgumentException($"The destination must contain at least {_states.Length} bytes.", nameof(destination));

        _states.AsSpan().CopyTo(destination);
    }

    /// <summary>Loads all 4096 probability states from a caller-provided buffer.</summary>
    public void LoadProbabilityStates(ReadOnlySpan<byte> source) {
        if (source.Length < _states.Length)
            throw new ArgumentException($"The source must contain at least {_states.Length} bytes.", nameof(source));

        source[.._states.Length].CopyTo(_states);
    }
}
