namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// The exception that is thrown when a firmware image does not target the microcontroller of a
/// device in BOOTSEL mode.
/// </summary>
/// <remarks>
/// The update restarts the device before throwing, so the device runs its previous firmware.
/// </remarks>
public sealed class UnsupportedImageException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnsupportedImageException"/> class with the
    /// specified message.
    /// </summary>
    /// <param name="message">The message that describes the mismatch.</param>
    public UnsupportedImageException(string message)
        : base(message)
    {
    }
}
