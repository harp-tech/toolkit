using System.Buffers.Binary;
using Bonsai.Harp;
using Harp.Toolkit.Usb;

namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// Specifies the microcontroller of a device in BOOTSEL mode.
/// </summary>
internal enum PicoModel
{
    RP2040,
    RP2350
}

/// <summary>
/// Represents a connection to the PICOBOOT interface that the RP2040 and RP2350 boot ROM
/// exposes in BOOTSEL mode.
/// </summary>
/// <remarks>
/// The protocol is the one implemented by picotool and documented in the RP2040 and RP2350
/// datasheets. Every command is a 32-byte packet on the bulk OUT endpoint, followed by an
/// optional data phase and an acknowledgement in the opposite direction to the data.
/// </remarks>
internal sealed class PicobootDevice : IDisposable
{
    public const ushort VendorId = 0x2E8A;
    public const ushort ProductIdRP2040 = 0x0003;
    public const ushort ProductIdRP2350 = 0x000F;

    public const uint FlashStart = 0x10000000;
    public const int PageSize = 256;

    const uint Magic = 0x431FD10B;
    const int CommandSize = 32;
    const int StatusSize = 16;

    const byte ExclusiveAccess = 0x01;
    const byte Reboot = 0x02;
    const byte FlashErase = 0x03;
    const byte Read = 0x84;
    const byte Write = 0x05;
    const byte ExitXip = 0x06;
    const byte EnterCommandXip = 0x07;
    const byte Reboot2 = 0x0A;

    const byte InterfaceReset = 0x41;
    const byte InterfaceCommandStatus = 0x42;

    const byte Exclusive = 1;
    const uint RP2040StackPointer = 0x20042000;
    const uint RebootDelayMilliseconds = 500;

    const uint ControlTimeout = 1000;
    const uint CommandTimeout = 3000;
    const uint DataTimeout = 10000;

    readonly UsbDeviceHandle handle;
    readonly int interfaceNumber;
    readonly byte outEndpoint;
    readonly byte inEndpoint;
    uint token = 1;

    PicobootDevice(UsbDeviceHandle handle, PicoModel model, string? serialNumber, int interfaceNumber, byte outEndpoint, byte inEndpoint)
    {
        this.handle = handle;
        this.interfaceNumber = interfaceNumber;
        this.outEndpoint = outEndpoint;
        this.inEndpoint = inEndpoint;
        Model = model;
        SerialNumber = serialNumber;
    }

    /// <summary>
    /// Gets the microcontroller of the device.
    /// </summary>
    public PicoModel Model { get; }

    /// <summary>
    /// Gets the USB serial number reported by the boot ROM, or <see langword="null"/> if it reports none.
    /// </summary>
    public string? SerialNumber { get; }

    /// <summary>
    /// Returns whether the specified USB device is a boot ROM in BOOTSEL mode, and its model.
    /// </summary>
    public static bool TryGetModel(UsbDevice device, out PicoModel model)
    {
        var descriptor = device.GetDescriptor();
        model = descriptor.idProduct == ProductIdRP2350 ? PicoModel.RP2350 : PicoModel.RP2040;
        return descriptor.idVendor == VendorId &&
               (descriptor.idProduct == ProductIdRP2040 || descriptor.idProduct == ProductIdRP2350);
    }

    /// <summary>
    /// Opens the PICOBOOT interface of the specified boot ROM.
    /// </summary>
    /// <exception cref="UsbException">
    /// The device could not be opened. On Windows, the usual cause is a PICOBOOT interface with no
    /// WinUSB driver.
    /// </exception>
    public static PicobootDevice Open(UsbDevice device, PicoModel model)
    {
        var descriptor = device.GetDescriptor();
        var handle = device.Open();
        try
        {
            var configuration = handle.GetConfigurationDescriptor(ControlTimeout);
            if (!TryFindInterface(configuration, out var interfaceNumber, out var outEndpoint, out var inEndpoint))
            {
                throw new HarpException("The device in BOOTSEL mode has no PICOBOOT interface.");
            }

            var serialNumber = handle.GetStringDescriptor(descriptor.iSerialNumber);
            handle.ClaimInterface(interfaceNumber);
            var picoboot = new PicobootDevice(handle, model, serialNumber, interfaceNumber, outEndpoint, inEndpoint);
            picoboot.ResetInterface();
            return picoboot;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Finds the vendor-specific interface with one bulk OUT and one bulk IN endpoint in the
    /// specified configuration descriptor.
    /// </summary>
    internal static bool TryFindInterface(
        ReadOnlySpan<byte> configuration,
        out int interfaceNumber,
        out byte outEndpoint,
        out byte inEndpoint)
    {
        const byte InterfaceDescriptor = 0x04;
        const byte EndpointDescriptor = 0x05;
        const byte VendorSpecificClass = 0xFF;
        const byte BulkTransfer = 0x02;

        interfaceNumber = -1;
        outEndpoint = inEndpoint = 0;
        var candidate = -1;
        byte candidateOut = 0, candidateIn = 0;
        for (int offset = 0; offset + 2 <= configuration.Length;)
        {
            var length = configuration[offset];
            if (length < 2 || offset + length > configuration.Length)
            {
                break;
            }

            var type = configuration[offset + 1];
            if (type == InterfaceDescriptor && length >= 9)
            {
                if (candidate >= 0 && candidateOut != 0 && candidateIn != 0)
                {
                    break;
                }

                candidate = configuration[offset + 5] == VendorSpecificClass ? configuration[offset + 2] : -1;
                candidateOut = candidateIn = 0;
            }
            else if (type == EndpointDescriptor && length >= 7 && candidate >= 0)
            {
                var address = configuration[offset + 2];
                var attributes = configuration[offset + 3];
                if ((attributes & 0x03) == BulkTransfer)
                {
                    if ((address & LibUsb.EndpointIn) != 0) candidateIn = address;
                    else candidateOut = address;
                }
            }

            offset += length;
        }

        if (candidate >= 0 && candidateOut != 0 && candidateIn != 0)
        {
            interfaceNumber = candidate;
            outEndpoint = candidateOut;
            inEndpoint = candidateIn;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Creates the 32-byte packet of a PICOBOOT command.
    /// </summary>
    internal static byte[] CreateCommand(uint token, byte commandId, uint transferLength, ReadOnlySpan<byte> arguments)
    {
        if (arguments.Length > 16) throw new ArgumentOutOfRangeException(nameof(arguments));
        var command = new byte[CommandSize];
        var span = command.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(span[4..], token);
        command[8] = commandId;
        command[9] = (byte)arguments.Length;
        BinaryPrimitives.WriteUInt32LittleEndian(span[12..], transferLength);
        arguments.CopyTo(span[16..]);
        return command;
    }

    static byte[] RangeArguments(uint address, uint size)
    {
        var arguments = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(arguments, address);
        BinaryPrimitives.WriteUInt32LittleEndian(arguments.AsSpan(4), size);
        return arguments;
    }

    void ResetInterface()
    {
        try { handle.ClearHalt(inEndpoint); } catch (UsbException) { }
        try { handle.ClearHalt(outEndpoint); } catch (UsbException) { }
        handle.ControlTransfer(
            LibUsb.RequestTypeVendor | LibUsb.RecipientInterface,
            InterfaceReset, 0, (ushort)interfaceNumber, data: null, ControlTimeout);
    }

    (uint StatusCode, byte CommandId) ReadStatus()
    {
        var status = new byte[StatusSize];
        handle.ControlTransfer(
            LibUsb.EndpointIn | LibUsb.RequestTypeVendor | LibUsb.RecipientInterface,
            InterfaceCommandStatus, 0, (ushort)interfaceNumber, status, ControlTimeout);
        return (BinaryPrimitives.ReadUInt32LittleEndian(status.AsSpan(4)), status[8]);
    }

    void Execute(byte commandId, ReadOnlySpan<byte> arguments, byte[]? data = null)
    {
        var transferLength = (uint)(data?.Length ?? 0);
        var command = CreateCommand(token++, commandId, transferLength, arguments);
        try
        {
            handle.BulkTransfer(outEndpoint, command, command.Length, CommandTimeout);
            if (data is not null && data.Length > 0)
            {
                var endpoint = (commandId & 0x80) != 0 ? inEndpoint : outEndpoint;
                var transferred = handle.BulkTransfer(endpoint, data, data.Length, DataTimeout);
                if (transferred != data.Length)
                {
                    throw new IOException("The PICOBOOT data transfer was incomplete.");
                }
            }

            var acknowledgement = new byte[1];
            var ackEndpoint = (commandId & 0x80) != 0 ? outEndpoint : inEndpoint;
            handle.BulkTransfer(ackEndpoint, acknowledgement, 1, transferLength == 0 ? DataTimeout : CommandTimeout);
        }
        catch (UsbException ex) when (ex.ErrorCode == LibUsb.ErrorPipe)
        {
            var (statusCode, statusCommand) = ReadStatus();
            throw new HarpException(
                $"The boot ROM rejected PICOBOOT command 0x{statusCommand:X2} with status {statusCode}.");
        }
    }

    /// <summary>
    /// Takes exclusive access to flash, which stops the mass storage drive from writing it.
    /// </summary>
    public void TakeExclusiveAccess()
    {
        Execute(ExclusiveAccess, stackalloc byte[] { Exclusive });
    }

    /// <summary>
    /// Prepares flash for erase and write commands.
    /// </summary>
    public void PrepareFlashWrite()
    {
        Execute(ExitXip, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>
    /// Prepares flash for read commands after a write.
    /// </summary>
    /// <remarks>
    /// The RP2040 reads flash back through XIP. The RP2350 reads flash back while XIP is off, and
    /// reads only zeros through XIP.
    /// </remarks>
    public void PrepareFlashRead()
    {
        if (Model == PicoModel.RP2040)
        {
            Execute(EnterCommandXip, ReadOnlySpan<byte>.Empty);
        }
        else
        {
            Execute(ExitXip, ReadOnlySpan<byte>.Empty);
        }
    }

    public void EraseFlash(uint address, uint size)
    {
        Execute(FlashErase, RangeArguments(address, size));
    }

    public void WriteMemory(uint address, byte[] data)
    {
        Execute(Write, RangeArguments(address, (uint)data.Length), data);
    }

    public byte[] ReadMemory(uint address, int size)
    {
        var data = new byte[size];
        Execute(Read, RangeArguments(address, (uint)size), data);
        return data;
    }

    /// <summary>
    /// Reboots the device into the application in flash, after a short delay that lets the
    /// command be acknowledged.
    /// </summary>
    public void RebootToApplication()
    {
        Span<byte> arguments = stackalloc byte[16];
        if (Model == PicoModel.RP2040)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(arguments, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(arguments[4..], RP2040StackPointer);
            BinaryPrimitives.WriteUInt32LittleEndian(arguments[8..], RebootDelayMilliseconds);
            Execute(Reboot, arguments[..12]);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(arguments, 0);
            BinaryPrimitives.WriteUInt32LittleEndian(arguments[4..], RebootDelayMilliseconds);
            Execute(Reboot2, arguments);
        }
    }

    public void Dispose()
    {
        handle.ReleaseInterface(interfaceNumber);
        handle.Dispose();
    }
}
