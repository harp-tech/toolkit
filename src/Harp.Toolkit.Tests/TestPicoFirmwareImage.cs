using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.Pico;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestPicoFirmwareImage
{
    const uint FlagNotMainFlash = 0x00000001;
    const uint FlagFamilyIdPresent = 0x00002000;

    static byte[] Block(uint address, byte[] payload, uint family = FirmwareImage.FamilyRP2040, uint flags = FlagFamilyIdPresent)
    {
        var block = new byte[512];
        var span = block.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, 0x0A324655);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], 0x9E5D5157);
        BinaryPrimitives.WriteUInt32LittleEndian(span[8..], flags);
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], address);
        BinaryPrimitives.WriteUInt32LittleEndian(span[16..], (uint)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..], family);
        payload.CopyTo(span[32..]);
        BinaryPrimitives.WriteUInt32LittleEndian(span[508..], 0x0AB16F30);
        return block;
    }

    static FirmwareImage CreateImage(params byte[][] blocks)
    {
        return FirmwareImage.FromStream(new MemoryStream(blocks.SelectMany(block => block).ToArray()));
    }

    [TestMethod]
    public void GetSectors_BlocksInOneSector_FillsGapsWithErasedValue()
    {
        var image = CreateImage(
            Block(0x10000000, new byte[] { 0x11, 0x22 }),
            Block(0x10000100, new byte[] { 0x33 }));

        var sectors = image.GetSectors(FirmwareImage.FamilyRP2040);

        Assert.HasCount(1, sectors);
        Assert.AreEqual(0x10000000u, sectors[0].Address);
        Assert.HasCount(FirmwareImage.SectorSize, sectors[0].Data);
        Assert.AreEqual(0x22, sectors[0].Data[1]);
        Assert.AreEqual(0xFF, sectors[0].Data[2]);
        Assert.AreEqual(0x33, sectors[0].Data[0x100]);
    }

    [TestMethod]
    public void GetSectors_BlocksOutOfOrder_ReturnsSectorsByAddress()
    {
        var image = CreateImage(
            Block(0x10002000, new byte[] { 0x02 }),
            Block(0x10000000, new byte[] { 0x01 }));

        var sectors = image.GetSectors(FirmwareImage.FamilyRP2040);

        CollectionAssert.AreEqual(new[] { 0x10000000u, 0x10002000u }, sectors.Select(sector => sector.Address).ToArray());
    }

    [TestMethod]
    public void GetSectors_MixedFamilies_ReturnsRequestedFamilyOnly()
    {
        var image = CreateImage(
            Block(0x10FFFF00, new byte[] { 0xEF }, FirmwareImage.FamilyAbsolute),
            Block(0x10000000, new byte[] { 0x01 }, FirmwareImage.FamilyRP2350ArmSecure));

        var sectors = image.GetSectors(FirmwareImage.FamilyRP2350ArmSecure);

        CollectionAssert.AreEqual(new[] { FirmwareImage.FamilyAbsolute, FirmwareImage.FamilyRP2350ArmSecure }, image.Families.ToArray());
        Assert.HasCount(1, sectors);
        Assert.AreEqual(0x10000000u, sectors[0].Address);
    }

    [TestMethod]
    public void FromStream_NotMainFlashBlock_IsIgnored()
    {
        var image = CreateImage(
            Block(0x20000000, new byte[] { 0x01 }, flags: FlagFamilyIdPresent | FlagNotMainFlash),
            Block(0x10000000, new byte[] { 0x02 }));

        Assert.HasCount(1, image.GetSectors(FirmwareImage.FamilyRP2040));
    }

    [TestMethod]
    public void FromStream_IncompleteBlock_Throws()
    {
        var data = Block(0x10000000, new byte[] { 0x01 }).AsSpan(0, 300).ToArray();
        Assert.ThrowsExactly<ArgumentException>(() => FirmwareImage.FromStream(new MemoryStream(data)));
    }

    [TestMethod]
    public void FromStream_BlockOutsideFlash_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateImage(Block(0x20000000, new byte[] { 0x01 })));
    }

    [TestMethod]
    public void GetSectors_OverlappingBlocks_Throws()
    {
        var image = CreateImage(
            Block(0x10000000, new byte[] { 0x01, 0x02 }),
            Block(0x10000001, new byte[] { 0x03 }));

        Assert.ThrowsExactly<ArgumentException>(() => image.GetSectors(FirmwareImage.FamilyRP2040));
    }
}
