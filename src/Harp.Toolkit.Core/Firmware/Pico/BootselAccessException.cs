namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// The exception that is thrown when the system does not allow the boot interface of a device in
/// BOOTSEL mode to be opened, because a driver or a permission is missing.
/// </summary>
/// <remarks>
/// The device stays in BOOTSEL mode, and its previous firmware is still installed.
/// </remarks>
public sealed class BootselAccessException : IOException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BootselAccessException"/> class with the
    /// specified message and the exception that caused it.
    /// </summary>
    /// <param name="message">The message that describes the missing driver or permission.</param>
    /// <param name="innerException">The exception reported by libusb.</param>
    public BootselAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
