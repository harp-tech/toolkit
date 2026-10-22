using System.Buffers.Binary;
using System.Threading.Channels;
using Harp.Toolkit.Firmware.PIC32;

namespace Harp.Toolkit.Tests;

/// <summary>
/// Emulates the device side of the PIC32 bootloader over an in-memory stream, so that a whole
/// update can run without hardware.
/// </summary>
/// <remarks>
/// Flash starts out holding a previous firmware rather than erased, so programming without an
/// erase is visible in <see cref="UnerasedWrites"/>. Each write to the stream is expected to
/// carry one whole frame.
/// </remarks>
sealed class PIC32BootloaderEmulator : Stream
{
    public const int MajorVersion = 4;
    public const int MinorVersion = 1;

    const uint KernelSegment0 = 0x80000000;
    const byte PreviousFirmwareValue = 0x00;
    const byte ErasedValue = 0xFF;

    readonly FrameDecoder decoder = new();
    readonly Channel<byte> replies = Channel.CreateUnbounded<byte>();
    readonly Dictionary<byte, int> requestCounts = new();
    uint baseAddress;
    int programFrames;

    public PIC32BootloaderEmulator()
    {
        Array.Fill(Flash, PreviousFirmwareValue);
    }

    public byte[] Flash { get; } = new byte[FirmwareImage.FlashSize];

    public int UnerasedWrites { get; private set; }

    public int MaxFrameLength { get; private set; }

    public bool StartedApplication { get; private set; }

    /// <summary>
    /// Gets or sets the largest number of bytes a single read returns, to split replies across reads.
    /// </summary>
    public int ReadChunkSize { get; set; } = int.MaxValue;

    /// <summary>
    /// Gets or sets bytes sent ahead of the first reply, as a port opened mid-stream would see.
    /// </summary>
    public byte[]? StaleBytes { get; set; }

    /// <summary>
    /// Gets or sets the zero-based index of a program frame that the emulator neither writes nor
    /// answers, to emulate a record checksum failure.
    /// </summary>
    public int? SilentProgramFrame { get; set; }

    public int GetRequestCount(byte command)
    {
        return requestCounts.GetValueOrDefault(command);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (!await replies.Reader.WaitToReadAsync(cancellationToken))
        {
            return 0;
        }

        var count = 0;
        var maxCount = Math.Min(buffer.Length, ReadChunkSize);
        while (count < maxCount && replies.Reader.TryRead(out var value))
        {
            buffer.Span[count++] = value;
        }

        return count;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Receive(buffer.AsSpan(offset, count));
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        Receive(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    void Receive(ReadOnlySpan<byte> data)
    {
        MaxFrameLength = Math.Max(MaxFrameLength, data.Length);
        foreach (var value in data)
        {
            if (decoder.TryDecode(value, out var frame))
            {
                Handle(frame);
            }
        }
    }

    void Handle(byte[] frame)
    {
        var command = frame[0];
        requestCounts[command] = GetRequestCount(command) + 1;
        switch (command)
        {
            case 1:
                Reply(command, MinorVersion, MajorVersion);
                break;
            case 2:
                Array.Fill(Flash, ErasedValue);
                Reply(command);
                break;
            case 3:
                if (programFrames++ != SilentProgramFrame && Program(frame.AsSpan(1)))
                {
                    Reply(command);
                }
                break;
            case 4:
                var address = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1));
                var length = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(5));
                var crc = Crc16.Compute(Flash.AsSpan((int)(address - FirmwareImage.FlashStart), (int)length));
                Reply(command, (byte)crc, (byte)(crc >> 8));
                break;
            case 5:
                StartedApplication = true;
                break;
        }
    }

    bool Program(ReadOnlySpan<byte> records)
    {
        while (records.Length > 0)
        {
            var record = records.Slice(0, records[0] + 5);
            var checksum = 0;
            foreach (var value in record)
            {
                checksum += value;
            }

            if ((byte)checksum != 0)
            {
                return false;
            }

            var data = record.Slice(4, record[0]);
            switch (record[3])
            {
                case 0:
                    var address = (baseAddress + (uint)((record[1] << 8) | record[2])) | KernelSegment0;
                    if (address >= FirmwareImage.FlashStart &&
                        address + data.Length <= FirmwareImage.FlashStart + FirmwareImage.FlashSize)
                    {
                        var target = Flash.AsSpan((int)(address - FirmwareImage.FlashStart), data.Length);
                        foreach (var value in target)
                        {
                            if (value != ErasedValue) UnerasedWrites++;
                        }

                        data.CopyTo(target);
                    }
                    break;
                case 2:
                    baseAddress = (uint)((data[0] << 8) | data[1]) << 4;
                    break;
                case 4:
                    baseAddress = (uint)((data[0] << 8) | data[1]) << 16;
                    break;
            }

            records = records.Slice(record.Length);
        }

        return true;
    }

    void Reply(byte command, params byte[] payload)
    {
        if (StaleBytes != null)
        {
            foreach (var value in StaleBytes)
            {
                replies.Writer.TryWrite(value);
            }

            StaleBytes = null;
        }

        foreach (var value in Frame.Encode(command, payload))
        {
            replies.Writer.TryWrite(value);
        }
    }
}
