namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// The exception that is thrown when several devices are in BOOTSEL mode and none can be matched
/// to the device to update.
/// </summary>
/// <remarks>
/// Every device stays in BOOTSEL mode, and its previous firmware is still installed.
/// </remarks>
public sealed class AmbiguousBootselDeviceException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AmbiguousBootselDeviceException"/> class with
    /// the specified message.
    /// </summary>
    /// <param name="message">The message that describes the devices found.</param>
    public AmbiguousBootselDeviceException(string message)
        : base(message)
    {
    }
}
