namespace Harp.Toolkit.Firmware.PIC32;

/// <summary>
/// Represents a PIC32 application firmware image read from a file in Intel HEX format.
/// </summary>
/// <remarks>
/// The bootloader programs the records of the image as they appear in the file, so the image
/// keeps each record rather than a binary blob. Only data inside application flash counts
/// towards the verified range.
/// </remarks>
public sealed class FirmwareImage
{
    internal const uint FlashStart = 0x9D000000;
    internal const uint FlashSize = 0x200000;
    const uint KernelSegment0 = 0x80000000;
    const byte ErasedValue = 0xFF;
    const char StartCode = ':';

    private FirmwareImage(IReadOnlyList<ReadOnlyMemory<byte>> records, uint startAddress, uint length, ushort crc)
    {
        Records = records;
        StartAddress = startAddress;
        Length = length;
        Crc = crc;
    }

    /// <summary>
    /// Gets the decoded bytes of each record in file order. A record holds its byte count,
    /// address, type, data and checksum.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<byte>> Records { get; }

    /// <summary>
    /// Gets the address of the first byte of application flash programmed by the image.
    /// </summary>
    public uint StartAddress { get; }

    /// <summary>
    /// Gets the number of bytes from <see cref="StartAddress"/> to the last byte of application
    /// flash programmed by the image.
    /// </summary>
    public uint Length { get; }

    /// <summary>
    /// Gets the CRC-16/XMODEM checksum of the range of application flash programmed by the
    /// image. Bytes in the range that the image does not program read as erased flash.
    /// </summary>
    public ushort Crc { get; }

    /// <summary>
    /// Creates a <see cref="FirmwareImage"/> object from the specified file in Intel HEX format.
    /// </summary>
    /// <param name="path">The name of the file from which to create the <see cref="FirmwareImage"/>.</param>
    /// <returns>A new <see cref="FirmwareImage"/> object holding the records of the file.</returns>
    public static FirmwareImage FromFile(string path)
    {
        using var stream = File.OpenRead(path);
        return FromStream(stream);
    }

    /// <summary>
    /// Creates a <see cref="FirmwareImage"/> object from the specified ASCII stream in Intel HEX
    /// format.
    /// </summary>
    /// <param name="stream">The ASCII stream in Intel HEX format from which to read the image.</param>
    /// <returns>A new <see cref="FirmwareImage"/> object holding the records of the stream.</returns>
    public static FirmwareImage FromStream(Stream stream)
    {
        var records = new List<ReadOnlyMemory<byte>>();
        var segments = new List<(uint Address, ReadOnlyMemory<byte> Data)>();
        var baseAddress = 0u;
        var lineNumber = 0;
        var endOfFile = false;

        using var reader = new StreamReader(stream);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (endOfFile)
            {
                throw new ArgumentException($"{lineNumber}: Record found after the end of file record in hex stream.", nameof(stream));
            }

            if (line[0] != StartCode)
            {
                throw new ArgumentException($"{lineNumber}: Invalid record start code found in hex stream.", nameof(stream));
            }

            byte[] record;
            try
            {
                record = Convert.FromHexString(line.AsSpan(1));
            }
            catch (FormatException)
            {
                throw new ArgumentException($"{lineNumber}: Invalid hex digits found in hex stream.", nameof(stream));
            }

            if (record.Length < 5 || record.Length != record[0] + 5)
            {
                throw new ArgumentException($"{lineNumber}: Invalid record length found in hex stream.", nameof(stream));
            }

            var checksum = 0;
            foreach (var value in record)
            {
                checksum += value;
            }

            if ((byte)checksum != 0)
            {
                throw new ArgumentException($"{lineNumber}: Invalid data checksum found in hex stream.", nameof(stream));
            }

            var address = (uint)((record[1] << 8) | record[2]);
            var data = record.AsMemory(4, record[0]);
            switch ((RecordType)record[3])
            {
                case RecordType.Data:
                    segments.Add((baseAddress + address, data));
                    break;
                case RecordType.EndOfFile:
                    endOfFile = true;
                    break;
                case RecordType.ExtendedSegmentAddress:
                    if (data.Length != 2) throw new ArgumentException($"{lineNumber}: Invalid extended segment address payload found in hex stream.", nameof(stream));
                    baseAddress = (uint)((record[4] << 8) | record[5]) << 4;
                    break;
                case RecordType.ExtendedLinearAddress:
                    if (data.Length != 2) throw new ArgumentException($"{lineNumber}: Invalid extended linear address payload found in hex stream.", nameof(stream));
                    baseAddress = (uint)((record[4] << 8) | record[5]) << 16;
                    break;
                case RecordType.StartSegmentAddress:
                case RecordType.StartLinearAddress:
                    throw new NotSupportedException($"{lineNumber}: Unsupported record type found in hex stream.");
                default:
                    throw new ArgumentException($"{lineNumber}: Invalid record type found in hex stream.", nameof(stream));
            }

            records.Add(record);
        }

        if (!endOfFile)
        {
            throw new ArgumentException("No end of file record found in hex stream.", nameof(stream));
        }

        var startAddress = uint.MaxValue;
        var endAddress = 0u;
        for (int i = segments.Count - 1; i >= 0; i--)
        {
            var segmentAddress = segments[i].Address | KernelSegment0;
            var segmentEnd = segmentAddress + (uint)segments[i].Data.Length;
            if (segmentAddress < FlashStart || segmentEnd > FlashStart + FlashSize)
            {
                segments.RemoveAt(i);
                continue;
            }

            segments[i] = (segmentAddress, segments[i].Data);
            startAddress = Math.Min(startAddress, segmentAddress);
            endAddress = Math.Max(endAddress, segmentEnd);
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("No data for application flash found in hex stream.", nameof(stream));
        }

        var flash = new byte[endAddress - startAddress];
        Array.Fill(flash, ErasedValue);
        foreach (var (segmentAddress, data) in segments)
        {
            data.Span.CopyTo(flash.AsSpan((int)(segmentAddress - startAddress)));
        }

        return new FirmwareImage(records, startAddress, (uint)flash.Length, Crc16.Compute(flash));
    }

    enum RecordType : byte
    {
        Data = 0,
        EndOfFile = 1,
        ExtendedSegmentAddress = 2,
        StartSegmentAddress = 3,
        ExtendedLinearAddress = 4,
        StartLinearAddress = 5
    }
}
