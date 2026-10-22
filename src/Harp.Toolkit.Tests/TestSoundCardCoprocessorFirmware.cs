using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.ATxmega;
using Harp.Toolkit.Firmware.SoundCard;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestSoundCardCoprocessorFirmware
{
    const string ReleaseName = "SoundCard-fw2.2-harp1.13-hw2.2-ass0";

    [TestMethod]
    [DataRow("SoundCard.PIC32-fw2.2-harp1.13-hw2.2-ass0")]
    [DataRow("SoundCard.X-fw2.2-harp1.13-hw2.2-ass0")]
    public void TryParseMetadata_ProcessorSuffix_MatchesReleaseMetadata(string name)
    {
        Assert.IsTrue(CoprocessorFirmware.TryParseMetadata(name, out var metadata));
        Assert.AreEqual(FirmwareMetadata.Parse(ReleaseName), metadata);
    }

    [TestMethod]
    public void TryParseMetadata_NoProcessorSuffix_ReturnsFalse()
    {
        Assert.IsFalse(CoprocessorFirmware.TryParseMetadata(ReleaseName, out _));
    }

    [TestMethod]
    public void GetPathsFor_ReleaseImage_PrefersPIC32SuffixInSameFolder()
    {
        var folder = Path.Combine("firmware", "release");
        var paths = CoprocessorFirmware.GetPathsFor(Path.Combine(folder, ReleaseName + ".hex"));
        CollectionAssert.AreEqual(
            new[]
            {
                Path.Combine(folder, "SoundCard.PIC32-fw2.2-harp1.13-hw2.2-ass0.hex"),
                Path.Combine(folder, "SoundCard.X-fw2.2-harp1.13-hw2.2-ass0.hex")
            },
            paths);
    }

    [TestMethod]
    public void GetPathsFor_NoDeviceName_ReturnsEmpty()
    {
        Assert.IsEmpty(CoprocessorFirmware.GetPathsFor("firmware.hex"));
    }

    [TestMethod]
    public void FirmwareMetadata_ProcessorSuffix_Rejected()
    {
        // The ATxmega update must never accept a coprocessor image
        Assert.IsFalse(FirmwareMetadata.TryParse("SoundCard.PIC32-fw2.2-harp1.13-hw2.2-ass0", out _));
        Assert.IsFalse(FirmwareMetadata.TryParse("SoundCard.X-fw2.2-harp1.13-hw2.2-ass0", out _));
    }
}
