using Bonsai.Harp;

namespace Harp.Toolkit;

/// <summary>
/// Represents the identity reported by the core registers of a Harp device.
/// </summary>
/// <param name="WhoAmI">The identity class of the device.</param>
/// <param name="DeviceName">The name of the device.</param>
/// <param name="HardwareVersion">The hardware version of the device.</param>
/// <param name="FirmwareVersion">The firmware version of the device.</param>
public sealed record DeviceIdentity(
    int WhoAmI,
    string DeviceName,
    HarpVersion HardwareVersion,
    HarpVersion FirmwareVersion)
{
    /// <summary>
    /// Asynchronously reads the identity of the Harp device on the specified connection.
    /// </summary>
    /// <param name="device">The connection to the Harp device.</param>
    /// <param name="timeout">The time to wait, in milliseconds, for the device to answer each read.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is
    /// <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property contains the identity of the device.
    /// </returns>
    /// <exception cref="TimeoutException">The device did not answer a read in time.</exception>
    /// <exception cref="HarpException">The device answered a read with an error.</exception>
    public static async Task<DeviceIdentity> ReadAsync(
        AsyncDevice device,
        int timeout,
        CancellationToken cancellationToken = default)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        var whoAmI = await device.ReadWhoAmIAsync(cancellationToken).WithTimeout(timeout);
        var deviceName = await device.ReadDeviceNameAsync(cancellationToken).WithTimeout(timeout);
        var hardwareVersion = await device.ReadHardwareVersionAsync(cancellationToken).WithTimeout(timeout);
        var firmwareVersion = await device.ReadFirmwareVersionAsync(cancellationToken).WithTimeout(timeout);
        return new DeviceIdentity(whoAmI, deviceName, hardwareVersion, firmwareVersion);
    }
}
