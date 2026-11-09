namespace Harp.Toolkit;

/// <summary>
/// Represents the result of probing a serial port for a Harp device.
/// </summary>
/// <param name="Status">The outcome of the probe.</param>
/// <param name="Identity">
/// The identity of the device, or <see langword="null"/> if no identity was read.
/// </param>
/// <param name="Exception">
/// The exception that ended the probe, or <see langword="null"/> if the device answered.
/// </param>
public sealed record DeviceProbeResult(
    DeviceProbeStatus Status,
    DeviceIdentity? Identity,
    Exception? Exception)
{
    internal static DeviceProbeResult Responding(DeviceIdentity identity)
        => new(DeviceProbeStatus.Responding, identity, Exception: null);

    internal static DeviceProbeResult Failed(DeviceProbeStatus status, Exception exception)
        => new(status, Identity: null, exception);
}
