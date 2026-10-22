namespace Harp.Toolkit.Firmware.PIC32;

/// <summary>
/// Provides the framing of messages exchanged with the PIC32 bootloader.
/// </summary>
internal static class Frame
{
    public const byte StartOfHeader = 0x01;
    public const byte EndOfTransmission = 0x04;
    public const byte DataLinkEscape = 0x10;

    public const int ChecksumSize = 2;
    public const int MaxLength = 512;

    public static bool IsControl(byte value)
    {
        return value is StartOfHeader or EndOfTransmission or DataLinkEscape;
    }

    public static int GetEscapedLength(ReadOnlySpan<byte> data)
    {
        var length = data.Length;
        foreach (var value in data)
        {
            if (IsControl(value))
            {
                length++;
            }
        }

        return length;
    }

    public static byte[] Encode(byte command, ReadOnlySpan<byte> payload)
    {
        var body = new byte[1 + payload.Length + ChecksumSize];
        body[0] = command;
        payload.CopyTo(body.AsSpan(1));
        var crc = Crc16.Compute(body.AsSpan(0, body.Length - ChecksumSize));
        body[body.Length - 2] = (byte)crc;
        body[body.Length - 1] = (byte)(crc >> 8);

        var frame = new byte[GetEscapedLength(body) + 2];
        var index = 0;
        frame[index++] = StartOfHeader;
        foreach (var value in body)
        {
            if (IsControl(value))
            {
                frame[index++] = DataLinkEscape;
            }

            frame[index++] = value;
        }

        frame[index] = EndOfTransmission;
        return frame;
    }
}
