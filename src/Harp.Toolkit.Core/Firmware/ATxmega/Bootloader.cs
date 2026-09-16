using System.IO.Ports;
using Bonsai.Harp;

namespace Harp.Toolkit.Firmware.ATxmega;

/// <summary>
/// Provides asynchronous operations to update a device firmware using the dedicated bootloader protocol.
/// </summary>
public static class Bootloader
{
    const int HeaderSize = 15;
    const int DefaultBaudRate = 1000000;

    const int FlushDelayMilliseconds = 500;
    const int BootloaderTimeoutMilliseconds = 500;

    const int WritePage = 0x0;
    const int ReadPageSize = 0x66;
    const int ExitBootloader = 0x77;

    const int MinPageSize = 64;
    const int MaxPageSize = 4096;

    const int NoError = 0;
    const int UndefinedError = 1;
    const int InvalidAddress = 2;
    const int InvalidDataLength = 3;

    /// <summary>
    /// Asynchronously updates the firmware of the Harp device on the specified port.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the Harp device.</param>
    /// <param name="firmware">The binary firmware image to upload to the device.</param>
    /// <param name="forceUpdate">
    /// <b>true</b> to indicate that the firmware should be uploaded even if the device reports unsupported hardware,
    /// or is in bootloader mode; <b>false</b> to throw an exception if the firmware is not supported, or the device
    /// is in an invalid state.
    /// </param>
    /// <param name="timeout">The time to wait, in milliseconds, for the device to answer a Harp command.</param>
    /// <param name="progress">The optional object that receives update progress reports.</param>
    /// <returns>
    /// The task object representing the asynchronous firmware update operation.
    /// </returns>
    public static async Task UpdateFirmwareAsync(
        string portName,
        DeviceFirmware firmware,
        bool forceUpdate,
        int timeout,
        IProgress<UpdateProgress>? progress = default)
    {
        progress?.Report(new(UpdateStage.Connect, 0));
        try
        {
            using (var device = new AsyncDevice(portName))
            {
                progress?.Report(new(UpdateStage.Check, 10));
                if (!forceUpdate)
                {
                    var hardwareVersion = await device.ReadHardwareVersionAsync().WithTimeout(timeout);
                    var deviceName = await device.ReadDeviceNameAsync().WithTimeout(timeout);
                    if (!firmware.Metadata.Supports(deviceName, hardwareVersion))
                    {
                        throw new HarpException(
                            $"The firmware file is for {firmware.Metadata.DeviceName} with hardware version " +
                            $"{firmware.Metadata.HardwareVersion}, but the device on this port is {deviceName} " +
                            $"with hardware version {hardwareVersion}.");
                    }
                }

                progress?.Report(new(UpdateStage.Reset, 20));
                var reset = await device.ReadResetDeviceAsync().WithTimeout(timeout);
                if ((reset & ResetFlags.BootFromEeprom) != 0)
                {
                    await device.WriteResetDeviceAsync(ResetFlags.RestoreEeprom);
                }
                else if ((reset & ResetFlags.BootFromDefault) != 0)
                {
                    await device.WriteResetDeviceAsync(ResetFlags.RestoreDefault);
                }
                else throw new HarpException("The device is in an unexpected boot mode.");
            }
        }
        catch (Exception ex) when (ex is TimeoutException || ex is IOException)
        {
            if (!forceUpdate)
            {
                throw;
            }
        }

        await Task.Delay(FlushDelayMilliseconds);

        const int MaxAttempts = 3;
        for (int i = 1; i <= MaxAttempts; i++)
        {
            try
            {
                progress?.Report(new(UpdateStage.Bootloader, 30));
                using (var bootloader = new SerialPort(portName, DefaultBaudRate, Parity.None, 8, StopBits.One))
                {
                    bootloader.Handshake = Handshake.None;
                    bootloader.Open();
                    await Task.Delay(FlushDelayMilliseconds);
                    var pageSize = await ReadPageSizeAsync(bootloader.BaseStream);
                    progress?.Report(new(UpdateStage.Write, 40));

                    var bytesWritten = 0;
                    var reportSize = pageSize * 8;
                    var dataMessage = new byte[pageSize + HeaderSize];
                    while (bytesWritten < firmware.Data.Length)
                    {
                        CreateBootloaderMessage(dataMessage, WritePage, bytesWritten, firmware.Data, bytesWritten, pageSize);
                        await BootloaderCommandAsync(bootloader.BaseStream, dataMessage);
                        bytesWritten += pageSize;
                        if (bytesWritten % reportSize == 0)
                        {
                            var percent = 40 + bytesWritten * 50 / firmware.Data.Length;
                            progress?.Report(new(UpdateStage.Write, percent));
                        }
                    }

                    progress?.Report(new(UpdateStage.Restart, 90));
                    CreateBootloaderMessage(dataMessage, ExitBootloader, 0, firmware.Data, 0, pageSize);
                    await BootloaderCommandAsync(bootloader.BaseStream, dataMessage);
                    progress?.Report(new(UpdateStage.Restart, 100));
                    break;
                };
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException ||
                                      ex is TimeoutException || ex is InvalidOperationException)
            {
                if (i < MaxAttempts)
                {
                    await Task.Delay(FlushDelayMilliseconds);
                    continue;
                }

                throw;
            }
        }
    }

    /// <summary>
    /// Asynchronously determines whether a device in bootloader mode is listening on the specified port.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the Harp device.</param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property is <b>true</b> if a device answered the bootloader protocol; otherwise, <b>false</b>.
    /// </returns>
    /// <remarks>
    /// The bootloader restarts its timeout on every byte it receives, so the device is probed exactly
    /// once. Probing repeatedly would hold a device in bootloader mode. The port is opened after a
    /// settle delay, since a port closed moments earlier can still refuse to open, which would
    /// otherwise be reported as the absence of a bootloader.
    /// </remarks>
    public static async Task<bool> IsBootloaderAsync(string portName)
    {
        try
        {
            await Task.Delay(FlushDelayMilliseconds);
            using var bootloader = new SerialPort(portName, DefaultBaudRate, Parity.None, 8, StopBits.One);
            bootloader.Handshake = Handshake.None;
            bootloader.Open();
            await ReadPageSizeAsync(bootloader.BaseStream);
            return true;
        }
        catch (Exception ex) when (ex is HarpException or TimeoutException or IOException or
                                         UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    static ushort GetMessageChecksum(byte[] messageBytes)
    {
        var checksum = (ushort)0;
        unchecked
        {
            for (int i = 0; i < messageBytes.Length - 2; i++)
            {
                checksum += messageBytes[i];
            }
        }
        return checksum;
    }

    static bool IsValidChecksum(byte[] messageBytes)
    {
        var checksum = GetMessageChecksum(messageBytes);
        var messageChecksum = (messageBytes[messageBytes.Length - 1] << 8) + messageBytes[messageBytes.Length - 2];
        return checksum == messageChecksum;
    }

    static void CreateBootloaderMessage(byte[] messageBytes, int opcode, int address, byte[] data, int offset, int count)
    {
        messageBytes[0] = 1;
        messageBytes[1] = 2;
        messageBytes[2] = 3;
        messageBytes[3] = (byte)opcode;
        messageBytes[4] = 0; //error
        messageBytes[5] = (byte)address;
        messageBytes[6] = (byte)(address >> 8);
        messageBytes[7] = (byte)(address >> 16);
        messageBytes[8] = (byte)(address >> 24);
        messageBytes[9] = (byte)count;
        messageBytes[10] = (byte)(count >> 8);
        messageBytes[11] = (byte)(count >> 16);
        messageBytes[12] = (byte)(count >> 24);
        Array.Copy(data, offset, messageBytes, 13, count);
        var checksum = GetMessageChecksum(messageBytes);
        messageBytes[messageBytes.Length - 2] = (byte)checksum;
        messageBytes[messageBytes.Length - 1] = (byte)(checksum >> 8);
    }

    static byte[] CreateBootloaderMessage(int opcode, int address, params byte[] data)
    {
        var messageBytes = new byte[data.Length + HeaderSize];
        CreateBootloaderMessage(messageBytes, opcode, address, data, 0, data.Length);
        return messageBytes;
    }

    static async Task<int> ReadPageSizeAsync(Stream stream)
    {
        var message = CreateBootloaderMessage(ReadPageSize, address: 0);
        await BootloaderCommandAsync(stream, message);
        var pageSize = BitConverter.ToInt32(message, startIndex: 9);
        if (pageSize < MinPageSize || pageSize > MaxPageSize || (pageSize & (pageSize - 1)) != 0)
        {
            throw new HarpException("The device reported an invalid bootloader page size.");
        }

        return pageSize;
    }

    static async Task BootloaderCommandAsync(Stream stream, byte[] message)
    {
        var bytesRead = 0;
        var opcode = message[3];
        await stream.WriteAsync(message, 0, message.Length);
        while (bytesRead < message.Length)
        {
            bytesRead += await stream.ReadAsync(message, bytesRead, message.Length - bytesRead)
                                     .WithTimeout(BootloaderTimeoutMilliseconds);
        }

        if (bytesRead != message.Length)
        {
            throw new HarpException("The device responded with an invalid buffer length.");
        }

        if (message[0] != 1 || message[1] != 2 || message[2] != 3 || message[3] != opcode)
        {
            throw new HarpException("The device did not respond with a bootloader message.");
        }

        if (!IsValidChecksum(message))
        {
            throw new HarpException("The device responded with an invalid response checksum.");
        }

        switch (message[4])
        {
            case NoError: break;
            case UndefinedError: throw new HarpException("The device reported an undefined error while updating the bootloader logic.");
            case InvalidAddress: throw new HarpException("The device reported an invalid address while updating the bootloader logic.");
            case InvalidDataLength: throw new HarpException("The device reported an invalid data length while writing the bootloader page.");
            default: throw new HarpException("The device reported an unknown error while updating the bootloader logic.");
        }
    }
}
