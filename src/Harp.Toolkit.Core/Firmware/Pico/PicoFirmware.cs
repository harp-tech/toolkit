namespace Harp.Toolkit.Firmware.Pico;

/// <summary>
/// Represents a firmware image for a device built on the Pico core.
/// </summary>
/// <remarks>
/// The metadata comes from the file name, which follows the Harp firmware naming convention with
/// the <c>.uf2</c> extension.
/// </remarks>
public sealed class PicoFirmware
{
    private PicoFirmware(FirmwareMetadata metadata, FirmwareImage image)
    {
        Metadata = metadata;
        Image = image;
    }

    /// <summary>
    /// Gets information about the firmware version and supported devices on which it can be installed.
    /// </summary>
    public FirmwareMetadata Metadata { get; }

    /// <summary>
    /// Gets the image to write to the flash of the device.
    /// </summary>
    public FirmwareImage Image { get; }

    /// <summary>
    /// Creates a <see cref="PicoFirmware"/> object from the specified metadata and file in UF2 format.
    /// </summary>
    /// <param name="metadata">The metadata describing the firmware version and supported devices.</param>
    /// <param name="path">The name of the file from which to create the <see cref="PicoFirmware"/>.</param>
    /// <returns>A new <see cref="PicoFirmware"/> object holding the image and the specified metadata.</returns>
    public static PicoFirmware FromFile(FirmwareMetadata metadata, string path)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        return new PicoFirmware(metadata, FirmwareImage.FromFile(path));
    }
}
