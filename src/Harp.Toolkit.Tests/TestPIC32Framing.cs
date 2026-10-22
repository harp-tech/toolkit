using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.PIC32;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestPIC32Framing
{
    static readonly byte[] BootInfoReplyFrame = { 0x01, 0x10, 0x01, 0x10, 0x01, 0x10, 0x04, 0x85, 0x44, 0x04 };
    static readonly byte[] BootInfoReplyBody = { 0x01, 0x01, 0x04 };

    static List<byte[]> DecodeAll(params byte[][] chunks)
    {
        var decoder = new FrameDecoder();
        var frames = new List<byte[]>();
        foreach (var value in chunks.SelectMany(chunk => chunk))
        {
            if (decoder.TryDecode(value, out var frame))
            {
                frames.Add(frame);
            }
        }

        return frames;
    }

    [TestMethod]
    public void Crc16_CheckString_MatchesXmodem()
    {
        Assert.AreEqual((ushort)0x31C3, Crc16.Compute("123456789"u8));
    }

    [TestMethod]
    public void Crc16_AllByteValues_MatchesXmodem()
    {
        var data = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        Assert.AreEqual((ushort)0x7E55, Crc16.Compute(data));
    }

    [TestMethod]
    public void Encode_ReadBootInfo_EscapesCommandAndChecksum()
    {
        // The command 0x01 and the checksum high byte 0x10 both collide with control bytes
        var expected = new byte[] { 0x01, 0x10, 0x01, 0x21, 0x10, 0x10, 0x04 };
        CollectionAssert.AreEqual(expected, Frame.Encode(0x01, ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void Encode_ChecksumLowByteControl_Escaped()
    {
        var expected = new byte[] { 0x01, 0x10, 0x04, 0x51, 0x10, 0x10, 0x86, 0x04 };
        CollectionAssert.AreEqual(expected, Frame.Encode(0x04, new byte[] { 0x51 }));
    }

    [TestMethod]
    public void Decode_BootInfoReply_ReturnsBodyWithoutChecksum()
    {
        var frames = DecodeAll(BootInfoReplyFrame);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(BootInfoReplyBody, frames[0]);
    }

    [TestMethod]
    public void Decode_StaleBytes_ReturnsBody()
    {
        var frames = DecodeAll(new byte[] { 0x00, 0x10, 0x04, 0xAA }, BootInfoReplyFrame);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(BootInfoReplyBody, frames[0]);
    }

    [TestMethod]
    public void Decode_StartByteInsideFrame_RestartsFrame()
    {
        var frames = DecodeAll(new byte[] { 0x01, 0x55, 0x66 }, BootInfoReplyFrame);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(BootInfoReplyBody, frames[0]);
    }

    [TestMethod]
    public void Decode_InvalidChecksum_DiscardsFrameAndRecovers()
    {
        var corrupted = (byte[])BootInfoReplyFrame.Clone();
        corrupted[corrupted.Length - 2] ^= 0xFF;
        var frames = DecodeAll(corrupted, BootInfoReplyFrame);
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(BootInfoReplyBody, frames[0]);
    }

    [TestMethod]
    public void EncodeDecode_ControlBytesInPayload_RoundTrip()
    {
        var payload = new byte[] { 0x01, 0x04, 0x10, 0x10, 0x00, 0xFF };
        var frames = DecodeAll(Frame.Encode(0x03, payload));
        Assert.HasCount(1, frames);
        CollectionAssert.AreEqual(new byte[] { 0x03 }.Concat(payload).ToArray(), frames[0]);
    }
}
