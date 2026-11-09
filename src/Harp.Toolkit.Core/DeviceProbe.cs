using Bonsai.Harp;

namespace Harp.Toolkit;

/// <summary>
/// Provides a method to read the identity of the Harp device on a serial port.
/// </summary>
public static class DeviceProbe
{
    /// <summary>
    /// Asynchronously reads the identity of the Harp device on the specified port, and reports the
    /// error when no identity was read.
    /// </summary>
    /// <param name="portName">The name of the serial port used to communicate with the Harp device.</param>
    /// <param name="timeout">The time to wait, in milliseconds, for the device to answer each read.</param>
    /// <param name="cancellationToken">
    /// The token to monitor for cancellation requests. The default value is
    /// <see cref="CancellationToken.None"/>.
    /// </param>
    /// <returns>
    /// The task object representing the asynchronous operation. The <see cref="Task{TResult}.Result"/>
    /// property contains the identity of the device, or the error that ended the probe.
    /// </returns>
    /// <remarks>
    /// An ATxmega bootloader restarts its fall-through timer on every received byte, so a probe
    /// holds a device that waits in its bootloader. Probe a port only on request, never every port
    /// in turn.
    /// </remarks>
    public static async Task<DeviceProbeResult> ProbeAsync(
        string portName,
        int timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var device = new AsyncDevice(portName);
            var identity = await DeviceIdentity.ReadAsync(device, timeout, cancellationToken);
            return DeviceProbeResult.Responding(identity);
        }
        catch (TimeoutException ex)
        {
            return DeviceProbeResult.Failed(DeviceProbeStatus.NotResponding, ex);
        }
        catch (HarpException ex)
        {
            return DeviceProbeResult.Failed(DeviceProbeStatus.ProtocolError, ex);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                                         InvalidOperationException or ArgumentException)
        {
            return DeviceProbeResult.Failed(DeviceProbeStatus.PortUnavailable, ex);
        }
    }
}
