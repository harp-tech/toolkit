using Microsoft.VisualStudio.TestTools.UnitTesting;
using Harp.Toolkit.Firmware;
using Harp.Toolkit.Firmware.Pico;

namespace Harp.Toolkit.Tests;

/// <summary>
/// Updates the firmware of a physical Pico device while other devices are in BOOTSEL mode, to
/// check that the update never writes to any device other than the one it reset.
/// </summary>
/// <remarks>
/// A device in BOOTSEL mode reports no Harp identity, and every RP2040 boot ROM reports the same
/// USB serial number, so the choice of device can only be checked against real devices. Each test
/// states the arrangement of boards it needs and skips itself when that arrangement is missing.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class TestPicoFirmwareUpdate
{
    /// <summary>
    /// The time allowed for the device to start responding again after an update.
    /// </summary>
    const int ReadyTimeoutMilliseconds = 15000;

    /// <summary>
    /// The time allowed for the device to answer a Harp command during an update, held independent
    /// of the default carried by the tool.
    /// </summary>
    const int ResponseTimeoutMilliseconds = 2000;

    public TestContext TestContext { get; set; } = null!;

    static PicoFirmware RequirePicoFirmware()
    {
        var path = HardwareTestHelper.RequireFirmware();
        var isUf2 = string.Equals(Path.GetExtension(path), ".uf2", StringComparison.OrdinalIgnoreCase);
        if (!FirmwareMetadata.TryParse(Path.GetFileNameWithoutExtension(path), out var metadata) || !isUf2)
        {
            Assert.Inconclusive(
                $"The firmware image '{path}' is not a UF2 image named after the Harp firmware convention.");
        }

        return PicoFirmware.FromFile(metadata, path);
    }

    static HashSet<string> RequireDevicesInBootsel(int count)
    {
        var waiting = BootRom.GetBootselLocations();
        if (waiting.Count < count)
        {
            Assert.Inconclusive(
                $"This test needs {count} other Pico {(count == 1 ? "device" : "devices")} in BOOTSEL mode, " +
                $"but {waiting.Count} were found. " +
                "Hold the BOOTSEL button while connecting each one.");
        }

        return waiting;
    }

    [TestMethod]
    [TestCategory(HardwareTestHelper.Category)]
    public async Task UpdateFirmware_DeviceWaitingInBootsel_IsNotWritten()
    {
        // An update must only write to the device it reset, even when another device is already
        // waiting in BOOTSEL mode and the reset device has not yet appeared on the bus.
        var portName = HardwareTestHelper.RequirePort();
        var firmware = RequirePicoFirmware();
        var iterations = HardwareTestHelper.GetTestIterations(3);
        HardwareTestHelper.RequireResetConsent();
        HardwareTestHelper.RequireLibUsb();
        var waiting = RequireDevicesInBootsel(1);
        if (!(await HardwareTestHelper.MeasureTimeUntilReadyAsync(portName, ReadyTimeoutMilliseconds)).HasValue)
        {
            Assert.Inconclusive("The device under test did not respond before the run started.");
        }

        TestContext.WriteLine("devices waiting in BOOTSEL mode: {0}", string.Join(", ", waiting));
        for (int i = 0; i < iterations; i++)
        {
            await BootRom.UpdateFirmwareAsync(portName, firmware, forceUpdate: false, ResponseTimeoutMilliseconds);

            var ready = await HardwareTestHelper.MeasureTimeUntilReadyAsync(portName, ReadyTimeoutMilliseconds);
            var stillWaiting = BootRom.GetBootselLocations();
            TestContext.WriteLine(
                "iteration {0}: responding again after {1}, devices in BOOTSEL mode: {2}",
                i,
                ready.HasValue ? $"{ready.GetValueOrDefault():F0}ms" : "no response",
                string.Join(", ", stillWaiting));

            Assert.IsTrue(
                waiting.IsSubsetOf(stillWaiting),
                "A device that was waiting in BOOTSEL mode left it, so the update may have written to it.");
            Assert.IsTrue(ready.HasValue, "The updated device did not respond again on its port.");
        }
    }

    [TestMethod]
    [TestCategory(HardwareTestHelper.Category)]
    public async Task UpdateFirmware_ForcedWithSeveralDevicesInBootsel_RaisesAmbiguousDevice()
    {
        // A forced update of a device that is already in BOOTSEL mode cannot tell it apart from
        // other devices in BOOTSEL mode, so it must refuse rather than choose one.
        var portName = HardwareTestHelper.RequirePort();
        var firmware = RequirePicoFirmware();
        HardwareTestHelper.RequireResetConsent();
        HardwareTestHelper.RequireLibUsb();
        var waiting = RequireDevicesInBootsel(2);
        if (await HardwareTestHelper.CheckDeviceResponseAsync(portName) is null)
        {
            Assert.Inconclusive(
                "The device on the configured port responds to Harp. Put it in BOOTSEL mode as well, so the port disappears.");
        }

        await Assert.ThrowsExactlyAsync<AmbiguousBootselDeviceException>(() => BootRom.UpdateFirmwareAsync(
            portName, firmware, forceUpdate: true, ResponseTimeoutMilliseconds));

        CollectionAssert.AreEquivalent(
            waiting.ToArray(),
            BootRom.GetBootselLocations().ToArray(),
            "A device left BOOTSEL mode, so the refused update may have written to it.");
    }
}
