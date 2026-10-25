using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware.PIC32;
using static Harp.Toolkit.Tests.IntelHexHelper;

namespace Harp.Toolkit.Tests;

[TestClass]
public class TestPIC32Bootloader
{
    const byte ProgramFlash = 3;

    static FirmwareImage RequireImage(string fileName)
    {
        return FirmwareImage.FromFile(TestAssetHelper.RequireAsset(fileName));
    }

    static async Task UpdateAsync(Bootloader bootloader, FirmwareImage image, IProgress<int>? progress = default)
    {
        await bootloader.EraseFlashAsync();
        await bootloader.ProgramFlashAsync(image, progress);
    }

    [TestMethod]
    public async Task ReadBootInfo_DeviceVersion_ReadsMajorAfterMinor()
    {
        // The device sends the minor version first, so 4.1 arrives as 01 04
        using var device = new PIC32BootloaderEmulator();
        var version = await new Bootloader(device).ReadBootInfoAsync();
        Assert.AreEqual(new Version(PIC32BootloaderEmulator.MajorVersion, PIC32BootloaderEmulator.MinorVersion), version);
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task Update_ReleaseImage_DeviceChecksumMatchesImage(string fileName)
    {
        // A full erase, program and verify against the device CRC, with no flash programmed twice
        var image = RequireImage(fileName);
        using var device = new PIC32BootloaderEmulator();
        var bootloader = new Bootloader(device);
        var percentages = new List<int>();

        await UpdateAsync(bootloader, image, new ImmediateProgress<int>(percentages.Add));

        Assert.IsTrue(await bootloader.VerifyAsync(image));
        Assert.AreEqual(0, device.UnerasedWrites);
        Assert.AreEqual(0, percentages[0]);
        Assert.AreEqual(100, percentages[percentages.Count - 1]);
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task Update_RepliesSplitAfterStaleBytes_DeviceChecksumMatchesImage(string fileName)
    {
        // Serial reads split frames arbitrarily and a port can open mid-stream
        var image = RequireImage(fileName);
        using var device = new PIC32BootloaderEmulator
        {
            ReadChunkSize = 1,
            StaleBytes = new byte[] { 0x55, 0x10, 0x04, 0xAA }
        };
        var bootloader = new Bootloader(device);

        await bootloader.ReadBootInfoAsync();
        await UpdateAsync(bootloader, image);

        Assert.IsTrue(await bootloader.VerifyAsync(image));
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task Verify_ProgramWithoutErase_ReportsMismatch(string fileName)
    {
        var image = RequireImage(fileName);
        using var device = new PIC32BootloaderEmulator();
        var bootloader = new Bootloader(device);

        await bootloader.ProgramFlashAsync(image);

        Assert.IsFalse(await bootloader.VerifyAsync(image));
    }

    [TestMethod]
    public async Task ProgramFlash_32ByteRecords_FramesFitDeviceBuffer()
    {
        // Records made entirely of control bytes double in size when escaped, so the frame
        // limit has to be enforced on the escaped size rather than on the record count
        var data = Enumerable.Repeat((byte)0x10, 32).ToArray();
        var records = new List<string> { LinearAddress(0x1D00) };
        for (int i = 0; i < 64; i++)
        {
            records.Add(Record(Data, (ushort)(i * data.Length), data));
        }

        records.Add(Record(EndOfFile, 0));
        var image = CreateImage(records.ToArray());
        using var device = new PIC32BootloaderEmulator();
        var bootloader = new Bootloader(device);

        await UpdateAsync(bootloader, image);

        Assert.IsTrue(await bootloader.VerifyAsync(image));
        Assert.IsLessThanOrEqualTo(512, device.MaxFrameLength);
        Assert.IsGreaterThan(1, device.GetRequestCount(ProgramFlash));
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task ProgramFlash_SilentDevice_RaisesTimeoutWithoutResending(string fileName)
    {
        // The device stays silent when a record fails, after writing the records before it,
        // so resending the frame would program flash that is no longer erased
        var image = RequireImage(fileName);
        using var device = new PIC32BootloaderEmulator { SilentProgramFrame = 2 };
        var bootloader = new Bootloader(device);

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => UpdateAsync(bootloader, image));

        Assert.AreEqual(3, device.GetRequestCount(ProgramFlash));
        Assert.AreEqual(0, device.UnerasedWrites);
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task ProgramFlash_ResetVectorLastInterrupted_ResetVectorErased(string fileName)
    {
        // The SoundCard bootloader starts the application once its reset address is programmed,
        // so an interrupted update stays recoverable only while it is erased
        const uint ResetAddress = 0x9D001970;
        var image = RequireImage(fileName).WithRecordLast(ResetAddress);
        using var device = new PIC32BootloaderEmulator { SilentProgramFrame = 2 };
        var bootloader = new Bootloader(device);

        await Assert.ThrowsExactlyAsync<TimeoutException>(() => UpdateAsync(bootloader, image));

        var resetVector = device.Flash.Skip((int)(ResetAddress - FirmwareImage.FlashStart)).Take(4).ToArray();
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, resetVector);
    }

    [TestMethod]
    [DataRow(TestAssetHelper.SoundCardImageFw20)]
    [DataRow(TestAssetHelper.SoundCardImageFw22)]
    public async Task Update_ResetVectorLast_DeviceChecksumMatchesImage(string fileName)
    {
        var image = RequireImage(fileName).WithRecordLast(0x9D001970);
        using var device = new PIC32BootloaderEmulator();
        var bootloader = new Bootloader(device);

        await UpdateAsync(bootloader, image);

        Assert.IsTrue(await bootloader.VerifyAsync(image));
        Assert.AreEqual(0, device.UnerasedWrites);
    }

    [TestMethod]
    public async Task JumpToApplication_NoReply_Completes()
    {
        using var device = new PIC32BootloaderEmulator();
        await new Bootloader(device).JumpToApplicationAsync();
        Assert.IsTrue(device.StartedApplication);
    }
}
