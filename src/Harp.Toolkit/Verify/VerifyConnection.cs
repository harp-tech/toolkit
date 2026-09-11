using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Bonsai.Harp;

namespace Harp.Toolkit.Verify;

/// <summary>
/// A single Harp connection shared by every test in a verification run.
/// </summary>
public sealed class VerifyConnection : IDisposable
{
    const int ConnectDelayMilliseconds = 200;
    const int PortReleaseDelayMilliseconds = 300;
    const int OpenTimeoutMilliseconds = 10000;
    const int ReadTimeoutMilliseconds = 2000;
    const int ReadyAttempts = 5;
    const int ReadyTimeoutMilliseconds = 1000;

    readonly Subject<HarpMessage> requests = new();
    readonly IConnectableObservable<HarpMessage> messages;
    readonly IDisposable subscription;

    VerifyConnection(string portName, int whoAmI)
    {
        WhoAmI = whoAmI;
        var device = new Bonsai.Harp.Device(whoAmI)
        {
            PortName = portName,
            IgnoreErrors = true,
            OperationMode = OperationMode.Standby,
            DumpRegisters = false,
        };
        messages = device.Generate(requests).Publish();
        subscription = messages.Connect();
    }

    /// <summary>
    /// Opens the shared connection, reading the device identity first so the connection can be
    /// constructed with a known WhoAmI, which suppresses the device name probe that would
    /// otherwise open a second port in the background.
    /// </summary>
    public static async Task<VerifyConnection> OpenAsync(string portName, CancellationToken cancellationToken = default)
    {
        var whoAmI = await ReadIdentityAsync(portName, cancellationToken);
        await Task.Delay(PortReleaseDelayMilliseconds, cancellationToken);

        var retryStart = Stopwatch.GetTimestamp();
        while (true)
        {
            var connection = new VerifyConnection(portName, whoAmI);
            try
            {
                await connection.WaitUntilReadyAsync(cancellationToken);
                return connection;
            }
            catch (Exception ex) when (
                IsRetryableOpenFailure(ex) &&
                IsWithinRetryBudget(retryStart) &&
                !cancellationToken.IsCancellationRequested)
            {
                connection.Dispose();
                await Task.Delay(PortReleaseDelayMilliseconds, cancellationToken);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
    }

    static async Task<int> ReadIdentityAsync(string portName, CancellationToken cancellationToken)
    {
        var retryStart = Stopwatch.GetTimestamp();
        while (true)
        {
            using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readTimeout.CancelAfter(ReadTimeoutMilliseconds);
            try
            {
                using var probe = new AsyncDevice(portName);
                return await probe.ReadWhoAmIAsync(readTimeout.Token);
            }
            catch (Exception ex) when (
                (IsRetryableOpenFailure(ex) || ex is OperationCanceledException) &&
                IsWithinRetryBudget(retryStart) &&
                !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(PortReleaseDelayMilliseconds, cancellationToken);
            }
        }
    }

    static bool IsRetryableOpenFailure(Exception ex)
    {
        return ex is UnauthorizedAccessException || ex is IOException || ex is TimeoutException;
    }

    static bool IsWithinRetryBudget(long retryStart)
    {
        return Stopwatch.GetElapsedTime(retryStart).TotalMilliseconds < OpenTimeoutMilliseconds;
    }

    /// <summary>
    /// The device identifier validated when the connection was opened.
    /// </summary>
    public int WhoAmI { get; }

    /// <summary>
    /// Every message received from the device, before any reply correlation.
    /// </summary>
    public IObservable<HarpMessage> Messages => messages;

    /// <summary>
    /// Sends a message without awaiting a reply.
    /// </summary>
    public void Write(HarpMessage message) => requests.OnNext(message);

    /// <summary>
    /// Sends the specified request and awaits the matching reply, failing with a
    /// <see cref="TimeoutException"/> if the device does not answer in time.
    /// </summary>
    public async Task<HarpMessage> CommandAsync(HarpMessage command, CancellationToken cancellationToken = default)
    {
        using var replyTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        replyTimeout.CancelAfter(ReadTimeoutMilliseconds);
        var reply = messages.FirstAsync(message =>
        {
            var match = message.IsMatch(command.Address, command.MessageType);
            if (match && message.Error)
            {
                throw new HarpException(message);
            }

            return match;
        }).RunAsync(replyTimeout.Token);

        Write(command);
        try
        {
            return await reply;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"The device did not reply to a {command.MessageType} request at address " +
                $"{command.Address} within {ReadTimeoutMilliseconds} ms.");
        }
    }

    /// <summary>
    /// Sends the specified messages and collects everything received for the given duration.
    /// </summary>
    public async Task<IList<HarpMessage>> WriteAndCollectAsync(
        IEnumerable<HarpMessage> messagesToWrite,
        TimeSpan listenDuration,
        CancellationToken cancellationToken = default)
    {
        var collected = new List<HarpMessage>();
        using (messages.Subscribe(message =>
        {
            lock (collected)
            {
                collected.Add(message);
            }
        }))
        {
            foreach (var message in messagesToWrite)
            {
                Write(message);
            }

            await Task.Delay(listenDuration, cancellationToken);
        }

        lock (collected)
        {
            return collected.ToList();
        }
    }

    public async Task<byte> ReadByteAsync(int address, CancellationToken cancellationToken = default)
    {
        var reply = await CommandAsync(HarpCommand.ReadByte(address), cancellationToken);
        return reply.GetPayloadByte();
    }

    public async Task<byte[]> ReadByteArrayAsync(int address, CancellationToken cancellationToken = default)
    {
        var reply = await CommandAsync(HarpCommand.ReadByte(address), cancellationToken);
        return reply.GetPayloadArray<byte>();
    }

    public async Task<ushort> ReadUInt16Async(int address, CancellationToken cancellationToken = default)
    {
        var reply = await CommandAsync(HarpCommand.ReadUInt16(address), cancellationToken);
        return reply.GetPayloadUInt16();
    }

    public async Task<uint> ReadUInt32Async(int address, CancellationToken cancellationToken = default)
    {
        var reply = await CommandAsync(HarpCommand.ReadUInt32(address), cancellationToken);
        return reply.GetPayloadUInt32();
    }

    public async Task<int> ReadWhoAmIAsync(CancellationToken cancellationToken = default)
    {
        return await ReadUInt16Async(Bonsai.Harp.WhoAmI.Address, cancellationToken);
    }

    public async Task<string> ReadDeviceNameAsync(CancellationToken cancellationToken = default)
    {
        var payload = await ReadByteArrayAsync(DeviceName.Address, cancellationToken);
        var terminator = Array.IndexOf(payload, (byte)0);
        return Encoding.ASCII.GetString(payload, 0, terminator < 0 ? payload.Length : terminator);
    }

    public async Task<int> ReadAssemblyVersionAsync(CancellationToken cancellationToken = default)
    {
        return await ReadByteAsync(AssemblyVersion.Address, cancellationToken);
    }

    public async Task<int> ReadSerialNumberAsync(CancellationToken cancellationToken = default)
    {
        return await ReadUInt16Async(SerialNumber.Address, cancellationToken);
    }

    public async Task<HarpVersion> ReadHardwareVersionAsync(CancellationToken cancellationToken = default)
    {
        var major = await ReadByteAsync(HardwareVersionHigh.Address, cancellationToken);
        var minor = await ReadByteAsync(HardwareVersionLow.Address, cancellationToken);
        return new HarpVersion(major, minor);
    }

    public async Task<HarpVersion> ReadFirmwareVersionAsync(CancellationToken cancellationToken = default)
    {
        var major = await ReadByteAsync(FirmwareVersionHigh.Address, cancellationToken);
        var minor = await ReadByteAsync(FirmwareVersionLow.Address, cancellationToken);
        return new HarpVersion(major, minor);
    }

    public async Task<uint> ReadTimestampSecondsAsync(CancellationToken cancellationToken = default)
    {
        return await ReadUInt32Async(TimestampSeconds.Address, cancellationToken);
    }

    public async Task WriteTimestampSecondsAsync(uint seconds, CancellationToken cancellationToken = default)
    {
        await CommandAsync(HarpCommand.WriteUInt32(TimestampSeconds.Address, seconds), cancellationToken);
    }

    /// <summary>
    /// Reads the identity registers reported in the run header, leaving any register the device
    /// does not answer within the read timeout unreported rather than failing the run.
    /// </summary>
    internal async Task<DeviceIdentity> ReadDeviceIdentityAsync(CancellationToken cancellationToken = default)
    {
        var name = await TryReadAsync(ReadDeviceNameAsync, cancellationToken);
        var hardwareVersion = await TryReadAsync(ReadHardwareVersionAsync, cancellationToken);
        var firmwareVersion = await TryReadAsync(ReadFirmwareVersionAsync, cancellationToken);
        return new DeviceIdentity(WhoAmI, name, hardwareVersion, firmwareVersion);
    }

    /// <summary>
    /// Reads the protocol version the device declares, leaving it undeclared when the version
    /// register is unreadable, carries an unexpected length, or reports all zeros.
    /// </summary>
    internal async Task<Suites.SemanticVersion?> ReadProtocolVersionAsync(CancellationToken cancellationToken = default)
    {
        var reply = await TryReadAsync(
            token => CommandAsync(HarpCommand.ReadByte(Suites.Version.Address), token),
            cancellationToken);
        if (reply is null || reply.GetPayloadArray<byte>().Length != Suites.Version.RegisterLength)
            return null;

        var protocolVersion = Suites.Version.GetPayload(reply).ProtocolVersion;
        return protocolVersion.Major == 0 ? null : protocolVersion;
    }

    static async Task<T?> TryReadAsync<T>(
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await read(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(ConnectDelayMilliseconds, cancellationToken);
        for (int attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ReadyTimeoutMilliseconds);
            try
            {
                await ReadWhoAmIAsync(timeout.Token);
                return;
            }
            catch (OperationCanceledException) when (attempt < ReadyAttempts && !cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    public void Dispose()
    {
        subscription.Dispose();
        requests.Dispose();
    }
}
