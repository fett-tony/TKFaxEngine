
/*
 * Although highly modified and altered, the code in this file was originally
 * derived from sources taken from (1) HylaFAX+ on 13 June 2022 which states
 * that its source was derived from (2) GitHub user, "mrwicks", on 9 Oct 2018.
 * That source, itself, was derived from work by "Amlendra" published at
 * Aticleworld on 21 May 2017 (3). That work, then, references programs (4)
 * Copyright (c) 2000 Sean Walton and Macmillan Publishers (The "Linux Socket
 * Programming" book) and are licensed under the GPL.
 *
 * 1. http://hylafax.sourceforge.net
 * 2. https://github.com/mrwicks/miscellaneous/tree/master/tls_1.2_example
 * 3. https://aticleworld.com/ssl-server-client-using-openssl-in-c/
 * 4. http://www.cs.utah.edu/~swalton/listings/sockets/programs/
 *
 * It is, therefore, presumed that this work is either under the public
 * domain or is licensed under the GPL.
 *
 * THIS SOFTWARE IS PROVIDED BY THE REGENTS AND CONTRIBUTORS ``AS IS'' AND
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE REGENTS OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
 * OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
 * HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
 * LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
 * OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF
 * SUCH DAMAGE.
 *
 * Managed C# port of ssl_fax.c and ssl_fax.h from TKFaxEngine.
 * OpenSSL and POSIX socket operations are implemented with TcpClient and
 * SslStream. The original spanDSP callbacks are represented by C# delegates.
 * Target framework: modern .NET (.NET 8 or newer).
 */

#nullable enable

using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TKFaxEngine;

/// <summary>
/// Status values used by the original spanDSP callback API.
/// </summary>
public static class SslFaxSignalStatus {
    public const int CarrierDown = -1;
}

public delegate void SpanPutMessageHandler(object? userData, byte[]? message, int length);
public delegate int SpanGetMessageHandler(object? userData, byte[] message, int maximumLength);
public delegate int SpanGetByteHandler(object? userData);
public delegate void HdlcFrameHandler(object? userData, byte[]? packet, int length, bool ok);
public delegate void HdlcUnderflowHandler(object? userData);

/// <summary>
/// Managed equivalent of <c>sslfax_state_t</c> plus the functions from
/// <c>ssl_fax.c</c>.
/// </summary>
public sealed class SslFaxState : IDisposable {
    private const byte Dle = 0x10;
    private const byte Etx = 0x03;
    private const int MaximumFrameLength = 265;
    private const int T30PhaseCEcmRx = 7;
    private static readonly TimeSpan ModemPollInterval = TimeSpan.FromMilliseconds(20);

    private readonly object _readSync = new();
    private readonly object _writeSync = new();
    private readonly object _transportSync = new();

    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private bool _disposed;

    public SslFaxState() {
        Logger = static message => Trace.WriteLine($"[SSL Fax] {message}");
        InitializeState();
    }

    /// <summary>
    /// Remote URL in the form passcode@host:port or passcode@[IPv6]:port.
    /// The optional ssl:// prefix is accepted and removed.
    /// </summary>
    public string? Url { get; set; }

    public int RcpCount { get; set; }
    public int EcmOnes { get; set; }
    public int EcmBitPosition { get; set; }
    public byte EcmByte { get; set; }
    public bool DoRead { get; set; }
    public int Signal { get; set; }
    public bool DoUnderflow { get; set; }
    public bool CleanupPending { get; set; }
    public bool TxUseHdlc { get; private set; }
    public bool RxUseHdlc { get; private set; }
    public object? UserData { get; private set; }

    public SpanGetByteHandler? GetPhase { get; private set; }
    public SpanGetMessageHandler? GetMessage { get; private set; }
    public SpanPutMessageHandler? PutMessage { get; private set; }
    public HdlcFrameHandler? HdlcAccept { get; private set; }
    public HdlcUnderflowHandler? HdlcTransmitUnderflow { get; private set; }

    /// <summary>
    /// Receives diagnostic messages without a trailing newline.
    /// </summary>
    public Action<string>? Logger { get; set; }

    /// <summary>
    /// Preserves the permissive certificate behavior of the original OpenSSL
    /// implementation. Set this to false or supply CertificateValidationCallback
    /// when certificate verification is required.
    /// </summary>
    public bool AllowUntrustedCertificates { get; set; } = true;

    public RemoteCertificateValidationCallback? CertificateValidationCallback { get; set; }

    /// <summary>
    /// TLS 1.2 matches the non-FLEXSSL branch of the original implementation.
    /// Set to SslProtocols.None to let the operating system choose enabled TLS versions.
    /// </summary>

    public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.None;
    //public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12;

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(2);
    public TimeSpan AuthenticationTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Optional replacement for the POSIX modem file descriptor monitored by select().
    /// Return true when modem data is waiting and SSL Fax should be interrupted.
    /// </summary>
    public Func<bool>? ModemDataAvailable { get; set; }

    public bool IsConnected {
        get {
            lock (_transportSync) {
                return _tcpClient?.Connected == true && _sslStream is not null;
            }
        }
    }

    /// <summary>
    /// Managed equivalent of sslfax_init().
    /// </summary>
    public SslFaxState Initialize() {
        ThrowIfDisposed();
        ForceCloseTransport();
        InitializeState();
        Log("Initialize");
        return this;
    }

    /// <summary>
    /// Managed equivalent of sslfax_setup().
    /// </summary>
    public void Setup(
        SpanPutMessageHandler? putMessage,
        SpanGetMessageHandler? getMessage,
        HdlcFrameHandler? hdlcAccept,
        HdlcUnderflowHandler? hdlcTransmitUnderflow,
        bool transmitUsesHdlc,
        bool receiveUsesHdlc,
        SpanGetByteHandler? getPhase,
        object? userData) {
        ThrowIfDisposed();

        PutMessage = putMessage;
        GetMessage = getMessage;
        HdlcAccept = hdlcAccept;
        HdlcTransmitUnderflow = hdlcTransmitUnderflow;
        TxUseHdlc = transmitUsesHdlc;
        RxUseHdlc = receiveUsesHdlc;
        GetPhase = getPhase;
        UserData = userData;
    }

    /// <summary>
    /// Managed equivalent of sslfax_start_client().
    /// </summary>
    public bool StartClient() {
        ThrowIfDisposed();

        string? configuredUrl = Url;
        if (string.IsNullOrWhiteSpace(configuredUrl)) {
            Log("Could not start SSL Fax client: URL is empty.");
            return false;
        }

        Log($"Starting SSL Fax client, URL: {configuredUrl}");

        if (!TryParseUrl(configuredUrl, out string passcode, out string host, out int port)) {
            Log($"Could not parse SSL Fax URL: \"{configuredUrl}\"");
            return false;
        }

        ForceCloseTransport();

        TcpClient? tcpClient = null;
        SslStream? sslStream = null;

        try {
            tcpClient = new TcpClient();
            tcpClient.NoDelay = true;

            tcpClient
                .ConnectAsync(host, port)
                .WaitAsync(ConnectTimeout)
                .GetAwaiter()
                .GetResult();

            sslStream = new SslStream(
                tcpClient.GetStream(),
                leaveInnerStreamOpen: false,
                ValidateRemoteCertificate);

            var authenticationOptions = new SslClientAuthenticationOptions {
                TargetHost = host,
                EnabledSslProtocols = EnabledSslProtocols,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };

            sslStream
                .AuthenticateAsClientAsync(authenticationOptions)
                .WaitAsync(AuthenticationTimeout)
                .GetAwaiter()
                .GetResult();

            lock (_transportSync) {
                _tcpClient = tcpClient;
                _sslStream = sslStream;
            }

            byte[] passcodeBytes = Encoding.ASCII.GetBytes(passcode);
            if (Write(passcodeBytes, passcodeBytes.Length, TimeSpan.FromSeconds(1), filterDle: false, sustain: false) <= 0) {
                Log("SSL Fax passcode write failed.");
                Cleanup(sustain: false);
                return false;
            }

            ShowCertificateInformation(sslStream);
            return true;
        } catch (TimeoutException) {
            Log($"Timeout connecting to SSL Fax receiver \"{host}\" at port {port}.");
        } catch (AuthenticationException exception) {
            Log($"TLS authentication with SSL Fax receiver failed: {exception.Message}");
        } catch (SocketException exception) {
            Log($"Unable to connect to SSL Fax receiver \"{host}\" at port {port}: {exception.Message}");
        } catch (IOException exception) {
            Log($"SSL Fax transport error while connecting: {exception.Message}");
        } catch (Exception exception) {
            Log($"Unable to start SSL Fax client: {exception.Message}");
        }

        sslStream?.Dispose();
        tcpClient?.Dispose();
        return false;
    }

    /// <summary>
    /// Managed equivalent of sslfax_tx(). The supplied audio buffer is filled
    /// with silence while data is transferred through TLS.
    /// </summary>
    public int Transmit(short[] samples, int length) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(samples);

        if (length < 0 || length > samples.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Array.Clear(samples, 0, length);

        if (!IsConnected || GetMessage is null || HdlcTransmitUnderflow is null) {
            return 0;
        }

        if (DoUnderflow) {
            DoUnderflow = false;
            HdlcTransmitUnderflow?.Invoke(UserData);
        }

        if (Signal > 0) {
            Signal--;

            if (Signal > 0 && TxUseHdlc) {
                DoUnderflow = true;
            }

            if (Signal == 0 && CleanupPending) {
                Cleanup(sustain: false);
            }

            return 0;
        }

        if (!TxUseHdlc) {
            var oneByte = new byte[1];
            bool sent = false;

            while (GetMessage is not null && GetMessage(UserData, oneByte, 1) == 1) {
                sent = true;

                if (Write(oneByte, 1, TimeSpan.FromSeconds(60), filterDle: true, sustain: false) <= 0) {
                    break;
                }
            }

            if (sent) {
                byte[] endOfData = { Dle, Etx };
                Write(endOfData, endOfData.Length, TimeSpan.FromSeconds(60), filterDle: false, sustain: false);
                Signal = 1;
                return 0;
            }
        }

        return length;
    }

    /// <summary>
    /// Managed equivalent of sslfax_rx(). Audio samples are intentionally ignored
    /// while the SSL Fax connection is active.
    /// </summary>
    public int Receive(short[] samples, int length) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(samples);

        if (length < 0 || length > samples.Length) {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        if (!IsConnected || PutMessage is null || HdlcAccept is null || GetPhase is null) {
            return 0;
        }

        return GetPhase(UserData) == T30PhaseCEcmRx
            ? ReceiveEcmFrames()
            : ReceiveNormalData();
    }

    /// <summary>
    /// Managed equivalent of sslfax_read().
    /// </summary>
    public int Read(
        byte[] buffer,
        int count,
        TimeSpan timeout,
        bool sustain,
        bool carryOn) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (count < 0 || count > buffer.Length) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) {
            return 0;
        }

        SslStream? stream = GetSslStream();
        if (stream is null) {
            return -2;
        }

        lock (_readSync) {
            using var cancellation = new CancellationTokenSource();
            Task<int> readTask;

            try {
                readTask = stream
                    .ReadAsync(buffer.AsMemory(0, count), cancellation.Token)
                    .AsTask();
            } catch (Exception exception) {
                Log($"Unable to begin SSL Fax read: {exception.Message}");
                Cleanup(sustain);
                return -2;
            }

            if (readTask.IsCompleted) {
                return CompleteRead(readTask, sustain);
            }

            if (timeout == TimeSpan.Zero) {
                cancellation.Cancel();
                ObserveFault(readTask);
                return 0;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            while (!readTask.IsCompleted) {
                if (HasModemData()) {
                    cancellation.Cancel();
                    ObserveFault(readTask);

                    if (!carryOn) {
                        Log("Modem has data while waiting for SSL Fax read. Terminating SSL Fax.");
                        Cleanup(sustain);
                    }

                    return -1;
                }

                if (timeout != Timeout.InfiniteTimeSpan && stopwatch.Elapsed >= timeout) {
                    cancellation.Cancel();
                    ObserveFault(readTask);
                    Log("Timeout waiting for SSL Fax read.");
                    Cleanup(sustain);
                    return 0;
                }

                TimeSpan delay = GetPollDelay(timeout, stopwatch.Elapsed);
                Task.WhenAny(readTask, Task.Delay(delay)).GetAwaiter().GetResult();
            }

            return CompleteRead(readTask, sustain);
        }
    }

    /// <summary>
    /// Convenience overload using milliseconds like the original C function.
    /// </summary>
    public int Read(byte[] buffer, int count, long milliseconds, bool sustain, bool carryOn) {
        TimeSpan timeout = milliseconds < 0
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromMilliseconds(milliseconds);

        return Read(buffer, count, timeout, sustain, carryOn);
    }

    /// <summary>
    /// Managed equivalent of sslfax_write(). When filterDle is true, every DLE
    /// byte is doubled, except when the caller sends the explicit DLE/ETX marker
    /// with filtering disabled.
    /// </summary>
    public int Write(
        byte[] buffer,
        int count,
        TimeSpan timeout,
        bool filterDle,
        bool sustain) {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(buffer);

        if (count < 0 || count > buffer.Length) {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        if (count == 0) {
            return 0;
        }

        SslStream? stream = GetSslStream();
        if (stream is null) {
            return -2;
        }

        byte[] payload = filterDle
            ? DuplicateDleBytes(buffer, count)
            : CopyPrefix(buffer, count);

        lock (_writeSync) {
            using var cancellation = new CancellationTokenSource();
            Task writeTask;

            try {
                writeTask = stream
                    .WriteAsync(payload.AsMemory(0, payload.Length), cancellation.Token)
                    .AsTask();
            } catch (Exception exception) {
                Log($"Unable to begin SSL Fax write: {exception.Message}");
                Cleanup(sustain);
                return -2;
            }

            if (!writeTask.IsCompleted) {
                Stopwatch stopwatch = Stopwatch.StartNew();

                while (!writeTask.IsCompleted) {
                    if (HasModemData()) {
                        cancellation.Cancel();
                        ObserveFault(writeTask);
                        Log("Modem has data while waiting for SSL Fax write. Terminating SSL Fax.");
                        Cleanup(sustain);
                        return -1;
                    }

                    if (timeout != Timeout.InfiniteTimeSpan && stopwatch.Elapsed >= timeout) {
                        cancellation.Cancel();
                        ObserveFault(writeTask);
                        Log("Timeout waiting for SSL Fax write.");
                        Cleanup(sustain);
                        return 0;
                    }

                    TimeSpan delay = GetPollDelay(timeout, stopwatch.Elapsed);
                    Task.WhenAny(writeTask, Task.Delay(delay)).GetAwaiter().GetResult();
                }
            }

            try {
                writeTask.GetAwaiter().GetResult();
                stream.Flush();
                return count;
            } catch (OperationCanceledException) {
                Log("SSL Fax write was cancelled.");
                Cleanup(sustain);
                return 0;
            } catch (Exception exception) when (exception is IOException or AuthenticationException or ObjectDisposedException) {
                Log($"Unable to write to SSL Fax connection: {exception.Message}");
                Cleanup(sustain);
                return -2;
            }
        }
    }

    /// <summary>
    /// Convenience overload using milliseconds like the original C function.
    /// </summary>
    public int Write(byte[] buffer, int count, long milliseconds, bool filterDle, bool sustain) {
        TimeSpan timeout = milliseconds < 0
            ? Timeout.InfiniteTimeSpan
            : TimeSpan.FromMilliseconds(milliseconds);

        return Write(buffer, count, timeout, filterDle, sustain);
    }

    /// <summary>
    /// Managed equivalent of sslfax_cleanup(). If a transmit signal is still
    /// pending, cleanup is deferred until Transmit() has delivered it.
    /// </summary>
    public void Cleanup(bool sustain) {
        if (_disposed) {
            return;
        }

        if (Signal > 0) {
            CleanupPending = true;
            return;
        }

        ResetProtocolState();
        Url = null;

        if (!sustain) {
            ForceCloseTransport();
        }

        CleanupPending = false;
    }

    public void Dispose() {
        if (_disposed) {
            return;
        }

        Signal = 0;
        ResetProtocolState();
        Url = null;
        ForceCloseTransport();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private int ReceiveEcmFrames() {
        var input = new byte[1];
        var frame = new byte[MaximumFrameLength];
        int position = 0;
        int bitPosition = 0;
        int ones = 0;
        bool skipFirstBit = false;

        while (true) {
            int read = Read(input, 1, position > 0 ? 3000L : 0L, sustain: false, carryOn: false);
            if (read < 1) {
                break;
            }

            if (input[0] == Dle) {
                read = Read(input, 1, 3000L, sustain: false, carryOn: false);
                if (read < 1) {
                    break;
                }

                if (input[0] == Etx) {
                    HdlcAccept?.Invoke(UserData, null, SslFaxSignalStatus.CarrierDown, true);
                    break;
                }
            }

            int startBit = skipFirstBit ? 1 : 0;
            skipFirstBit = false;

            for (int bitIndex = startBit; bitIndex < 8; bitIndex++) {
                int bit = (input[0] & (1 << bitIndex)) != 0 ? 1 : 0;

                if (bit == 1) {
                    ones++;
                }

                if (!(ones == 5 && bit == 0)) {
                    if (position >= frame.Length) {
                        Log("Invalid long ECM frame received via SSL Fax.");
                        return 0;
                    }

                    frame[position] |= (byte)(bit << bitPosition);
                    bitPosition++;

                    if (bitPosition == 8) {
                        position++;
                        bitPosition = 0;

                        if (position < frame.Length) {
                            frame[position] = 0;
                        }
                    }
                }

                if (bit == 0) {
                    ones = 0;
                }

                if (ones == 6) {
                    if (bitIndex == 7) {
                        skipFirstBit = true;
                    }

                    bitIndex++;

                    if (position > 2) {
                        byte[] packet = CopyPrefix(frame, position - 2);
                        HdlcAccept?.Invoke(UserData, packet, packet.Length, CrcItu16Check(frame, position));
                    }

                    ones = 0;
                    position = 0;
                    bitPosition = 0;
                    Array.Clear(frame, 0, frame.Length);
                }
            }
        }

        return 0;
    }

    private int ReceiveNormalData() {
        var input = new byte[1];
        var frame = new byte[MaximumFrameLength];
        int position = 0;

        while (true) {
            int read;

            do {
                read = Read(
                    input,
                    1,
                    position > 0 ? 3000L : 0L,
                    sustain: false,
                    carryOn: false);

                if (read > 0) {
                    frame[position] = input[0];
                }
            }
            while (read > 0 && RxUseHdlc && position == 0 && frame[position] == 0x00);

            if (read < 1) {
                break;
            }

            if (frame[position] == Dle) {
                read = Read(input, 1, 3000L, sustain: false, carryOn: false);
                if (read < 1) {
                    break;
                }

                frame[position] = input[0];

                if (frame[position] == Etx) {
                    if (!RxUseHdlc) {
                        PutMessage?.Invoke(UserData, null, SslFaxSignalStatus.CarrierDown);
                        return 0;
                    }

                    if (position == 0) {
                        // Usually zero fill following non-ECM phase C after RTC.
                        return 0;
                    }

                    if (position > 2) {
                        byte[] packet = CopyPrefix(frame, position - 2);
                        HdlcAccept?.Invoke(UserData, packet, packet.Length, CrcItu16Check(frame, position));
                    }

                    if (position > 1 && frame[1] != Etx) {
                        HdlcAccept?.Invoke(UserData, null, SslFaxSignalStatus.CarrierDown, true);
                        return 0;
                    }
                }
            }

            if (!RxUseHdlc) {
                PutMessage?.Invoke(UserData, new[] { frame[position] }, 1);
                position--;
            }

            position++;
            if (position >= frame.Length) {
                Log("Invalid long frame received via SSL Fax.");
                break;
            }
        }

        return 0;
    }

    private int CompleteRead(Task<int> readTask, bool sustain) {
        try {
            int read = readTask.GetAwaiter().GetResult();
            if (read > 0) {
                return read;
            }

            Log("SSL Fax peer closed the connection.");
            Cleanup(sustain);
            return -2;
        } catch (OperationCanceledException) {
            return 0;
        } catch (Exception exception) when (exception is IOException or AuthenticationException or ObjectDisposedException) {
            Log($"Unable to read from SSL Fax connection: {exception.Message}");
            Cleanup(sustain);
            return -2;
        }
    }

    private void InitializeState() {
        Url = null;
        RcpCount = 0;
        EcmOnes = 0;
        EcmBitPosition = 0;
        EcmByte = 0;
        DoRead = false;
        Signal = 0;
        DoUnderflow = false;
        CleanupPending = false;
        TxUseHdlc = false;
        RxUseHdlc = false;
        UserData = null;
        GetPhase = null;
        GetMessage = null;
        PutMessage = null;
        HdlcAccept = null;
        HdlcTransmitUnderflow = null;
    }

    private void ResetProtocolState() {
        RcpCount = 0;
        EcmOnes = 0;
        EcmBitPosition = 0;
        EcmByte = 0;
        DoRead = false;
        Signal = 0;
        DoUnderflow = false;
        TxUseHdlc = false;
        RxUseHdlc = false;
        UserData = null;
        GetPhase = null;
        GetMessage = null;
        PutMessage = null;
        HdlcAccept = null;
        HdlcTransmitUnderflow = null;
    }

    private SslStream? GetSslStream() {
        lock (_transportSync) {
            return _sslStream;
        }
    }

    private void ForceCloseTransport() {
        SslStream? sslStream;
        TcpClient? tcpClient;

        lock (_transportSync) {
            sslStream = _sslStream;
            tcpClient = _tcpClient;
            _sslStream = null;
            _tcpClient = null;
        }

        try {
            sslStream?.Dispose();
        } catch (Exception exception) {
            Log($"Error while closing SSL Fax TLS stream: {exception.Message}");
        }

        try {
            tcpClient?.Dispose();
        } catch (Exception exception) {
            Log($"Error while closing SSL Fax TCP connection: {exception.Message}");
        }
    }

    private bool ValidateRemoteCertificate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors) {
        if (CertificateValidationCallback is not null) {
            return CertificateValidationCallback(sender, certificate, chain, sslPolicyErrors);
        }

        return AllowUntrustedCertificates || sslPolicyErrors == SslPolicyErrors.None;
    }

    private void ShowCertificateInformation(SslStream sslStream) {
        try {
            string cipher = sslStream.NegotiatedCipherSuite.ToString();
            Log($"SSL Fax connection uses {sslStream.SslProtocol} with {cipher}.");

            X509Certificate? certificate = sslStream.RemoteCertificate;
            if (certificate is null) {
                Log("No remote certificate was supplied.");
                return;
            }

            using var certificate2 = new X509Certificate2(certificate);
            Log($"Remote certificate: Subject=\"{certificate2.Subject}\", Issuer=\"{certificate2.Issuer}\"");
        } catch (Exception exception) {
            Log($"Unable to inspect remote SSL Fax certificate: {exception.Message}");
        }
    }

    private bool HasModemData() {
        try {
            return ModemDataAvailable?.Invoke() == true;
        } catch (Exception exception) {
            Log($"Modem data callback failed: {exception.Message}");
            return true;
        }
    }

    private static TimeSpan GetPollDelay(TimeSpan timeout, TimeSpan elapsed) {
        if (timeout == Timeout.InfiniteTimeSpan) {
            return ModemPollInterval;
        }

        TimeSpan remaining = timeout - elapsed;
        if (remaining <= TimeSpan.Zero) {
            return TimeSpan.Zero;
        }

        return remaining < ModemPollInterval ? remaining : ModemPollInterval;
    }

    private static byte[] DuplicateDleBytes(byte[] source, int count) {
        int dleCount = 0;
        for (int index = 0; index < count; index++) {
            if (source[index] == Dle) {
                dleCount++;
            }
        }

        var result = new byte[count + dleCount];
        int destination = 0;

        for (int index = 0; index < count; index++) {
            byte value = source[index];
            result[destination++] = value;

            if (value == Dle) {
                result[destination++] = value;
            }
        }

        return result;
    }

    private static byte[] CopyPrefix(byte[] source, int count) {
        var result = new byte[count];
        Buffer.BlockCopy(source, 0, result, 0, count);
        return result;
    }

    private static bool CrcItu16Check(byte[] buffer, int length) {
        ushort crc = 0xFFFF;

        for (int index = 0; index < length; index++) {
            crc ^= buffer[index];

            for (int bit = 0; bit < 8; bit++) {
                crc = (ushort)((crc & 1) != 0
                    ? (crc >> 1) ^ 0x8408
                    : crc >> 1);
            }
        }

        return crc == 0xF0B8;
    }

    private static bool TryParseUrl(string url, out string passcode, out string host, out int port) {
        passcode = string.Empty;
        host = string.Empty;
        port = 0;

        string value = url.Trim();
        if (value.StartsWith("ssl://", StringComparison.OrdinalIgnoreCase)) {
            value = value[6..];
        }

        int at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1) {
            return false;
        }

        passcode = value[..at];
        string endpoint = value[(at + 1)..];
        string portText;

        if (endpoint.StartsWith("[", StringComparison.Ordinal)) {
            int closingBracket = endpoint.IndexOf(']');
            if (closingBracket <= 1 || closingBracket + 2 > endpoint.Length || endpoint[closingBracket + 1] != ':') {
                return false;
            }

            host = endpoint[1..closingBracket];
            portText = endpoint[(closingBracket + 2)..];
        } else {
            int colon = endpoint.LastIndexOf(':');
            if (colon <= 0 || colon == endpoint.Length - 1) {
                return false;
            }

            host = endpoint[..colon];
            portText = endpoint[(colon + 1)..];
        }

        return !string.IsNullOrWhiteSpace(host)
            && int.TryParse(portText, out port)
            && port is >= 1 and <= 65535;
    }

    private static void ObserveFault(Task task) {
        _ = task.ContinueWith(
            static completedTask => _ = completedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Log(string message) {
        Logger?.Invoke(message);
    }

    private void ThrowIfDisposed() {
        if (_disposed) {
            throw new ObjectDisposedException(nameof(SslFaxState));
        }
    }
}

/// <summary>
/// Optional compatibility facade retaining the original C function names.
/// </summary>
public static class SslFax {
    public static SslFaxState sslfax_init(SslFaxState? state = null) {
        return state is null ? new SslFaxState() : state.Initialize();
    }

    public static void sslfax_setup(
        SslFaxState state,
        SpanPutMessageHandler? putMessage,
        SpanGetMessageHandler? getMessage,
        HdlcFrameHandler? hdlcAccept,
        HdlcUnderflowHandler? hdlcTransmitUnderflow,
        bool transmitUsesHdlc,
        bool receiveUsesHdlc,
        SpanGetByteHandler? getPhase,
        object? userData) {
        ArgumentNullException.ThrowIfNull(state);
        state.Setup(
            putMessage,
            getMessage,
            hdlcAccept,
            hdlcTransmitUnderflow,
            transmitUsesHdlc,
            receiveUsesHdlc,
            getPhase,
            userData);
    }

    public static bool sslfax_start_client(SslFaxState state) {
        ArgumentNullException.ThrowIfNull(state);
        return state.StartClient();
    }

    public static void sslfax_cleanup(SslFaxState state, bool sustain) {
        ArgumentNullException.ThrowIfNull(state);
        state.Cleanup(sustain);
    }

    public static int sslfax_tx(SslFaxState state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Transmit(samples, length);
    }

    public static int sslfax_rx(SslFaxState state, short[] samples, int length) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Receive(samples, length);
    }

    public static int sslfax_write(
        SslFaxState state,
        byte[] buffer,
        int count,
        long milliseconds,
        bool filter,
        bool sustain) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Write(buffer, count, milliseconds, filter, sustain);
    }

    public static int sslfax_read(
        SslFaxState state,
        byte[] buffer,
        int count,
        long milliseconds,
        bool sustain,
        bool carryOn) {
        ArgumentNullException.ThrowIfNull(state);
        return state.Read(buffer, count, milliseconds, sustain, carryOn);
    }
}

