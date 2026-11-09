/*
 * TKFaxEngine - managed C# port
 *
 * Logging.cs
 *
 * Combined port of:
 *   logging.h
 *   private/logging.h
 *   logging.c
 *
 * Error, protocol-flow and debug logging.
 *
 * Original implementation written by Steve Underwood <steveu@coppice.org>.
 * Copyright (C) 2005 Steve Underwood.
 *
 * This port preserves the LGPL-2.1 licensing terms of the original files.
 */

using System.Globalization;
using System.Text;

namespace TKFaxEngine;

/// <summary>
/// Native logging severities. The numeric values intentionally match
/// logging.h.
/// </summary>
public enum SpanLogSeverity {
    None = 0,
    Error = 1,
    Warning = 2,
    ProtocolError = 3,
    ProtocolWarning = 4,
    Flow = 5,
    Flow2 = 6,
    Flow3 = 7,
    Debug = 8,
    Debug2 = 9,
    Debug3 = 10
}

/// <summary>
/// Prefix and formatting flags from logging.h.
/// </summary>
[Flags]
public enum SpanLogOptions {
    None = 0,
    ShowDate = 0x0100,
    ShowSampleTime = 0x0200,
    ShowSeverity = 0x0400,
    ShowProtocol = 0x0800,
    ShowVariant = 0x1000,
    ShowTag = 0x2000,
    SuppressLabelling = 0x8000
}

/// <summary>
/// Callback corresponding to <c>message_handler_func_t</c>.
/// </summary>
public delegate void SpanMessageHandler(
    object? userData,
    int level,
    string text);

/// <summary>
/// Managed equivalent of <c>logging_state_t</c>.
/// </summary>
public sealed class SpanLogState : IDisposable {
    public const int SeverityMask = 0x00FF;
    public const int MaximumMessageCharacters = 1024;
    public const int DefaultSampleRate = 8000;

    private static readonly string[] Severities =
    [
        "NONE",
        "ERROR",
        "WARNING",
        "PROTOCOL_ERROR",
        "PROTOCOL_WARNING",
        "FLOW",
        "FLOW 2",
        "FLOW 3",
        "DEBUG 1",
        "DEBUG 2",
        "DEBUG 3"
    ];

    private static readonly object GlobalHandlerSync = new();

    private static SpanMessageHandler? _globalMessageHandler =
        DefaultMessageHandler;

    private static object? _globalUserData;

    private SpanMessageHandler? _messageHandler;
    private object? _userData;
    private bool _disposed;

    public SpanLogState(
        int level = 0,
        string? tag = null) {
        Initialize(level, tag);
    }

    internal SpanLogState() {
    }

    /// <summary>
    /// Severity threshold in the low byte plus display flags in the upper
    /// bits.
    /// </summary>
    public int Level { get; private set; }

    public int SamplesPerSecond { get; private set; }

    public long ElapsedSamples { get; private set; }

    public string? Tag { get; private set; }

    public string? Protocol { get; private set; }

    public bool IsDisposed => _disposed;

    public void Initialize(
        int level,
        string? tag) {
        SpanMessageHandler? globalHandler;
        object? globalUserData;

        lock (GlobalHandlerSync) {
            globalHandler = _globalMessageHandler;
            globalUserData = _globalUserData;
        }

        _messageHandler = globalHandler;
        _userData = globalUserData;

        Level = level;
        Tag = tag;
        Protocol = null;
        SamplesPerSecond = DefaultSampleRate;
        ElapsedSamples = 0;
        _disposed = false;
    }

    /// <summary>
    /// Tests whether a message severity is enabled.
    /// </summary>
    public bool Test(int level) {
        ThrowIfDisposed();

        return (Level & SeverityMask) >=
               (level & SeverityMask);
    }

    /// <summary>
    /// Generates a log entry using C printf-style formatting.
    /// </summary>
    public int Log(
        int level,
        string format,
        params object?[] arguments) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(format);

        if (!Test(level))
            return 0;

        StringBuilder message =
            new(MaximumMessageCharacters);

        if ((level & (int)SpanLogOptions.SuppressLabelling) == 0)
            AppendLabels(message, level);

        string body =
            CPrintfFormatter.Format(
                format,
                arguments);

        AppendTruncated(
            message,
            body,
            MaximumMessageCharacters);

        string completed =
            message.ToString();

        SpanMessageHandler? handler =
            _messageHandler;

        object? userData =
            _userData;

        if (handler is null) {
            lock (GlobalHandlerSync) {
                handler = _globalMessageHandler;
                userData = _globalUserData;
            }
        }

        handler?.Invoke(
            userData,
            level,
            completed);

        return 1;
    }

    /// <summary>
    /// Generates a log entry from already formatted text.
    /// </summary>
    public int LogText(
        int level,
        string text) {
        ArgumentNullException.ThrowIfNull(text);

        return Log(
            level,
            "%s",
            text);
    }

    /// <summary>
    /// Displays a buffer as lower-case hexadecimal bytes.
    /// </summary>
    public int LogBuffer(
        int level,
        string? tag,
        ReadOnlySpan<byte> buffer,
        int length) {
        ThrowIfDisposed();

        if (length < 0 || length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        if (!Test(level))
            return 0;

        StringBuilder body =
            new(Math.Min(1024, 32 + length * 3));

        if (tag is not null)
            body.Append(tag);

        for (int index = 0;
             index < length && body.Length < 800;
             index++) {
            body.Append(' ');
            body.Append(
                buffer[index].ToString(
                    "x2",
                    CultureInfo.InvariantCulture));
        }

        body.Append('\n');

        return Log(
            level,
            "%s",
            body.ToString());
    }

    public int GetLevel() {
        ThrowIfDisposed();
        return Level;
    }

    public int SetLevel(int level) {
        ThrowIfDisposed();
        Level = level;
        return 0;
    }

    public string? GetTag() {
        ThrowIfDisposed();
        return Tag;
    }

    public int SetTag(string? tag) {
        ThrowIfDisposed();
        Tag = tag;
        return 0;
    }

    public string? GetProtocol() {
        ThrowIfDisposed();
        return Protocol;
    }

    public int SetProtocol(string? protocol) {
        ThrowIfDisposed();
        Protocol = protocol;
        return 0;
    }

    public int SetSampleRate(int samplesPerSecond) {
        ThrowIfDisposed();
        SamplesPerSecond = samplesPerSecond;
        return 0;
    }

    public int BumpSamples(int samples) {
        ThrowIfDisposed();

        ElapsedSamples =
            unchecked(
                ElapsedSamples +
                samples);

        return 0;
    }

    /// <summary>
    /// Advances sample time using the exact native conversion of
    /// eight samples per millisecond.
    /// </summary>
    public int BumpTime(int milliseconds) {
        ThrowIfDisposed();

        ElapsedSamples =
            unchecked(
                ElapsedSamples +
                8L * milliseconds);

        return 0;
    }

    public void SetMessageHandler(
        SpanMessageHandler? handler,
        object? userData) {
        ThrowIfDisposed();
        _messageHandler = handler;
        _userData = userData;
    }

    public static void SetGlobalMessageHandler(
        SpanMessageHandler? handler,
        object? userData) {
        lock (GlobalHandlerSync) {
            _globalMessageHandler = handler;
            _globalUserData = userData;
        }
    }

    /// <summary>
    /// Matches <c>span_log_release()</c>. The native implementation has no
    /// release work.
    /// </summary>
    public int Release() {
        return 0;
    }

    public void Dispose() {
        if (_disposed)
            return;

        _messageHandler = null;
        _userData = null;
        Level = 0;
        SamplesPerSecond = 0;
        ElapsedSamples = 0;
        Tag = null;
        Protocol = null;
        _disposed = true;
    }

    private void AppendLabels(
        StringBuilder destination,
        int messageLevel) {
        SpanLogOptions options =
            (SpanLogOptions)Level;

        if ((options & SpanLogOptions.ShowDate) != 0) {
            DateTimeOffset now =
                DateTimeOffset.UtcNow;

            destination.Append(
                now.ToString(
                    "yyyy/MM/dd HH:mm:ss.fff ",
                    CultureInfo.InvariantCulture));
        }

        if ((options & SpanLogOptions.ShowSampleTime) != 0)
            AppendSampleTime(destination);

        int severity =
            messageLevel &
            SeverityMask;

        if ((options & SpanLogOptions.ShowSeverity) != 0 &&
            (uint)severity < (uint)Severities.Length) {
            destination.Append(Severities[severity]);
            destination.Append(' ');
        }

        if ((options & SpanLogOptions.ShowProtocol) != 0 &&
            Protocol is not null) {
            destination.Append(Protocol);
            destination.Append(' ');
        }

        if ((options & SpanLogOptions.ShowTag) != 0 &&
            Tag is not null) {
            destination.Append(Tag);
            destination.Append(' ');
        }

        // SHOW_VARIANT is defined by the native API but logging_state_t has
        // no variant field and logging.c never emits one.
    }

    private void AppendSampleTime(
        StringBuilder destination) {
        if (SamplesPerSecond <= 0) {
            destination.Append("00:00:00.000 ");
            return;
        }

        long wholeSeconds =
            ElapsedSamples /
            SamplesPerSecond;

        long remainingSamples =
            ElapsedSamples %
            SamplesPerSecond;

        if (remainingSamples < 0) {
            remainingSamples += SamplesPerSecond;
            wholeSeconds--;
        }

        long secondsInDay =
            wholeSeconds %
            86400;

        if (secondsInDay < 0)
            secondsInDay += 86400;

        long hours =
            secondsInDay /
            3600;

        long minutes =
            (secondsInDay / 60) %
            60;

        long seconds =
            secondsInDay %
            60;

        long milliseconds =
            remainingSamples *
            1000L /
            SamplesPerSecond;

        destination.Append(
            hours.ToString(
                "00",
                CultureInfo.InvariantCulture));

        destination.Append(':');

        destination.Append(
            minutes.ToString(
                "00",
                CultureInfo.InvariantCulture));

        destination.Append(':');

        destination.Append(
            seconds.ToString(
                "00",
                CultureInfo.InvariantCulture));

        destination.Append('.');

        destination.Append(
            milliseconds.ToString(
                "000",
                CultureInfo.InvariantCulture));

        destination.Append(' ');
    }

    private static void AppendTruncated(
        StringBuilder destination,
        string text,
        int maximumLength) {
        int remaining =
            maximumLength -
            destination.Length;

        if (remaining <= 0)
            return;

        if (text.Length <= remaining)
            destination.Append(text);
        else
            destination.Append(text.AsSpan(0, remaining));
    }

    private static void DefaultMessageHandler(
        object? userData,
        int level,
        string text) {
        _ = userData;
        _ = level;
        Console.Error.Write(text);
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(
                nameof(SpanLogState));
        }
    }
}

/// <summary>
/// Small C printf-style formatter for the format strings used throughout the
/// native TKFaxEngine sources.
/// </summary>
internal static class CPrintfFormatter {
    public static string Format(string format, ReadOnlySpan<object?> arguments) {
        StringBuilder result =
            new(format.Length + arguments.Length * 8);

        int argumentIndex = 0;

        for (int index = 0;
             index < format.Length;
             index++) {
            char current =
                format[index];

            if (current != '%') {
                result.Append(current);
                continue;
            }

            if (index + 1 < format.Length &&
                format[index + 1] == '%') {
                result.Append('%');
                index++;
                continue;
            }

            int tokenStart = index;
            index++;

            bool leftAlign = false;
            bool showSign = false;
            bool spaceSign = false;
            bool alternate = false;
            bool zeroPad = false;

            while (index < format.Length) {
                switch (format[index]) {
                    case '-':
                        leftAlign = true;
                        index++;
                        continue;

                    case '+':
                        showSign = true;
                        index++;
                        continue;

                    case ' ':
                        spaceSign = true;
                        index++;
                        continue;

                    case '#':
                        alternate = true;
                        index++;
                        continue;

                    case '0':
                        zeroPad = true;
                        index++;
                        continue;
                }

                break;
            }

            int width = 0;

            while (index < format.Length &&
                   char.IsAsciiDigit(format[index])) {
                width =
                    checked(
                        width * 10 +
                        format[index] -
                        '0');

                index++;
            }

            int precision = -1;

            if (index < format.Length &&
                format[index] == '.') {
                index++;
                precision = 0;

                while (index < format.Length &&
                       char.IsAsciiDigit(format[index])) {
                    precision =
                        checked(
                            precision * 10 +
                            format[index] -
                            '0');

                    index++;
                }
            }

            // Ignore C length modifiers. Managed conversion below handles the
            // actual runtime argument type.
            while (index < format.Length &&
                   format[index] is 'h' or 'l' or 'j' or 'z' or 't' or 'L') {
                char modifier = format[index++];

                if (index < format.Length &&
                    format[index] == modifier &&
                    modifier is 'h' or 'l') {
                    index++;
                }
            }

            if (index >= format.Length) {
                result.Append(
                    format.AsSpan(tokenStart));
                break;
            }

            char conversion =
                format[index];

            if (argumentIndex >= arguments.Length) {
                result.Append(
                    format.AsSpan(
                        tokenStart,
                        index - tokenStart + 1));

                continue;
            }

            object? argument =
                arguments[argumentIndex++];

            string formatted =
                FormatArgument(
                    argument,
                    conversion,
                    precision,
                    alternate,
                    showSign,
                    spaceSign);

            AppendPadded(
                result,
                formatted,
                width,
                leftAlign,
                zeroPad);
        }

        return result.ToString();
    }

    private static string FormatArgument(
        object? argument,
        char conversion,
        int precision,
        bool alternate,
        bool showSign,
        bool spaceSign) {
        switch (conversion) {
            case 's': {
                    string text =
                        argument?.ToString() ??
                        "(null)";

                    return precision >= 0 &&
                           text.Length > precision
                        ? text[..precision]
                        : text;
                }

            case 'c': {
                    if (argument is char character)
                        return character.ToString();

                    long value =
                        Convert.ToInt64(
                            argument,
                            CultureInfo.InvariantCulture);

                    return unchecked((char)value).ToString();
                }

            case 'd':
            case 'i': {
                    long value =
                        Convert.ToInt64(
                            argument,
                            CultureInfo.InvariantCulture);

                    string text =
                        value.ToString(
                            CultureInfo.InvariantCulture);

                    return AddPositiveSign(
                        text,
                        value >= 0,
                        showSign,
                        spaceSign);
                }

            case 'u': {
                    ulong value =
                        ConvertToUInt64(argument);

                    return value.ToString(
                        CultureInfo.InvariantCulture);
                }

            case 'x':
            case 'X': {
                    ulong value =
                        ConvertToUInt64(argument);

                    string digits =
                        value.ToString(
                            conversion == 'x'
                                ? "x"
                                : "X",
                            CultureInfo.InvariantCulture);

                    if (precision > 0)
                        digits = digits.PadLeft(precision, '0');

                    if (alternate && value != 0) {
                        digits =
                            (conversion == 'x'
                                ? "0x"
                                : "0X") +
                            digits;
                    }

                    return digits;
                }

            case 'o': {
                    ulong value =
                        ConvertToUInt64(argument);

                    string digits =
                        Convert.ToString(
                            unchecked((long)value),
                            8);

                    if (precision > 0)
                        digits = digits.PadLeft(precision, '0');

                    if (alternate &&
                        (digits.Length == 0 ||
                         digits[0] != '0')) {
                        digits = "0" + digits;
                    }

                    return digits;
                }

            case 'f':
            case 'F': {
                    double value =
                        Convert.ToDouble(
                            argument,
                            CultureInfo.InvariantCulture);

                    int digits =
                        precision >= 0
                            ? precision
                            : 6;

                    string text =
                        value.ToString(
                            "F" + digits,
                            CultureInfo.InvariantCulture);

                    return AddPositiveSign(
                        text,
                        value >= 0.0,
                        showSign,
                        spaceSign);
                }

            case 'e':
            case 'E': {
                    double value =
                        Convert.ToDouble(
                            argument,
                            CultureInfo.InvariantCulture);

                    int digits =
                        precision >= 0
                            ? precision
                            : 6;

                    string text =
                        value.ToString(
                            (conversion == 'e'
                                ? "e"
                                : "E") +
                            digits,
                            CultureInfo.InvariantCulture);

                    return AddPositiveSign(
                        text,
                        value >= 0.0,
                        showSign,
                        spaceSign);
                }

            case 'g':
            case 'G': {
                    double value =
                        Convert.ToDouble(
                            argument,
                            CultureInfo.InvariantCulture);

                    int digits =
                        precision > 0
                            ? precision
                            : 6;

                    string text =
                        value.ToString(
                            (conversion == 'g'
                                ? "g"
                                : "G") +
                            digits,
                            CultureInfo.InvariantCulture);

                    return AddPositiveSign(
                        text,
                        value >= 0.0,
                        showSign,
                        spaceSign);
                }

            case 'p': {
                    ulong value =
                        argument switch {
                            IntPtr pointer =>
                                unchecked((ulong)pointer.ToInt64()),

                            UIntPtr pointer =>
                                pointer.ToUInt64(),

                            _ => ConvertToUInt64(argument)
                        };

                    return "0x" +
                           value.ToString(
                               "x",
                               CultureInfo.InvariantCulture);
                }

            default:
                return argument?.ToString() ?? string.Empty;
        }
    }

    private static ulong ConvertToUInt64(
        object? argument) {
        return argument switch {
            sbyte value => unchecked((ulong)value),
            short value => unchecked((ulong)value),
            int value => unchecked((ulong)value),
            long value => unchecked((ulong)value),
            byte value => value,
            ushort value => value,
            uint value => value,
            ulong value => value,
            IntPtr value => unchecked((ulong)value.ToInt64()),
            UIntPtr value => value.ToUInt64(),
            _ => Convert.ToUInt64(
                argument,
                CultureInfo.InvariantCulture)
        };
    }

    private static string AddPositiveSign(
        string text,
        bool nonNegative,
        bool showSign,
        bool spaceSign) {
        if (!nonNegative)
            return text;

        if (showSign)
            return "+" + text;

        if (spaceSign)
            return " " + text;

        return text;
    }

    private static void AppendPadded(
        StringBuilder destination,
        string text,
        int width,
        bool leftAlign,
        bool zeroPad) {
        int padding =
            width -
            text.Length;

        if (padding <= 0) {
            destination.Append(text);
            return;
        }

        if (leftAlign) {
            destination.Append(text);
            destination.Append(' ', padding);
            return;
        }

        char fill =
            zeroPad
                ? '0'
                : ' ';

        if (fill == '0' &&
            text.Length > 0 &&
            text[0] is '+' or '-' or ' ') {
            destination.Append(text[0]);
            destination.Append('0', padding);
            destination.Append(text.AsSpan(1));
            return;
        }

        if (fill == '0' &&
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            destination.Append(text.AsSpan(0, 2));
            destination.Append('0', padding);
            destination.Append(text.AsSpan(2));
            return;
        }

        destination.Append(fill, padding);
        destination.Append(text);
    }
}

/// <summary>
/// Compatibility facade retaining the original C constants and function
/// names.
/// </summary>
public static class LoggingApi {
    public const int SPAN_LOG_SEVERITY_MASK =
        SpanLogState.SeverityMask;

    public const int SPAN_LOG_SHOW_DATE =
        (int)SpanLogOptions.ShowDate;

    public const int SPAN_LOG_SHOW_SAMPLE_TIME =
        (int)SpanLogOptions.ShowSampleTime;

    public const int SPAN_LOG_SHOW_SEVERITY =
        (int)SpanLogOptions.ShowSeverity;

    public const int SPAN_LOG_SHOW_PROTOCOL =
        (int)SpanLogOptions.ShowProtocol;

    public const int SPAN_LOG_SHOW_VARIANT =
        (int)SpanLogOptions.ShowVariant;

    public const int SPAN_LOG_SHOW_TAG =
        (int)SpanLogOptions.ShowTag;

    public const int SPAN_LOG_SUPPRESS_LABELLING =
        (int)SpanLogOptions.SuppressLabelling;

    public const int SPAN_LOG_NONE =
        (int)SpanLogSeverity.None;

    public const int SPAN_LOG_ERROR =
        (int)SpanLogSeverity.Error;

    public const int SPAN_LOG_WARNING =
        (int)SpanLogSeverity.Warning;

    public const int SPAN_LOG_PROTOCOL_ERROR =
        (int)SpanLogSeverity.ProtocolError;

    public const int SPAN_LOG_PROTOCOL_WARNING =
        (int)SpanLogSeverity.ProtocolWarning;

    public const int SPAN_LOG_FLOW =
        (int)SpanLogSeverity.Flow;

    public const int SPAN_LOG_FLOW_2 =
        (int)SpanLogSeverity.Flow2;

    public const int SPAN_LOG_FLOW_3 =
        (int)SpanLogSeverity.Flow3;

    public const int SPAN_LOG_DEBUG =
        (int)SpanLogSeverity.Debug;

    public const int SPAN_LOG_DEBUG_2 =
        (int)SpanLogSeverity.Debug2;

    public const int SPAN_LOG_DEBUG_3 =
        (int)SpanLogSeverity.Debug3;

    public static bool span_log_test(
        SpanLogState? state,
        int level) {
        return state is not null &&
               state.Test(level);
    }

    public static int span_log(
        SpanLogState state,
        int level,
        string format,
        params object?[] arguments) {
        ArgumentNullException.ThrowIfNull(state);

        return state.Log(
            level,
            format,
            arguments);
    }

    public static int span_log_buf(
        SpanLogState state,
        int level,
        string? tag,
        ReadOnlySpan<byte> buffer,
        int length) {
        ArgumentNullException.ThrowIfNull(state);

        return state.LogBuffer(
            level,
            tag,
            buffer,
            length);
    }

    public static int span_log_get_level(
        SpanLogState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetLevel();
    }

    public static int span_log_set_level(
        SpanLogState state,
        int level) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetLevel(level);
    }

    public static string? span_log_get_tag(
        SpanLogState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetTag();
    }

    public static int span_log_set_tag(
        SpanLogState state,
        string? tag) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetTag(tag);
    }

    public static string? span_log_get_protocol(
        SpanLogState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.GetProtocol();
    }

    public static int span_log_set_protocol(
        SpanLogState state,
        string? protocol) {
        ArgumentNullException.ThrowIfNull(state);
        return state.SetProtocol(protocol);
    }

    public static int span_log_set_sample_rate(
        SpanLogState state,
        int samplesPerSecond) {
        ArgumentNullException.ThrowIfNull(state);

        return state.SetSampleRate(
            samplesPerSecond);
    }

    public static int span_log_bump_samples(
        SpanLogState state,
        int samples) {
        ArgumentNullException.ThrowIfNull(state);
        return state.BumpSamples(samples);
    }

    public static int span_log_bump_time(
        SpanLogState state,
        int milliseconds) {
        ArgumentNullException.ThrowIfNull(state);
        return state.BumpTime(milliseconds);
    }

    public static void span_log_set_message_handler(
        SpanLogState state,
        SpanMessageHandler? handler,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);

        state.SetMessageHandler(
            handler,
            userData);
    }

    public static void span_set_message_handler(
        SpanMessageHandler? handler,
        object? userData) {
        SpanLogState.SetGlobalMessageHandler(
            handler,
            userData);
    }

    public static SpanLogState span_log_init(
        SpanLogState? state,
        int level,
        string? tag) {
        state ??=
            new SpanLogState();

        state.Initialize(
            level,
            tag);

        return state;
    }

    public static int span_log_release(
        SpanLogState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Release();
    }

    public static int span_log_free(
        SpanLogState? state) {
        if (state is null)
            return 0;

        int result =
            state.Release();

        state.Dispose();
        return result;
    }
}
