using System.Buffers.Binary;
using System.Diagnostics;

namespace Harp.Toolkit.Firmware.PIC32;

/// <summary>
/// Provides asynchronous operations to update the firmware of a PIC32 microcontroller running
/// the Microchip serial bootloader described in application note AN1388.
/// </summary>
/// <remarks>
/// The bootloader communicates over a serial port at <see cref="BaudRate"/> with eight data
/// bits, no parity, one stop bit and no handshaking. The client knows nothing about the device
/// that hosts the microcontroller, so entering and leaving the bootloader is up to the caller.
/// </remarks>
public sealed class Bootloader
{
    /// <summary>
    /// The baud rate of the serial port connected to the bootloader.
    /// </summary>
    public const int BaudRate = 115200;

    const byte ReadBootInfo = 1;
    const byte EraseFlash = 2;
    const byte ProgramFlash = 3;
    const byte ReadCrc = 4;
    const byte JumpToApplication = 5;

    const int ReadBootInfoAttempts = 30;
    const int ReadBootInfoTimeoutMilliseconds = 200;
    const int EraseFlashAttempts = 3;
    const int EraseFlashTimeoutMilliseconds = 5000;
    const int ProgramFlashTimeoutMilliseconds = 500;
    const int ReadCrcAttempts = 3;
    const int ReadCrcTimeoutMilliseconds = 5000;

    const int MaxRecordsPerFrame = 11;
    const int ProgramFrameOverhead = 3 + 2 * Frame.ChecksumSize;

    readonly Stream stream;
    readonly FrameDecoder decoder = new();
    readonly byte[] readBuffer = new byte[Frame.MaxLength];
    Task<int>? pendingRead;
    int readOffset;
    int readCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bootloader"/> class communicating over the
    /// specified stream.
    /// </summary>
    /// <param name="stream">
    /// The stream connected to the bootloader, usually the base stream of an open serial port.
    /// The caller keeps ownership of the stream.
    /// </param>
    public Bootloader(Stream stream)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
    }

    /// <summary>
    /// Asynchronously reads the version of the bootloader.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property contains the major and minor version of the bootloader.
    /// </returns>
    /// <remarks>
    /// The request is repeated until the bootloader answers, so this also waits for a bootloader
    /// that is still starting.
    /// </remarks>
    public async Task<Version> ReadBootInfoAsync(CancellationToken cancellationToken = default)
    {
        var reply = await CommandAsync(
            ReadBootInfo,
            Array.Empty<byte>(),
            ReadBootInfoAttempts,
            ReadBootInfoTimeoutMilliseconds,
            cancellationToken);
        RequireReplyLength(reply, 3);
        return new Version(reply[2], reply[1]);
    }

    /// <summary>
    /// Asynchronously erases the whole application flash.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// The bootloader answers once the erase completes. A device whose application flash is
    /// erased starts the bootloader on every reset, so an update interrupted from this point on
    /// can be repeated.
    /// </remarks>
    public async Task EraseFlashAsync(CancellationToken cancellationToken = default)
    {
        await CommandAsync(
            EraseFlash,
            Array.Empty<byte>(),
            EraseFlashAttempts,
            EraseFlashTimeoutMilliseconds,
            cancellationToken);
    }

    /// <summary>
    /// Asynchronously programs the records of the specified image into erased application flash.
    /// </summary>
    /// <param name="image">The firmware image to program.</param>
    /// <param name="progress">
    /// The optional object that receives the percentage of image records programmed, from 0 to 100.
    /// </param>
    /// <param name="cancellationToken">The token that cancels the operation between frames.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// A frame is never resent. The bootloader stops at a record it cannot accept and does not
    /// answer, after writing the records before it, and flash that is already programmed cannot
    /// be programmed again without an erase. After a failure, start again from
    /// <see cref="EraseFlashAsync"/>.
    /// </remarks>
    public async Task ProgramFlashAsync(
        FirmwareImage image,
        IProgress<int>? progress = default,
        CancellationToken cancellationToken = default)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));

        var percent = 0;
        var recordsProgrammed = 0;
        progress?.Report(percent);
        foreach (var (payload, recordCount) in CreateProgramPayloads(image.Records))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CommandAsync(ProgramFlash, payload, 1, ProgramFlashTimeoutMilliseconds, cancellationToken);
            recordsProgrammed += recordCount;
            var current = recordsProgrammed * 100 / image.Records.Count;
            if (current != percent)
            {
                percent = current;
                progress?.Report(percent);
            }
        }
    }

    /// <summary>
    /// Asynchronously reads the CRC-16/XMODEM checksum of the specified range of flash.
    /// </summary>
    /// <param name="address">The address of the first byte in the range.</param>
    /// <param name="length">The number of bytes in the range.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property contains the checksum computed by the bootloader.
    /// </returns>
    public async Task<ushort> ReadCrcAsync(uint address, uint length, CancellationToken cancellationToken = default)
    {
        var payload = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, address);
        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), length);
        var reply = await CommandAsync(
            ReadCrc,
            payload,
            ReadCrcAttempts,
            ReadCrcTimeoutMilliseconds,
            cancellationToken);
        RequireReplyLength(reply, 3);
        return BinaryPrimitives.ReadUInt16LittleEndian(reply.AsSpan(1));
    }

    /// <summary>
    /// Asynchronously determines whether application flash holds the specified image.
    /// </summary>
    /// <param name="image">The firmware image to compare with application flash.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property is <b>true</b> if the checksum of application flash matches the image;
    /// otherwise, <b>false</b>.
    /// </returns>
    public async Task<bool> VerifyAsync(FirmwareImage image, CancellationToken cancellationToken = default)
    {
        if (image == null) throw new ArgumentNullException(nameof(image));
        var crc = await ReadCrcAsync(image.StartAddress, image.Length, cancellationToken);
        return crc == image.Crc;
    }

    /// <summary>
    /// Asynchronously requests the bootloader to start the application.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// The bootloader does not answer this request. It resets the microcontroller, and the
    /// bootloader starts again instead of the application if the condition that entered it
    /// still holds.
    /// </remarks>
    public async Task JumpToApplicationAsync(CancellationToken cancellationToken = default)
    {
        var frame = Frame.Encode(JumpToApplication, Array.Empty<byte>());
        await stream.WriteAsync(frame, cancellationToken);
    }

    static List<(byte[] Payload, int RecordCount)> CreateProgramPayloads(IReadOnlyList<ReadOnlyMemory<byte>> records)
    {
        var payloads = new List<(byte[], int)>();
        var payload = new List<byte>();
        var recordCount = 0;
        var frameLength = ProgramFrameOverhead;
        foreach (var record in records)
        {
            var recordLength = Frame.GetEscapedLength(record.Span);
            if (ProgramFrameOverhead + recordLength > Frame.MaxLength)
            {
                throw new ArgumentException("The image holds a record too long to fit in a bootloader frame.", nameof(records));
            }

            if (recordCount == MaxRecordsPerFrame || frameLength + recordLength > Frame.MaxLength)
            {
                payloads.Add((payload.ToArray(), recordCount));
                payload.Clear();
                recordCount = 0;
                frameLength = ProgramFrameOverhead;
            }

            payload.AddRange(record.Span);
            recordCount++;
            frameLength += recordLength;
        }

        if (recordCount > 0)
        {
            payloads.Add((payload.ToArray(), recordCount));
        }

        return payloads;
    }

    static void RequireReplyLength(byte[] reply, int length)
    {
        if (reply.Length != length)
        {
            throw new InvalidDataException("The bootloader replied with an invalid message length.");
        }
    }

    async Task<byte[]> CommandAsync(
        byte command,
        byte[] payload,
        int attempts,
        int timeout,
        CancellationToken cancellationToken)
    {
        var frame = Frame.Encode(command, payload);
        for (int i = 1; ; i++)
        {
            await stream.WriteAsync(frame, cancellationToken);
            try
            {
                return await ReceiveAsync(command, timeout, cancellationToken);
            }
            catch (TimeoutException) when (i < attempts)
            {
            }
        }
    }

    async Task<byte[]> ReceiveAsync(byte command, int timeout, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            while (readOffset < readCount)
            {
                if (decoder.TryDecode(readBuffer[readOffset++], out var reply) && reply[0] == command)
                {
                    return reply;
                }
            }

            pendingRead ??= stream.ReadAsync(readBuffer, 0, readBuffer.Length, CancellationToken.None);
            var remaining = timeout - (int)stopwatch.ElapsedMilliseconds;
            if (remaining > 0)
            {
                using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delay = Task.Delay(remaining, delayCancellation.Token);
                await Task.WhenAny(pendingRead, delay);
                delayCancellation.Cancel();
            }

            if (!pendingRead.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"The bootloader did not reply to command {command}.");
            }

            var read = pendingRead;
            pendingRead = null;
            readOffset = 0;
            readCount = 0;
            readCount = await read;
            if (readCount == 0)
            {
                throw new EndOfStreamException("The bootloader stream was closed.");
            }
        }
    }
}
