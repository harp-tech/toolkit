using System.Text;
using Harp.Toolkit.Firmware.PIC32;

namespace Harp.Toolkit.Tests;

/// <summary>
/// Builds small Intel HEX images for tests that need a specific record layout.
/// </summary>
static class IntelHexHelper
{
    public const byte Data = 0;
    public const byte EndOfFile = 1;
    public const byte ExtendedLinearAddress = 4;

    public static string Record(byte type, ushort address, params byte[] data)
    {
        var record = new byte[data.Length + 5];
        record[0] = (byte)data.Length;
        record[1] = (byte)(address >> 8);
        record[2] = (byte)address;
        record[3] = type;
        data.CopyTo(record, 4);
        var checksum = 0;
        for (int i = 0; i < record.Length - 1; i++)
        {
            checksum += record[i];
        }

        record[record.Length - 1] = (byte)-checksum;
        return ":" + Convert.ToHexString(record);
    }

    public static string LinearAddress(ushort upperAddress)
    {
        return Record(ExtendedLinearAddress, 0, (byte)(upperAddress >> 8), (byte)upperAddress);
    }

    public static Stream CreateStream(params string[] records)
    {
        return new MemoryStream(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n"));
    }

    public static FirmwareImage CreateImage(params string[] records)
    {
        using var stream = CreateStream(records);
        return FirmwareImage.FromStream(stream);
    }
}
