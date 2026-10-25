using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.PIC32;
using static Harp.Toolkit.Tests.IntelHexHelper;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestPIC32FirmwareImage
{
    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20, 4062, 0xE858u, (ushort)0xBA15)]
    [DataRow(TestAssetHelper.SoundCardImageFw22, 4312, 0xF838u, (ushort)0x3A2C)]
    public void FromFile_ReleaseImage_MatchesReferenceChecksum(string fileName, int recordCount, uint length, ushort crc)
    {
        // Reference values computed independently with Python binascii.crc_hqx
        var image = FirmwareImage.FromFile(TestAssetHelper.RequireAsset(fileName));
        Assert.HasCount(recordCount, image.Records);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x00, 0x00, 0x01, 0xFF }, image.Records[image.Records.Count - 1].ToArray());
        Assert.AreEqual(0x9D001000u, image.StartAddress);
        Assert.AreEqual(length, image.Length);
        Assert.AreEqual(crc, image.Crc);
    }

    [TestMethod]
    public void FromStream_BootFlashRecord_ExcludedFromChecksumRange()
    {
        var image = CreateImage(
            LinearAddress(0x1D00),
            Record(Data, 0x0000, 0x11, 0x22, 0x33, 0x44),
            LinearAddress(0x1FC0),
            Record(Data, 0x0000, 0x55, 0x66, 0x77, 0x88),
            Record(EndOfFile, 0));
        Assert.HasCount(5, image.Records);
        Assert.AreEqual(0x9D000000u, image.StartAddress);
        Assert.AreEqual(4u, image.Length);
        Assert.AreEqual(Crc16.Compute(new byte[] { 0x11, 0x22, 0x33, 0x44 }), image.Crc);
    }

    [TestMethod]
    public void FromStream_GapBetweenRecords_ChecksumReadsErasedFlash()
    {
        var image = CreateImage(
            LinearAddress(0x1D00),
            Record(Data, 0x0000, 0x11),
            Record(Data, 0x0003, 0x44),
            Record(EndOfFile, 0));
        Assert.AreEqual(4u, image.Length);
        Assert.AreEqual(Crc16.Compute(new byte[] { 0x11, 0xFF, 0xFF, 0x44 }), image.Crc);
    }

    [TestMethod]
    public void WithRecordLast_AddressInRecord_MovesRecordBeforeEndOfFile()
    {
        var image = CreateImage(
            LinearAddress(0x1D00),
            Record(Data, 0x0000, 0x11),
            Record(Data, 0x0010, 0x22),
            Record(Data, 0x0020, 0x33),
            Record(EndOfFile, 0));
        var expected = CreateImage(
            LinearAddress(0x1D00),
            Record(Data, 0x0000, 0x11),
            Record(Data, 0x0020, 0x33),
            LinearAddress(0x1D00),
            Record(Data, 0x0010, 0x22),
            Record(EndOfFile, 0));

        var reordered = image.WithRecordLast(0x9D000010);

        Assert.HasCount(expected.Records.Count, reordered.Records);
        for (int i = 0; i < expected.Records.Count; i++)
        {
            CollectionAssert.AreEqual(expected.Records[i].ToArray(), reordered.Records[i].ToArray());
        }

        Assert.AreEqual(image.StartAddress, reordered.StartAddress);
        Assert.AreEqual(image.Length, reordered.Length);
        Assert.AreEqual(image.Crc, reordered.Crc);
    }

    [TestMethod]
    public void WithRecordLast_AddressInNoRecord_ReturnsSameImage()
    {
        var image = CreateImage(LinearAddress(0x1D00), Record(Data, 0x0000, 0x11), Record(EndOfFile, 0));
        Assert.AreSame(image, image.WithRecordLast(0x9D000010));
    }

    [TestMethod]
    public void FromStream_InvalidRecordChecksum_RaisesArgumentException()
    {
        var record = Record(Data, 0x0000, 0x11);
        var corrupted = record.Substring(0, record.Length - 2) + "00";
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateImage(LinearAddress(0x1D00), corrupted, Record(EndOfFile, 0)));
    }

    [TestMethod]
    public void FromStream_MissingEndOfFile_RaisesArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateImage(LinearAddress(0x1D00), Record(Data, 0x0000, 0x11)));
    }

    [TestMethod]
    public void FromStream_NoApplicationFlashData_RaisesArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CreateImage(LinearAddress(0x1FC0), Record(Data, 0x0000, 0x11), Record(EndOfFile, 0)));
    }
}
