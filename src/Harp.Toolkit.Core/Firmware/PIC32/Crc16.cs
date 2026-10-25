namespace Harp.Toolkit.Firmware.PIC32;

/// <summary>
/// Provides the CRC-16/XMODEM checksum used by the PIC32 bootloader protocol.
/// </summary>
internal static class Crc16
{
    const int Polynomial = 0x1021;

    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0;
        foreach (var value in data)
        {
            crc ^= value << 8;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0 ? (crc << 1) ^ Polynomial : crc << 1;
                crc &= 0xFFFF;
            }
        }

        return (ushort)crc;
    }
}
