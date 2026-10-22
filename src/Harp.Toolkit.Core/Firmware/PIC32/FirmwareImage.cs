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

    /// <summary>
    /// Returns a copy of the image that programs the data record holding the specified address
    /// after every other record.
    /// </summary>
    /// <param name="address">An address in application flash, such as the reset address of the application.</param>
    /// <returns>
    /// A new <see cref="FirmwareImage"/> object whose last data record holds <paramref name="address"/>,
    /// or the current image if no data record holds it.
    /// </returns>
    /// <remarks>
    /// A bootloader that starts the application whenever its reset address is programmed can
    /// recover from an interrupted update only while that address is still erased. The moved
    /// record follows an extended linear address record, so it programs the same flash as before.
    /// </remarks>
    public FirmwareImage WithRecordLast(uint address)
    {
        var baseAddress = 0u;
        for (int i = 0; i < Records.Count; i++)
        {
            var record = Records[i].Span;
            switch ((RecordType)record[3])
            {
                case RecordType.ExtendedSegmentAddress:
                    baseAddress = (uint)((record[4] << 8) | record[5]) << 4;
                    break;
                case RecordType.ExtendedLinearAddress:
                    baseAddress = (uint)((record[4] << 8) | record[5]) << 16;
                    break;
                case RecordType.Data:
                    var recordAddress = baseAddress + (uint)((record[1] << 8) | record[2]);
                    var start = recordAddress | KernelSegment0;
                    if (address >= start && address < start + record[0])
                    {
                        return MoveRecordLast(i, recordAddress);
                    }
                    break;
            }
        }

        return this;
    }

    FirmwareImage MoveRecordLast(int index, uint recordAddress)
    {
        var data = Records[index].Slice(4, Records[index].Span[0]).ToArray();
        var records = new List<ReadOnlyMemory<byte>>(Records.Count + 1);
        for (int i = 0; i < Records.Count - 1; i++)
        {
            if (i != index)
            {
                records.Add(Records[i]);
            }
        }

        records.Add(CreateRecord(RecordType.ExtendedLinearAddress, 0, (byte)(recordAddress >> 24), (byte)(recordAddress >> 16)));
        records.Add(CreateRecord(RecordType.Data, (ushort)recordAddress, data));
        records.Add(Records[Records.Count - 1]);
        return new FirmwareImage(records, StartAddress, Length, Crc);
    }

    static byte[] CreateRecord(RecordType type, ushort address, params byte[] data)
    {
        var record = new byte[data.Length + 5];
        record[0] = (byte)data.Length;
        record[1] = (byte)(address >> 8);
        record[2] = (byte)address;
        record[3] = (byte)type;
        data.CopyTo(record, 4);
        var checksum = 0;
        for (int i = 0; i < record.Length - 1; i++)
        {
            checksum += record[i];
        }

        record[record.Length - 1] = (byte)-checksum;
        return record;
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
