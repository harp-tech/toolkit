using System.Diagnostics;
using System.IO.Ports;
using Bonsai.Harp;
using Harp.Toolkit.Usb;

namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// Provides asynchronous operations to update the firmware of a device built on the Pico core,
/// through the PICOBOOT interface of the RP2040 and RP2350 boot ROM.
/// </summary>
/// <remarks>
/// Writing the <c>UPDATE_FIRMWARE</c> bit of <c>R_RESET_DEV</c> reboots the device into BOOTSEL
/// mode, with or without a reply first. The serial port then disappears, and the device reappears as a USB
/// device with a mass storage drive and the PICOBOOT interface, which this class uses to write
/// and read back the image. The update needs the libusb 1.0 library. On Windows, the RP2040 boot
/// interface also needs a WinUSB driver.
/// </remarks>
public static class BootRom
{
    const int UidAddress = 16;
    const int UidLength = 16;
    const int BoardIdOffset = 8;
    const byte UpdateFirmware = 0x20;

    const int ResetReplyMilliseconds = 200;
    const int BootselTimeoutMilliseconds = 10000;
    const int BootselPollMilliseconds = 250;
    const int DisconnectTimeoutMilliseconds = 2000;
    const int VerifyChunkSize = FirmwareImage.SectorSize;

    /// <summary>
    /// Asynchronously updates the firmware of the Pico device on the specified port.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the Harp device.</param>
    /// <param name="firmware">The firmware image to write to the device.</param>
    /// <param name="forceUpdate">
    /// <see langword="true"/> to write the image even if the device reports unsupported hardware, or is already
    /// in BOOTSEL mode and has no serial port; <see langword="false"/> to throw an exception if the image does
    /// not support the device or the device does not answer.
    /// </param>
    /// <param name="timeout">The time to wait, in milliseconds, for the device to answer a Harp command.</param>
    /// <param name="progress">The optional object that receives update progress reports.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is
    /// <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>The task object representing the asynchronous firmware update operation.</returns>
    /// <remarks>
    /// The update accepts cancellation only before it resets the device, and ignores the token from
    /// the <see cref="UpdateStage.Reset"/> stage onwards. A device in BOOTSEL mode has no Harp
    /// identity. After a reset, the update considers only a device that was not already in BOOTSEL
    /// mode before the reset. Without a reset, it accepts a single device in BOOTSEL mode. When
    /// several devices remain and none matches the USB serial number of the device on the port,
    /// the update throws rather than guess. When the image targets a different microcontroller,
    /// the update reboots the device with its previous firmware before throwing. Any other failure
    /// after the reset leaves the device in BOOTSEL mode.
    /// </remarks>
    /// <exception cref="DllNotFoundException">The libusb library is not installed.</exception>
    /// <exception cref="BootselAccessException">
    /// The boot interface of the device in BOOTSEL mode cannot be opened, because a driver or a
    /// permission is missing.
    /// </exception>
    /// <exception cref="UnsupportedImageException">
    /// The firmware image does not target the microcontroller of the device.
    /// </exception>
    /// <exception cref="BootselRequestIgnoredException">
    /// The device kept running its firmware after the request to restart in BOOTSEL mode.
    /// </exception>
    /// <exception cref="AmbiguousBootselDeviceException">
    /// Several devices are in BOOTSEL mode and none can be matched to the device on the port.
    /// </exception>
    public static async Task UpdateFirmwareAsync(
        string portName,
        PicoFirmware firmware,
        bool forceUpdate,
        int timeout,
        IProgress<UpdateProgress>? progress = default,
        CancellationToken cancellationToken = default)
    {
        if (firmware == null) throw new ArgumentNullException(nameof(firmware));

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(UpdateStage.Connect, 0));
        using var context = CreateContext();
        var waitingDevices = GetBootselLocations(context);
        var request = default(BootselRequest);
        HashSet<string>? excludedDevices = null;
        try
        {
            request = await RequestBootselAsync(portName, firmware, forceUpdate, timeout, progress, cancellationToken);
            excludedDevices = waitingDevices;
        }
        catch (Exception ex) when (forceUpdate && ex is TimeoutException or IOException)
        {
            if (waitingDevices.Count == 0)
            {
                throw;
            }
        }

        progress?.Report(new(UpdateStage.Bootloader, 30));
        using var picoboot = await OpenBootselDeviceAsync(context, portName, request, excludedDevices);
        var family = SelectFamily(firmware.Image, picoboot.Model);
        if (family is null)
        {
            try { picoboot.RebootToApplication(); } catch (Exception ex) when (ex is IOException or HarpException) { }
            throw new UnsupportedImageException(
                $"The firmware image does not target the {picoboot.Model} microcontroller of the device. " +
                "The device was restarted with its previous firmware.");
        }

        var sectors = firmware.Image.GetSectors(family.GetValueOrDefault());
        progress?.Report(new(UpdateStage.Write, 40));
        await Task.Run(() => WriteAndVerify(picoboot, sectors, progress));

        progress?.Report(new(UpdateStage.Restart, 90));
        await Task.Run(picoboot.RebootToApplication);
        progress?.Report(new(UpdateStage.Restart, 100));
    }

    /// <summary>
    /// Returns the number of devices in BOOTSEL mode attached to the system.
    /// </summary>
    /// <returns>
    /// The number of RP2040 and RP2350 boot ROMs attached, or zero if the libusb library is not
    /// installed.
    /// </returns>
    public static int GetBootselDeviceCount()
    {
        try
        {
            using var context = UsbContext.Create();
            return CountBootselDevices(context);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or UsbException)
        {
            return 0;
        }
    }

    static UsbContext CreateContext()
    {
        try
        {
            return UsbContext.Create();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new DllNotFoundException(
                "Updating a Pico device needs the libusb 1.0 library, which was not found on this system.", ex);
        }
    }

    /// <summary>
    /// Represents the outcome of a request to restart in BOOTSEL mode.
    /// </summary>
    /// <param name="SerialNumber">
    /// The USB serial number expected of the device, or <see langword="null"/> if unknown.
    /// </param>
    /// <param name="Replied">
    /// <see langword="true"/> if the device replied to the request; <see langword="false"/> if it
    /// disconnected or stayed silent.
    /// </param>
    readonly record struct BootselRequest(string? SerialNumber, bool Replied);

    static async Task<BootselRequest> RequestBootselAsync(
        string portName,
        PicoFirmware firmware,
        bool forceUpdate,
        int timeout,
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        var device = new AsyncDevice(portName);
        try
        {
            progress?.Report(new(UpdateStage.Check, 10));
            if (!forceUpdate)
            {
                var identity = await DeviceIdentity.ReadAsync(device, timeout, cancellationToken);
                if (!firmware.Metadata.Supports(identity.DeviceName, identity.HardwareVersion))
                {
                    throw new HarpException(
                        $"The firmware file is for {firmware.Metadata.DeviceName} with hardware version " +
                        $"{firmware.Metadata.HardwareVersion}, but the device on this port is {identity.DeviceName} " +
                        $"with hardware version {identity.HardwareVersion}.");
                }
            }

            var serialNumber = await TryReadSerialNumberAsync(device, timeout);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(UpdateStage.Reset, 20));
            using var replyCancellation = new CancellationTokenSource(ResetReplyMilliseconds);
            try
            {
                await device.WriteByteAsync(ResetDevice.Address, UpdateFirmware, replyCancellation.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or InvalidOperationException)
            {
                return new BootselRequest(serialNumber, Replied: false);
            }

            return new BootselRequest(serialNumber, Replied: true);
        }
        finally
        {
            try { device.Dispose(); } catch (Exception ex) when (ex is IOException or InvalidOperationException) { }
        }
    }

    static async Task<string?> TryReadSerialNumberAsync(AsyncDevice device, int timeout)
    {
        try
        {
            var uid = await device.ReadByteArrayAsync(UidAddress).WithTimeout(timeout);
            return GetSerialNumber(uid);
        }
        catch (Exception ex) when (ex is HarpException or TimeoutException)
        {
            return null;
        }
    }

    internal static string? GetSerialNumber(byte[] uid)
    {
        if (uid.Length != UidLength)
        {
            return null;
        }

        var boardId = uid.AsSpan(BoardIdOffset);
        return boardId.IndexOfAnyExcept((byte)0) < 0 ? null : Convert.ToHexString(boardId);
    }

    static int CountBootselDevices(UsbContext context)
    {
        return GetBootselLocations(context).Count;
    }

    /// <summary>
    /// Returns the location of every device in BOOTSEL mode attached to the system.
    /// </summary>
    internal static HashSet<string> GetBootselLocations()
    {
        using var context = UsbContext.Create();
        return GetBootselLocations(context);
    }

    static HashSet<string> GetBootselLocations(UsbContext context)
    {
        var devices = context.GetDevices();
        try
        {
            return devices
                .Where(device => PicobootDevice.TryGetModel(device, out _))
                .Select(device => device.GetLocation())
                .ToHashSet();
        }
        finally
        {
            foreach (var device in devices) device.Dispose();
        }
    }

    static async Task<PicobootDevice> OpenBootselDeviceAsync(
        UsbContext context,
        string portName,
        BootselRequest request,
        HashSet<string>? excludedDevices)
    {
        var stopwatch = Stopwatch.StartNew();
        OpenFailureException? lastFailure = null;
        while (true)
        {
            try
            {
                var picoboot = TryOpenBootselDevice(context, portName, request.SerialNumber, excludedDevices);
                if (picoboot is not null)
                {
                    return picoboot;
                }
            }
            catch (OpenFailureException failure)
            {
                lastFailure = failure;
            }

            if (request.Replied && stopwatch.ElapsedMilliseconds > DisconnectTimeoutMilliseconds && IsPortListed(portName))
            {
                throw new BootselRequestIgnoredException(
                    "The device kept running after the request to restart in BOOTSEL mode, so its firmware " +
                    "does not implement UPDATE_FIRMWARE.");
            }

            if (stopwatch.ElapsedMilliseconds > BootselTimeoutMilliseconds)
            {
                if (lastFailure is not null)
                {
                    throw DescribeOpenFailure((UsbException)lastFailure.InnerException!, lastFailure.Model);
                }

                throw new TimeoutException("No device appeared in BOOTSEL mode in time.");
            }

            await Task.Delay(BootselPollMilliseconds);
        }
    }

    static bool IsPortListed(string portName)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Array.Exists(SerialPort.GetPortNames(), name => string.Equals(name, portName, comparison));
    }

    static PicobootDevice? TryOpenBootselDevice(
        UsbContext context,
        string portName,
        string? serialNumber,
        HashSet<string>? excludedDevices)
    {
        var devices = context.GetDevices();
        try
        {
            var candidates = new List<(UsbDevice Device, PicoModel Model)>();
            foreach (var device in devices)
            {
                if (PicobootDevice.TryGetModel(device, out var model) &&
                    (excludedDevices is null || !excludedDevices.Contains(device.GetLocation())))
                {
                    candidates.Add((device, model));
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return OpenCandidate(candidates[0].Device, candidates[0].Model);
            }

            if (serialNumber is not null)
            {
                foreach (var (device, model) in candidates)
                {
                    PicobootDevice picoboot;
                    try
                    {
                        picoboot = PicobootDevice.Open(device, model);
                    }
                    catch (UsbException)
                    {
                        continue;
                    }

                    if (string.Equals(picoboot.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase))
                    {
                        return picoboot;
                    }

                    picoboot.Dispose();
                }
            }

            throw new AmbiguousBootselDeviceException(
                $"Several devices are in BOOTSEL mode and none could be matched to the device on {portName}, " +
                "so the update stopped without writing to any of them. Disconnect the other devices and update again.");
        }
        finally
        {
            foreach (var device in devices) device.Dispose();
        }
    }

    static PicobootDevice OpenCandidate(UsbDevice device, PicoModel model)
    {
        try
        {
            return PicobootDevice.Open(device, model);
        }
        catch (UsbException ex)
        {
            throw new OpenFailureException(ex, model);
        }
    }

    /// <summary>
    /// The exception that is thrown when a device found in BOOTSEL mode cannot be opened.
    /// </summary>
    sealed class OpenFailureException : Exception
    {
        public OpenFailureException(UsbException innerException, PicoModel model)
            : base(innerException.Message, innerException)
        {
            Model = model;
        }

        public PicoModel Model { get; }
    }

    static Exception DescribeOpenFailure(UsbException exception, PicoModel model)
    {
        if (OperatingSystem.IsWindows() &&
            (exception.ErrorCode == LibUsb.ErrorNotSupported || exception.ErrorCode == LibUsb.ErrorNotFound))
        {
            return new BootselAccessException(
                $"The {model} boot interface of the device in BOOTSEL mode has no WinUSB driver.", exception);
        }

        if (OperatingSystem.IsLinux() && exception.ErrorCode == LibUsb.ErrorAccess)
        {
            return new BootselAccessException(
                "Access to the device in BOOTSEL mode was denied. A udev rule is needed to open it without root access.",
                exception);
        }

        return exception;
    }

    static uint? SelectFamily(FirmwareImage image, PicoModel model)
    {
        var supported = model == PicoModel.RP2040
            ? new[] { FirmwareImage.FamilyRP2040 }
            : new[] { FirmwareImage.FamilyRP2350ArmSecure, FirmwareImage.FamilyRP2350RiscV, FirmwareImage.FamilyRP2350ArmNonSecure };
        foreach (var family in supported)
        {
            if (image.Families.Contains(family))
            {
                return family;
            }
        }

        return null;
    }

    static void WriteAndVerify(PicobootDevice picoboot, IReadOnlyList<FlashSector> sectors, IProgress<UpdateProgress>? progress)
    {
        picoboot.TakeExclusiveAccess();
        picoboot.PrepareFlashWrite();
        for (int i = 0; i < sectors.Count; i++)
        {
            var sector = sectors[i];
            picoboot.EraseFlash(sector.Address, (uint)sector.Data.Length);
            picoboot.WriteMemory(sector.Address, sector.Data);
            progress?.Report(new(UpdateStage.Write, 40 + (i + 1) * 40 / sectors.Count));
        }

        picoboot.PrepareFlashRead();
        for (int i = 0; i < sectors.Count; i++)
        {
            var sector = sectors[i];
            for (int offset = 0; offset < sector.Data.Length; offset += VerifyChunkSize)
            {
                var size = Math.Min(VerifyChunkSize, sector.Data.Length - offset);
                var readBack = picoboot.ReadMemory(sector.Address + (uint)offset, size);
                if (!readBack.AsSpan().SequenceEqual(sector.Data.AsSpan(offset, size)))
                {
                    throw new HarpException(
                        $"The device memory at 0x{sector.Address + (uint)offset:X8} does not match the firmware image after writing.");
                }
            }

            progress?.Report(new(UpdateStage.Write, 80 + (i + 1) * 10 / sectors.Count));
        }
    }
}
