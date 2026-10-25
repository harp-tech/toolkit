using System.IO.Ports;
using Bonsai.Harp;

namespace Harp.Toolkit.Firmware.SoundCard;

/// <summary>
/// Provides asynchronous operations to update the firmware of the PIC32 coprocessor of the SoundCard.
/// </summary>
/// <remarks>
/// Writing 1 to the SoundCard command register switches the serial port from the ATxmega to the
/// PIC32 bootloader, and writing 0 switches it back. The bootloader restarts the coprocessor on
/// its own once the register is cleared. The device does not answer Harp commands in between.
/// </remarks>
public static class Coprocessor
{
    /// <summary>
    /// The identity class of the SoundCard.
    /// </summary>
    public const int WhoAmI = 1280;

    /// <summary>
    /// The device name carried by SoundCard firmware images.
    /// </summary>
    public const string DeviceName = "SoundCard";

    const int CommandAddress = 82;
    const byte DisableBootloader = 0;
    const byte EnableBootloader = 1;
    const uint ApplicationResetAddress = 0x9D001970;

    const int EnableReplyMilliseconds = 200;
    const int PortOpenAttempts = 3;
    const int PortRetryDelayMilliseconds = 500;
    const int RestartMilliseconds = 2000;

    /// <summary>
    /// Asynchronously updates the coprocessor firmware of the SoundCard on the specified port.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the SoundCard.</param>
    /// <param name="firmware">The coprocessor firmware image to program.</param>
    /// <param name="forceUpdate">
    /// <b>true</b> to program the image without checking that it supports the device;
    /// <b>false</b> to throw an exception if the device does not answer or the image does not
    /// support it.
    /// </param>
    /// <param name="timeout">The time to wait, in milliseconds, for the device to answer a Harp command.</param>
    /// <param name="progress">The optional object that receives update progress reports.</param>
    /// <param name="cancellationToken">The token that cancels the update.</param>
    /// <returns>The task object representing the asynchronous firmware update operation.</returns>
    /// <remarks>
    /// The update accepts cancellation only before it switches the port to the coprocessor, and
    /// ignores the token from the <see cref="UpdateStage.Reset"/> stage onwards. If the update
    /// fails after that stage, the port stays switched to the coprocessor bootloader and the
    /// device does not answer Harp commands until it is power cycled or a forced update completes.
    /// The device does not report when the new coprocessor firmware is running, so the update
    /// ends after a fixed delay.
    /// </remarks>
    public static async Task UpdateFirmwareAsync(
        string portName,
        CoprocessorFirmware firmware,
        bool forceUpdate,
        int timeout,
        IProgress<UpdateProgress>? progress = default,
        CancellationToken cancellationToken = default)
    {
        if (firmware == null) throw new ArgumentNullException(nameof(firmware));
        var image = firmware.Image.WithRecordLast(ApplicationResetAddress);

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new(UpdateStage.Connect, 0));
        using (var device = new AsyncDevice(portName))
        {
            progress?.Report(new(UpdateStage.Check, 10));
            if (!forceUpdate)
            {
                var whoAmI = await device.ReadWhoAmIAsync(cancellationToken).WithTimeout(timeout);
                if (whoAmI != WhoAmI)
                {
                    throw new HarpException(
                        $"The coprocessor firmware is for the {DeviceName} with WhoAmI {WhoAmI}, but the " +
                        $"device on this port has WhoAmI {whoAmI}.");
                }

                var hardwareVersion = await device.ReadHardwareVersionAsync(cancellationToken).WithTimeout(timeout);
                var deviceName = await device.ReadDeviceNameAsync(cancellationToken).WithTimeout(timeout);
                if (!firmware.Metadata.Supports(deviceName, hardwareVersion))
                {
                    throw new HarpException(
                        $"The firmware file is for {firmware.Metadata.DeviceName} with hardware version " +
                        $"{firmware.Metadata.HardwareVersion}, but the device on this port is {deviceName} " +
                        $"with hardware version {hardwareVersion}.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new(UpdateStage.Reset, 20));
            using var replyCancellation = new CancellationTokenSource(EnableReplyMilliseconds);
            try
            {
                await device.WriteByteAsync(CommandAddress, EnableBootloader, replyCancellation.Token);
            }
            catch (OperationCanceledException) when (replyCancellation.IsCancellationRequested)
            {
            }
        }

        progress?.Report(new(UpdateStage.Bootloader, 30));
        using (var port = await OpenAsync(() => OpenBootloaderPort(portName)))
        {
            var bootloader = new PIC32.Bootloader(port.BaseStream);
            await bootloader.ReadBootInfoAsync();

            progress?.Report(new(UpdateStage.Write, 40));
            await bootloader.EraseFlashAsync();
            var programProgress = new ImmediateProgress<int>(
                percent => progress?.Report(new(UpdateStage.Write, 40 + percent * 45 / 100)));
            await bootloader.ProgramFlashAsync(image, programProgress);
            if (!await bootloader.VerifyAsync(image))
            {
                throw new HarpException("The coprocessor flash does not match the firmware image after writing.");
            }
        }

        progress?.Report(new(UpdateStage.Restart, 90));
        using (var device = await OpenAsync(() => new AsyncDevice(portName)))
        {
            using var replyCancellation = new CancellationTokenSource(timeout);
            try
            {
                await device.WriteByteAsync(CommandAddress, DisableBootloader, replyCancellation.Token);
            }
            catch (OperationCanceledException) when (replyCancellation.IsCancellationRequested)
            {
                throw new TimeoutException("There was a timeout while awaiting the device response.");
            }
        }

        await Task.Delay(RestartMilliseconds);
        progress?.Report(new(UpdateStage.Restart, 100));
    }

    static SerialPort OpenBootloaderPort(string portName)
    {
        var port = new SerialPort(portName, PIC32.Bootloader.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            RtsEnable = true
        };

        try
        {
            port.Open();
            return port;
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    static async Task<T> OpenAsync<T>(Func<T> open)
    {
        for (int i = 1; ; i++)
        {
            try
            {
                return open();
            }
            catch (Exception ex) when (i < PortOpenAttempts && ex is UnauthorizedAccessException or IOException)
            {
                await Task.Delay(PortRetryDelayMilliseconds);
            }
        }
    }
}
