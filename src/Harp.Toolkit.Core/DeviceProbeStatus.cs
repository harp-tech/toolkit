namespace Harp.Toolkit;

/// <summary>
/// Specifies the outcome of probing a serial port for a Harp device.
/// </summary>
public enum DeviceProbeStatus
{
    /// <summary>
    /// The device answered every identity read.
    /// </summary>
    Responding,

    /// <summary>
    /// The port opened, but the device did not answer in time. The port may hold a device that is
    /// not a Harp device, or a Harp device that is still starting.
    /// </summary>
    NotResponding,

    /// <summary>
    /// The device answered an identity read with an error.
    /// </summary>
    ProtocolError,

    /// <summary>
    /// The port could not be opened.
    /// </summary>
    PortUnavailable
}
