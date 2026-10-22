using System.Diagnostics.CodeAnalysis;

namespace Harp.Toolkit.Firmware.PIC32;

/// <summary>
/// Decodes PIC32 bootloader frames from a byte stream one byte at a time, so that frames split
/// across several reads are reassembled.
/// </summary>
internal sealed class FrameDecoder
{
    readonly List<byte> body = new();
    bool inFrame;
    bool escaped;

    public bool TryDecode(byte value, [NotNullWhen(true)] out byte[]? frame)
    {
        frame = null;
        if (escaped)
        {
            escaped = false;
            Append(value);
        }
        else if (value == Frame.StartOfHeader)
        {
            body.Clear();
            inFrame = true;
        }
        else if (!inFrame)
        {
            return false;
        }
        else if (value == Frame.DataLinkEscape)
        {
            escaped = true;
        }
        else if (value == Frame.EndOfTransmission)
        {
            inFrame = false;
            if (body.Count > Frame.ChecksumSize)
            {
                var bytes = body.ToArray();
                var length = bytes.Length - Frame.ChecksumSize;
                var crc = Crc16.Compute(bytes.AsSpan(0, length));
                if (bytes[length] == (byte)crc && bytes[length + 1] == (byte)(crc >> 8))
                {
                    frame = bytes.AsSpan(0, length).ToArray();
                    return true;
                }
            }
        }
        else
        {
            Append(value);
        }

        return false;
    }

    void Append(byte value)
    {
        if (body.Count < Frame.MaxLength)
        {
            body.Add(value);
        }
        else
        {
            inFrame = false;
            body.Clear();
        }
    }
}
