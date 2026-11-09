namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// The exception that is thrown when a device keeps running its firmware after the request to
/// restart in BOOTSEL mode.
/// </summary>
/// <remarks>
/// Such a device can only enter BOOTSEL mode with its BOOTSEL button held while it is connected.
/// </remarks>
public sealed class BootselRequestIgnoredException : NotSupportedException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BootselRequestIgnoredException"/> class with
    /// the specified message.
    /// </summary>
    /// <param name="message">The message that describes the ignored request.</param>
    public BootselRequestIgnoredException(string message)
        : base(message)
    {
    }
}
