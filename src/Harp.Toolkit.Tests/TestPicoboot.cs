using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.Pico;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestPicoboot
{
    // Configuration descriptor of the RP2040 boot ROM, with the mass storage interface first
    // and the PICOBOOT interface second
    static readonly byte[] BootRomConfiguration =
    {
        0x09, 0x02, 0x37, 0x00, 0x02, 0x01, 0x00, 0x80, 0xFA,
        0x09, 0x04, 0x00, 0x00, 0x02, 0x08, 0x06, 0x50, 0x00,
        0x07, 0x05, 0x81, 0x02, 0x40, 0x00, 0x00,
        0x07, 0x05, 0x02, 0x02, 0x40, 0x00, 0x00,
        0x09, 0x04, 0x01, 0x00, 0x02, 0xFF, 0x00, 0x00, 0x00,
        0x07, 0x05, 0x03, 0x02, 0x40, 0x00, 0x00,
        0x07, 0x05, 0x84, 0x02, 0x40, 0x00, 0x00
    };

    [TestMethod]
    public void CreateCommand_ReadRange_MatchesPacketLayout()
    {
        var arguments = new byte[] { 0x00, 0x00, 0x00, 0x10, 0x00, 0x01, 0x00, 0x00 };
        var command = PicobootDevice.CreateCommand(token: 7, commandId: 0x84, transferLength: 256, arguments);

        var expected = new byte[32];
        new byte[] { 0x0B, 0xD1, 0x1F, 0x43, 0x07, 0x00, 0x00, 0x00, 0x84, 0x08, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00 }
            .CopyTo(expected, 0);
        arguments.CopyTo(expected, 16);
        CollectionAssert.AreEqual(expected, command);
    }

    [TestMethod]
    public void TryFindInterface_BootRomConfiguration_FindsVendorInterface()
    {
        Assert.IsTrue(PicobootDevice.TryFindInterface(BootRomConfiguration, out var interfaceNumber, out var outEndpoint, out var inEndpoint));
        Assert.AreEqual(1, interfaceNumber);
        Assert.AreEqual(0x03, outEndpoint);
        Assert.AreEqual(0x84, inEndpoint);
    }

    [TestMethod]
    public void TryFindInterface_MassStorageOnly_ReturnsFalse()
    {
        var configuration = BootRomConfiguration.AsSpan(0, 32).ToArray();
        Assert.IsFalse(PicobootDevice.TryFindInterface(configuration, out _, out _, out _));
    }

    [TestMethod]
    public void GetSerialNumber_BoardIdInUid_FormatsSecondHalfAsHex()
    {
        var uid = new byte[16];
        new byte[] { 0xE6, 0x61, 0x38, 0x52, 0x83, 0x4A, 0x2B, 0x2F }.CopyTo(uid, 8);
        Assert.AreEqual("E6613852834A2B2F", BootRom.GetSerialNumber(uid));
    }

    [TestMethod]
    public void GetSerialNumber_UnimplementedUid_ReturnsNull()
    {
        Assert.IsNull(BootRom.GetSerialNumber(new byte[16]));
    }
}
