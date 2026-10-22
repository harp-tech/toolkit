using System.Diagnostics.CodeAnalysis;
using Harp.Toolkit.Firmware.ATxmega;
using Harp.Toolkit.Firmware.PIC32;

namespace Harp.Toolkit.Firmware.SoundCard;

/// <summary>
/// Represents a firmware image for the PIC32 coprocessor of the SoundCard.
/// </summary>
/// <remarks>
/// The file name of a coprocessor image carries a processor suffix after the device name, either
/// <c>.PIC32</c> or the legacy <c>.X</c>. The suffix is not part of the device name, so the
/// metadata of a coprocessor image equals the metadata of the ATxmega image from the same release.
/// </remarks>
public sealed class CoprocessorFirmware
{
    static readonly string[] ProcessorSuffixes = { ".PIC32", ".X" };

    private CoprocessorFirmware(FirmwareMetadata metadata, FirmwareImage image)
    {
        Metadata = metadata;
        Image = image;
    }

    /// <summary>
    /// Gets information about the firmware version and supported devices on which it can be installed.
    /// </summary>
    public FirmwareMetadata Metadata { get; }

    /// <summary>
    /// Gets the image to program into the coprocessor.
    /// </summary>
    public FirmwareImage Image { get; }

    /// <summary>
    /// Converts the name of a coprocessor firmware file, without its extension, to the metadata
    /// it carries. A return value indicates whether the conversion succeeded.
    /// </summary>
    /// <param name="input">The file name of the coprocessor image, without its extension.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata carried by the name if the conversion
    /// was successful; otherwise, contains <b>null</b>.
    /// </param>
    /// <returns>
    /// <b>true</b> if the name carries a processor suffix and otherwise follows the Harp firmware
    /// naming convention; otherwise, <b>false</b>.
    /// </returns>
    public static bool TryParseMetadata(string input, [NotNullWhen(true)] out FirmwareMetadata? metadata)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var separator = input.IndexOf('-');
        if (separator > 0)
        {
            var device = input.Substring(0, separator);
            foreach (var suffix in ProcessorSuffixes)
            {
                if (device.EndsWith(suffix, StringComparison.Ordinal))
                {
                    var name = device.Substring(0, device.Length - suffix.Length) + input.Substring(separator);
                    return FirmwareMetadata.TryParse(name, out metadata);
                }
            }
        }

        metadata = null;
        return false;
    }

    /// <summary>
    /// Returns the paths at which the coprocessor image for the specified firmware file can sit,
    /// in order of preference.
    /// </summary>
    /// <param name="firmwarePath">The path of the main firmware image.</param>
    /// <returns>
    /// The paths in the folder of <paramref name="firmwarePath"/> whose file name adds a processor
    /// suffix to the device name, or an empty array if the file name has no device name.
    /// </returns>
    /// <remarks>
    /// The coprocessor image of a release sits next to the main image and differs from it only by
    /// the processor suffix, so the two images always come from the same release.
    /// </remarks>
    public static string[] GetPathsFor(string firmwarePath)
    {
        if (firmwarePath == null) throw new ArgumentNullException(nameof(firmwarePath));
        var directory = Path.GetDirectoryName(firmwarePath) ?? string.Empty;
        var fileName = Path.GetFileName(firmwarePath);
        var separator = fileName.IndexOf('-');
        if (separator <= 0)
        {
            return Array.Empty<string>();
        }

        var device = fileName.Substring(0, separator);
        var release = fileName.Substring(separator);
        return Array.ConvertAll(ProcessorSuffixes, suffix => Path.Combine(directory, device + suffix + release));
    }

    /// <summary>
    /// Creates a <see cref="CoprocessorFirmware"/> object from the specified metadata and file
    /// in Intel HEX format.
    /// </summary>
    /// <param name="metadata">The metadata describing the firmware version and supported devices.</param>
    /// <param name="path">The name of the file from which to create the <see cref="CoprocessorFirmware"/>.</param>
    /// <returns>A new <see cref="CoprocessorFirmware"/> object holding the image and the specified metadata.</returns>
    public static CoprocessorFirmware FromFile(FirmwareMetadata metadata, string path)
    {
        return new CoprocessorFirmware(metadata, FirmwareImage.FromFile(path));
    }
}
