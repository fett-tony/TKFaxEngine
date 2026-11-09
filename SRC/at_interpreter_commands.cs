/*
 * TKFaxEngine - managed C# port
 *
 * at_interpreter_commands.cs
 *
 * Direct command handlers ported from spanDSP/src/at_interpreter.c.
 * Every command path dispatches through its original at_cmd_* function
 * name and preserves the native parser return position.
 */

using System.Globalization;
using System.Text;

namespace TKFaxEngine;

public sealed partial class AtInterpreterState {
    private int parse_num(string line, ref int position, int maximum) {
        long value = 0;
        bool overflow = false;

        while (position < line.Length && char.IsDigit(line[position])) {
            if (!overflow) {
                value = value * 10 + line[position] - '0';
                if (value > int.MaxValue)
                    overflow = true;
            }
            position++;
        }

        return overflow || value > maximum ? -1 : (int)value;
    }

    private bool parse_out(
        string line,
        ref int position,
        int maximum,
        string? prefix,
        string allowed) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                return parse_num(line, ref position, maximum) >= 0;

            case '?':
                PutResponse((prefix ?? string.Empty) + "0");
                return true;

            default:
                return false;
        }
    }

    private bool parse_2_out(
        string line,
        ref int position,
        int maximum1,
        int maximum2,
        string? prefix,
        string allowed) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                if (parse_num(line, ref position, maximum1) < 0)
                    return false;

                if (position < line.Length && line[position] == ',') {
                    position++;
                    if (parse_num(line, ref position, maximum2) < 0)
                        return false;
                }

                return true;

            case '?':
                PutResponse((prefix ?? string.Empty) + "0,0");
                return true;

            default:
                return false;
        }
    }

    private bool parse_n_out(
        string line,
        ref int position,
        ReadOnlySpan<int> maximums,
        int entries,
        string? prefix,
        string allowed) {
        if (position >= line.Length || maximums.Length < entries)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                for (int index = 0; index < entries; index++) {
                    if (parse_num(line, ref position, maximums[index]) < 0)
                        return false;

                    if (position >= line.Length || line[position] != ',')
                        break;

                    position++;
                }

                return true;

            case '?': {
                    StringBuilder response = new(prefix ?? string.Empty);
                    for (int index = 0; index < entries; index++) {
                        if (index > 0)
                            response.Append(',');
                        response.Append('0');
                    }
                    PutResponse(response.ToString());
                    return true;
                }

            default:
                return false;
        }
    }

    private bool parse_out(
        string line,
        ref int position,
        ref int target,
        int maximum,
        string? prefix,
        string allowed) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                int value = parse_num(line, ref position, maximum);
                if (value < 0)
                    return false;

                target = value;
                return true;

            case '?':
                PutResponse(
                    (prefix ?? string.Empty) +
                    target.ToString(CultureInfo.InvariantCulture));
                return true;

            default:
                return false;
        }
    }

    private bool parse_2_out(
        string line,
        ref int position,
        ref int target1,
        int maximum1,
        ref int target2,
        int maximum2,
        string? prefix,
        string allowed) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                int value1 = parse_num(line, ref position, maximum1);
                if (value1 < 0)
                    return false;
                target1 = value1;

                if (position < line.Length && line[position] == ',') {
                    position++;
                    int value2 = parse_num(line, ref position, maximum2);
                    if (value2 < 0)
                        return false;
                    target2 = value2;
                }
                return true;

            case '?':
                PutResponse(
                    (prefix ?? string.Empty) +
                    target1.ToString(CultureInfo.InvariantCulture) + "," +
                    target2.ToString(CultureInfo.InvariantCulture));
                return true;

            default:
                return false;
        }
    }

    private int parse_hex_num(
        string line,
        ref int position,
        int maximum) {
        if (position >= line.Length)
            return -1;

        int value;
        char character = line[position];
        if (char.IsAsciiDigit(character))
            value = character - '0';
        else if (character is >= 'A' and <= 'F')
            value = character - 'A';
        else
            return -1;
        position++;

        if (position >= line.Length)
            return -1;

        character = line[position];
        if (char.IsAsciiDigit(character))
            value = (value << 4) | (character - '0');
        else if (character is >= 'A' and <= 'F')
            value = (value << 4) | (character - 'A');
        else
            return -1;
        position++;

        return value > maximum ? -1 : value;
    }

    private int match_element(
        string line,
        ref int position,
        string variants) {
        ReadOnlySpan<char> variant = line.AsSpan(position);
        int index = 0;
        int start = 0;

        while (start < variants.Length) {
            int comma = variants.IndexOf(',', start);
            int length = comma >= 0
                ? comma - start
                : variants.Length - start;

            if (length == variant.Length &&
                variant.SequenceEqual(variants.AsSpan(start, length))) {
                position += length;
                return index;
            }

            start += length;
            if (start < variants.Length && variants[start] == ',')
                start++;
            index++;
        }

        return -1;
    }

    private bool parse_hex_out(
        string line,
        ref int position,
        ref int target,
        int maximum,
        string? prefix,
        string allowed) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + allowed);
                    return true;
                }

                int value = parse_hex_num(line, ref position, maximum);
                if (value < 0)
                    return false;
                target = value;
                return true;

            case '?':
                PutResponse(
                    (prefix ?? string.Empty) +
                    target.ToString("X2", CultureInfo.InvariantCulture));
                return true;

            default:
                return false;
        }
    }

    private bool parse_string_list_out(
        string line,
        ref int position,
        ref int target,
        int maximum,
        string? prefix,
        string variants) {
        _ = maximum;

        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse((prefix ?? string.Empty) + variants);
                    return true;
                }

                int value = match_element(line, ref position, variants);
                if (value < 0)
                    return false;
                target = value;
                return true;

            case '?': {
                    int selected = target;
                    int start = 0;
                    while (selected-- > 0) {
                        int comma = variants.IndexOf(',', start);
                        if (comma < 0) {
                            PutResponse(string.Empty);
                            return true;
                        }
                        start = comma + 1;
                    }

                    int end = variants.IndexOf(',', start);
                    if (end < 0)
                        end = variants.Length;

                    PutResponse(
                        (prefix ?? string.Empty) +
                        variants[start..end]);
                    return true;
                }

            default:
                return false;
        }
    }

    private bool parse_string_out(
        string line,
        ref int position,
        ref string? target,
        string? prefix) {
        if (position >= line.Length)
            return false;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse(prefix ?? string.Empty);
                } else {
                    target = line[position..];
                }
                break;

            case '?':
                PutResponse(target ?? string.Empty);
                break;

            default:
                return false;
        }

        position = line.Length;
        return true;
    }

    private CommandResult s_reg_handler(
        string line,
        ref int position,
        int register) {
        if (position >= line.Length)
            return CommandResult.Failure;

        switch (line[position++]) {
            case '=':
                if (position < line.Length && line[position] == '?') {
                    position++;
                    PutResponse("000");
                    return CommandResult.Success;
                }

                int value = parse_num(line, ref position, 255);
                if (value < 0)
                    return CommandResult.Failure;
                Profile.SRegisters[register] = (byte)value;
                return CommandResult.Success;

            case '?':
                PutResponse(
                    Profile.SRegisters[register]
                        .ToString("000", CultureInfo.InvariantCulture));
                return CommandResult.Success;

            case '.':
                int bit = parse_num(line, ref position, 7);
                if (bit < 0 || position >= line.Length)
                    return CommandResult.Failure;

                switch (line[position++]) {
                    case '=':
                        if (position < line.Length && line[position] == '?') {
                            position++;
                            PutNumericResponse(0);
                            return CommandResult.Success;
                        }

                        int bitValue = parse_num(line, ref position, 1);
                        if (bitValue < 0)
                            return CommandResult.Failure;

                        if (bitValue != 0)
                            Profile.SRegisters[register] |= (byte)(1 << bit);
                        else
                            Profile.SRegisters[register] &= (byte)~(1 << bit);
                        return CommandResult.Success;

                    case '?':
                        PutNumericResponse(
                            (Profile.SRegisters[register] >> bit) & 1);
                        return CommandResult.Success;

                    default:
                        return CommandResult.Failure;
                }

            default:
                return CommandResult.Failure;
        }
    }

    private CommandResult process_class1_cmd(
        string line,
        ref int position) {
        if (position + 3 >= line.Length)
            return CommandResult.Failure;

        int direction = line[position + 2] == 'T' ? 1 : 0;
        int operation = line[position + 3];
        position += 4;

        string allowed = operation switch {
            'S' => "0-255",
            'H' => "3",
            _ => "24,48,72,73,74,96,97,98,121,122,145,146"
        };

        int value = -1;
        if (!parse_out(
                line,
                ref position,
                ref value,
                255,
                null,
                allowed)) {
            return CommandResult.Success;
        }

        if (value < 0)
            return CommandResult.Success;

        if (ReceiveMode == AtReceiveMode.OnHookCommand)
            return CommandResult.Failure;

        int result = 1;
        if (_class1Handler is not null) {
            result = _class1Handler(
                _class1UserData,
                direction,
                operation,
                value);
        }

        return result switch {
            0 => CommandResult.SuppressImmediateResponse,
            -1 => CommandResult.Failure,
            _ => CommandResult.Success
        };
    }

    private bool answer_call() {
        if (ModemControl(
                AtModemControlOperation.Answer,
                null) < 0) {
            return false;
        }

        DoHangup = false;
        return true;
    }

    private CommandResult at_cmd_A(
        string line,
        int argumentStart,
        ref int position) {
        _ = line;
        _ = argumentStart;
        _ = position;

        return answer_call()
            ? CommandResult.SuppressImmediateResponse
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_D(
        string line,
        int argumentStart,
        ref int position) {
        ResetCallInformation();
        DoHangup = false;
        SilentDial = false;
        CommandDial = false;

        StringBuilder number = new(101);
        position = argumentStart;

        while (position < line.Length) {
            char character = line[position++];
            if (char.IsAsciiDigit(character)) {
                number.Append(character);
                continue;
            }

            switch (character) {
                case 'A':
                case 'B':
                case 'C':
                case 'D':
                case '*':
                case '#':
                    if (!Profile.PulseDial)
                        number.Append(character);
                    break;

                case ' ':
                case '-':
                    break;

                case '+':
                case ',':
                    number.Append(character);
                    break;

                case 'T':
                    Profile.PulseDial = false;
                    break;

                case 'P':
                    Profile.PulseDial = true;
                    break;

                case '@':
                    SilentDial = true;
                    break;

                case ';':
                    CommandDial = true;
                    break;

                case '!':
                case 'W':
                case 'S':
                case 'G':
                case 'I':
                case '>':
                    break;

                default:
                    return CommandResult.Failure;
            }
        }

        if (ModemControl(
                AtModemControlOperation.Call,
                number.ToString()) < 0) {
            return CommandResult.Failure;
        }

        return CommandResult.SuppressImmediateResponse;
    }

    private CommandResult at_cmd_E(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;
        Profile.Echo = value != 0;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_H(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;

        if (value != 0) {
            if (ReceiveMode is not AtReceiveMode.OnHookCommand and
                not AtReceiveMode.OffHookCommand) {
                return CommandResult.Failure;
            }

            ModemControl(AtModemControlOperation.OffHook, null);
            SetReceiveMode(AtReceiveMode.OffHookCommand);
            return CommandResult.Success;
        }

        ResetCallInformation();
        if (ReceiveMode is not AtReceiveMode.OnHookCommand and
            not AtReceiveMode.OffHookCommand) {
            RestartFaxModem(AtFaxModemRestartMode.Flush);
            DoHangup = true;
            SetReceiveMode(AtReceiveMode.Connected);
            return CommandResult.SuppressImmediateResponse;
        }

        ModemControl(AtModemControlOperation.Hangup, null);
        SetReceiveMode(AtReceiveMode.OnHookCommand);
        return CommandResult.Success;
    }

    private CommandResult at_cmd_I(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 255);
        switch (value) {
            case 0:
                PutResponse(Model);
                return CommandResult.Success;
            case 3:
                PutResponse(Manufacturer);
                return CommandResult.Success;
            default:
                return CommandResult.Failure;
        }
    }

    private CommandResult at_cmd_L(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        SpeakerVolume = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_M(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        SpeakerMode = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_O(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;
        if (value == 0) {
            SetReceiveMode(AtReceiveMode.Connected);
            PutResponseCode(AtResponseCode.Connect);
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_P(
        string line,
        int argumentStart,
        ref int position) {
        _ = line;
        position = argumentStart;
        Profile.PulseDial = true;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_Q(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;

        switch (value) {
            case 0:
                Profile.ResultCodeFormat = Profile.Verbose
                    ? AtResultCodeFormat.Ascii
                    : AtResultCodeFormat.Numeric;
                break;
            case 1:
                Profile.ResultCodeFormat = AtResultCodeFormat.None;
                break;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_S0(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 0);
    }

    private CommandResult at_cmd_S10(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 10);
    }

    private CommandResult at_cmd_S3(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 3);
    }

    private CommandResult at_cmd_S4(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 4);
    }

    private CommandResult at_cmd_S5(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 5);
    }

    private CommandResult at_cmd_S6(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 6);
    }

    private CommandResult at_cmd_S7(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 7);
    }

    private CommandResult at_cmd_S8(string line, int argumentStart, ref int position) {
        position = argumentStart;
        return s_reg_handler(line, ref position, 8);
    }

    private CommandResult at_cmd_T(
        string line,
        int argumentStart,
        ref int position) {
        _ = line;
        position = argumentStart;
        Profile.PulseDial = false;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_V(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;

        Profile.Verbose = value != 0;
        if (Profile.ResultCodeFormat != AtResultCodeFormat.None) {
            Profile.ResultCodeFormat = Profile.Verbose
                ? AtResultCodeFormat.Ascii
                : AtResultCodeFormat.Numeric;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_X(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 4);
        if (value < 0)
            return CommandResult.Failure;
        ResultCodeMode = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_Z(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, Profiles.Length - 1);
        if (value < 0)
            return CommandResult.Failure;

        ModemControl(AtModemControlOperation.Hangup, null);
        SetReceiveMode(AtReceiveMode.OnHookCommand);
        Profile = Profiles[value].Clone();
        ResetCallInformation();
        return CommandResult.Success;
    }

    private CommandResult at_cmd_amp_C(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 1);
        if (value < 0)
            return CommandResult.Failure;
        RlsdBehaviour = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_amp_D(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = parse_num(line, ref position, 2);
        if (value < 0)
            return CommandResult.Failure;
        DtrBehaviour = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_amp_F(
        string line,
        int argumentStart,
        ref int position) {
        _ = line;
        position = argumentStart;
        ModemControl(AtModemControlOperation.Hangup, null);
        SetReceiveMode(AtReceiveMode.OnHookCommand);
        Profile = Profiles[0].Clone();
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8T(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        int value = V8BisSignal;
        if (!parse_out(line, ref position, ref value, 10, "+A8T:", "(0-10)"))
            return CommandResult.Failure;
        V8BisSignal = value;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;
        value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        V8BisFirstMessage = value;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;
        value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        V8BisSecondMessage = value;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;
        value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        V8BisSignalEnable = value;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;
        value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        V8BisMessageEnable = value;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;
        value = parse_num(line, ref position, 255);
        if (value < 0)
            return CommandResult.Failure;
        V8BisSupplementaryDelay = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_EWIND(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int receiveWindow = ReceiveWindow;
        int transmitWindow = TransmitWindow;
        if (!parse_2_out(line, ref position, ref receiveWindow, 127, ref transmitWindow, 127, "+EWIND:", "(1-127),(1-127)"))
            return CommandResult.Failure;
        ReceiveWindow = receiveWindow;
        TransmitWindow = transmitWindow;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FAR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = Profile.AdaptiveReceive;
        if (!parse_out(line, ref position, ref value, 1, null, "0,1"))
            return CommandResult.Failure;
        Profile.AdaptiveReceive = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCL(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = CarrierLossTimeout;
        if (!parse_out(line, ref position, ref value, 255, null, "(0-255)"))
            return CommandResult.Failure;
        CarrierLossTimeout = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCLASS(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = FaxClassMode;
        if (!parse_string_list_out(line, ref position, ref value, 1, null, "0,1,1.0"))
            return CommandResult.Failure;
        FaxClassMode = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FDD(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = Profile.DoubleEscape;
        if (!parse_out(line, ref position, ref value, 1, null, "(0,1)"))
            return CommandResult.Failure;
        Profile.DoubleEscape = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FIT(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int timeout = DteInactivityTimeout;
        int action = DteInactivityAction;
        if (!parse_2_out(line, ref position, ref timeout, 255, ref action, 1, "+FIT:", "(0-255),(0-1)"))
            return CommandResult.Failure;
        DteInactivityTimeout = timeout;
        DteInactivityAction = action;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FLO(string line, int argumentStart, ref int position) {
        position = argumentStart;
        Logging.Flow("+FLO received");
        int value = DteToDceFlowControl;
        if (!parse_out(line, ref position, ref value, 2, "+FLO:", "(0-2)"))
            return CommandResult.Failure;
        DteToDceFlowControl = value;
        DceToDteFlowControl = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FMI(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Manufacturer);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FMM(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Model);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FMR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Revision);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = DteRate;
        if (!parse_out(line, ref position, ref value, 115200, null, "115200"))
            return CommandResult.Failure;
        DteRate = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FRH(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_FRM(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_FRS(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_FTH(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_FTM(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_FTS(string line, int argumentStart, ref int position) {
        _ = argumentStart;
        return process_class1_cmd(line, ref position);
    }

    private CommandResult at_cmd_plus_GCAP(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse("+GCAP:+FCLASS");
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GCI(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = CountryOfInstallation;
        if (!parse_hex_out(line, ref position, ref value, 255, "+GCI:", "(00-FF)"))
            return CommandResult.Failure;
        CountryOfInstallation = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GMI(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Manufacturer);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GMM(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Model);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GMR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(Revision);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GOI(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(GlobalObjectIdentity);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_GSN(string line, int argumentStart, ref int position) {
        position = argumentStart;
        if (position < line.Length && line[position] == '?') {
            PutResponse(SerialNumber);
            position++;
        }
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_ICF(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int characterFormat = DteCharacterFormat;
        int parity = DteParity;
        if (!parse_2_out(line, ref position, ref characterFormat, 6, ref parity, 3, "+ICF:", "(0-6),(0-3)"))
            return CommandResult.Failure;
        DteCharacterFormat = characterFormat;
        DteParity = parity;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_ICLOK(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = SynchronousTransmitClockSource;
        if (!parse_out(line, ref position, ref value, 2, "+ICLOK:", "(0-2)"))
            return CommandResult.Failure;
        SynchronousTransmitClockSource = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_IDSR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = DsrOption;
        if (!parse_out(line, ref position, ref value, 2, "+IDSR:", "(0-2)"))
            return CommandResult.Failure;
        DsrOption = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_IFC(string line, int argumentStart, ref int position) {
        Logging.Flow("+IFC received");
        position = argumentStart;
        int dteToDce = DteToDceFlowControl;
        int dceToDte = DceToDteFlowControl;
        if (!parse_2_out(line, ref position, ref dteToDce, 2, ref dceToDte, 2, "+IFC:", "(0-2),(0-2)"))
            return CommandResult.Failure;
        DteToDceFlowControl = dteToDce;
        DceToDteFlowControl = dceToDte;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_ILSD(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = LongSpaceDisconnectOption;
        if (!parse_out(line, ref position, ref value, 2, "+ILSD:", "(0,1)"))
            return CommandResult.Failure;
        LongSpaceDisconnectOption = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_IPR(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = DteRate;
        if (!parse_out(line, ref position, ref value, 115200, "+IPR:", "(115200),(115200)"))
            return CommandResult.Failure;
        DteRate = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VCID(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = DisplayCallInformation;
        if (!parse_out(line, ref position, ref value, 1, null, "0,1"))
            return CommandResult.Failure;
        DisplayCallInformation = value;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VRID(string line, int argumentStart, ref int position) {
        position = argumentStart;
        int value = 0;
        if (!parse_out(line, ref position, ref value, 1, null, "0,1"))
            return CommandResult.Failure;
        if (value == 1)
            DisplayStoredCallInformation();
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VSID(string line, int argumentStart, ref int position) {
        position = argumentStart;
        string? value = LocalId;
        if (!parse_string_out(line, ref position, ref value, null))
            return CommandResult.Failure;
        LocalId = value;
        return ModemControl(AtModemControlOperation.SetId, LocalId) < 0
            ? CommandResult.Failure
            : CommandResult.Success;
    }

    private CommandResult at_cmd_dummy(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8A(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8C(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8E(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        if (!parse_out(line, ref position, 6, "+A8E:", "(0-6),(0-5),(00-FF)"))
            return CommandResult.Failure;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;

        // Preserve the supplied FX source exactly: it does not advance
        // past the comma before parsing the second value.
        if (parse_num(line, ref position, 5) < 0)
            return CommandResult.Failure;

        if (position >= line.Length || line[position] != ',')
            return CommandResult.Success;

        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8I(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8J(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8M(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_A8R(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_ASTO(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+ASTO:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAAP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_2_out(line, ref position, 65535, 65535, "+CAAP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CACM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CACM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CACSP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CACSP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CAEMLPP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAEMLPP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAHLD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAHLD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAJOIN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAJOIN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CALA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CALA:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CALCC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CALCC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CALD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CALD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CALM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CALM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAMM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAMM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CANCHEV(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CANCHEV:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAOC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAOC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAPD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAPD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAPTT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAPTT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAREJ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAREJ:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CAULEV(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CAULEV:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CBC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CBC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CBCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CBCS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CBIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CBST(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CBST:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CCFC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CCFC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CCLK(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CCLK:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CCUG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CCUG:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CCWA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CCWA:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CCWE(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CCWE:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CDIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CDIP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CDIS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CDIS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CDV(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CEER(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CEER:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CESP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CFCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CFCS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CFG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CFUN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CFUN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGACT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGACT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGANS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGANS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGATT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGATT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGAUTO(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGAUTO:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGCAP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CGCLASS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGCLASS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGCLOSP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGCLOSP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGCLPAD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGCLPAD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGCMOD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGCMOD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGCS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGDATA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGDATA:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGDCONT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGDCONT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGDSCONT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGDSCONT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGEQMIN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGEQMIN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGEQNEG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGEQNEG:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGEQREQ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGEQREQ:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGEREP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGEREP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGMI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGMI:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGMM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGMM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGMR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGMR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGOI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CGPADDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGPADDR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGQMIN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGQMIN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGQREQ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGQREQ:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGREG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGREG:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGSMS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGSMS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGSN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGSN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CGTFT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CGTFT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHLD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHLD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSA:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHST(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHST:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHSU(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHSU:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHUP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CHUP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CHV(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CIMI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CIMI:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CIND(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CIND:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CIT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CKPD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CKPD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLAC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLAC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLAE(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLAE:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLAN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLAN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLCC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLCC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLCK(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLCK:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLIP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLIR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLIR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CLVL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CLVL:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMAR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMAR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMEC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMEC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMEE(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMER(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMER:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMGC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMGW(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMMS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMOD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMOD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMSS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CMUT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMUT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CMUX(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CMUX:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CNMA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CNMI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CNUM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CNUM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_COLP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+COLP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_COPN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+COPN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_COPS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+COPS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_COS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_COTDI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+COTDI:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPAS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPAS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPBF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPBF:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPBR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPBR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPBS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPBS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPBW(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPBW:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPIN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPIN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPLS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPLS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPMS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CPOL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPOL:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPPS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPPS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPROT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPROT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPUC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPUC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPWC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPWC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CPWD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CPWD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CQD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CREG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CREG:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRES(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRLP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRLP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRLP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CRMC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRMC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRMP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRMP:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRSL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRSL:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CRSM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CRSM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSAS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSCA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSCB(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSCC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSCC:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSCS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSDF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSDF:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSDH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSGT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSGT:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSIL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSIL:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSIM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSIM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSMP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSMS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSNS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSNS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSQ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSQ:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CSSN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSSN:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSTA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSTA:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSTF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSTF:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CSVM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CSVM:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CTA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CTF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_CTFR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CTFR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CTZR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CTZR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CTZU(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CTZU:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CUSD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CUSD:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CUUS1(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CUUS1:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CV120(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CV120:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CVHU(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CVHU:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CVIB(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+CVIB:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_CXT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_DR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+DR:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_DS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+DS:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_DS44(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_EB(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+EB:", "")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_EFCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 2, "+EFCS:", "(0-2)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_EFRAM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_2_out(line, ref position, 65535, 65535, "+EFRAM:", "(1-65535),(1-65535)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ER(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+ER:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ES(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        ReadOnlySpan<int> maximums = [7, 4, 9];
        return parse_n_out(line, ref position, maximums, 3, "+ES:", "(0-7),(0-4),(0-9)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ESA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        ReadOnlySpan<int> maximums = [2, 1, 1, 1, 2, 1, 255, 255];
        return parse_n_out(line, ref position, maximums, 8, "+ESA:", "(0-2),(0-1),(0-1),(0-1),(0-2),(0-1),(0-255),(0-255)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ESR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_ETBM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_2_out(line, ref position, 2, 2, "+ETBM:", "(0-2),(0-2),(0-30)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_F34(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        ReadOnlySpan<int> maximums = [14, 14, 2, 14, 14];
        return parse_n_out(line, ref position, maximums, 5, "+F34:", "(0-14),(0-14),(0-2),(0-14),(0-14)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_FAA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FAP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FBO(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FBS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FBU(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCQ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FCT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FDT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FEA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FFC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FFD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FHS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FIE(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FIS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FKS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FLI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FLP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FMS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FND(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FNR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FNS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FPW(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FRQ(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FRY(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FSA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_FSP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_IBC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        ReadOnlySpan<int> maximums = [2, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1];
        return parse_n_out(line, ref position, maximums, 13, "+IBC:", "(0-2),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0,1),(0.1),(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_IBM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        ReadOnlySpan<int> maximums = [7, 255, 255];
        return parse_n_out(line, ref position, maximums, 3, "+IBM:", "(0-7),(0-255),(0-255)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ILRR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_IRTS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+IRTS:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_ITF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_MA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_MR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+MR:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_MS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_MSC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+MSC:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_MV18AM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_MV18P(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 7, "+MV18P:", "(2-7)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_MV18R(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+MV18R:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_MV18S(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PCW(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PIG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PMH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PMHF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PMHR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PMHT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PQC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_PSS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SAC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SAM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SAR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SARR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SAT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SCRR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SDC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SDI(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SRSC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_STC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_STH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SVC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SVM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SVR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SVRR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_SVT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_TADR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_TAL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_2_out(line, ref position, 1, 1, "+TAL:", "(0,1),(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TALS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 3, "+TALS:", "(0-3)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TDLS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 3, "+TDLS:", "(0-4)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TE140(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TE140:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TE141(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TE141:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TEPAL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TEPAL:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TEPDL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TEPDL:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TERDL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TERDL:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TLDL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TLDL:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TMO(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_TMODE(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TMODE:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TNUM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_TRDL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TRDL:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TRDLS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_TRES(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TRES:", "(0-2)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TSELF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_out(line, ref position, 1, "+TSELF:", "(0,1)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_TTER(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return parse_2_out(line, ref position, 65535, 65535, "+TTER:", "(0-65535),(0-65535)")
            ? CommandResult.Success
            : CommandResult.Failure;
    }

    private CommandResult at_cmd_plus_VAC(
        string line,
        int argumentStart,
        ref int position) {
        // The FX indication handler returns the original pointer unchanged.
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VACR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VBT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VCIDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VDID(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VDIDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VDR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VDT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VDX(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VEM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VGM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VGR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VGS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VGT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VHC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VIP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VIT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VLS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VNH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VPH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VPP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VPR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VRA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VRL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VRN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VRX(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VSD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VSM(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VSP(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTER(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTS(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VTX(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_VXT(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_W(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WBAG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCDA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCHG(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCID(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCLK(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCPN(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WCXF(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WDAC(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WDIR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WECR(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WFON(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WKPD(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WPBA(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WPTH(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WRLK(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS45(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS46(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS50(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS51(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS52(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS53(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS54(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS57(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WS58(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }

    private CommandResult at_cmd_plus_WSTL(
        string line,
        int argumentStart,
        ref int position) {
        position = argumentStart;
        return CommandResult.Success;
    }
}
