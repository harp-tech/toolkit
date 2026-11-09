namespace Harp.Toolkit.Firmware.ATxmega;

/// <summary>
/// Represents a firmware image for a device built on the ATxmega core.
/// </summary>
public sealed class ATxmegaFirmware
{
    private ATxmegaFirmware(FirmwareMetadata metadata, FirmwareImage image)
    {
        Metadata = metadata;
        Image = image;
    }

    /// <summary>
    /// Gets information about the firmware version and supported devices on which it can be installed.
    /// </summary>
    public FirmwareMetadata Metadata { get; }

    /// <summary>
    /// Gets the image to write to the device.
    /// </summary>
    public FirmwareImage Image { get; }

    /// <summary>
    /// Creates an <see cref="ATxmegaFirmware"/> object from the specified file in Intel HEX format,
    /// with the metadata taken from the file name.
    /// </summary>
    /// <param name="path">The name of the file from which to create the <see cref="ATxmegaFirmware"/>.</param>
    /// <returns>
    /// A new <see cref="ATxmegaFirmware"/> object holding the image and the metadata from the file name.
    /// </returns>
    public static ATxmegaFirmware FromFile(string path)
    {
        var metadata = FirmwareMetadata.Parse(Path.GetFileNameWithoutExtension(path));
        return FromFile(metadata, path);
    }

    /// <summary>
    /// Creates an <see cref="ATxmegaFirmware"/> object from the specified metadata and file in
    /// Intel HEX format.
    /// </summary>
    /// <param name="metadata">The metadata describing the firmware version and supported devices.</param>
    /// <param name="path">The name of the file from which to create the <see cref="ATxmegaFirmware"/>.</param>
    /// <returns>A new <see cref="ATxmegaFirmware"/> object holding the image and the specified metadata.</returns>
    public static ATxmegaFirmware FromFile(FirmwareMetadata metadata, string path)
    {
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        return new ATxmegaFirmware(metadata, FirmwareImage.FromFile(path));
    }
}
