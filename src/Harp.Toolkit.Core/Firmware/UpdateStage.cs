namespace Harp.Toolkit.Firmware;

/// <summary>
/// Specifies the stage of a firmware update.
/// </summary>
/// <remarks>
/// An update reports a stage when it starts that stage. The core controls both the order of
/// the stages and the set of stages in an update.
/// </remarks>
public enum UpdateStage
{
    /// <summary>
    /// Connects to the device.
    /// </summary>
    Connect,

    /// <summary>
    /// Checks that the firmware image supports the device.
    /// </summary>
    Check,

    /// <summary>
    /// Resets the device into the bootloader. The device does not answer Harp commands from
    /// this stage until an update completes.
    /// </summary>
    Reset,

    /// <summary>
    /// Connects to the bootloader. A second report of this stage marks a retry.
    /// </summary>
    Bootloader,

    /// <summary>
    /// Writes the firmware image to the device.
    /// </summary>
    Write,

    /// <summary>
    /// Leaves the bootloader. The new firmware then starts.
    /// </summary>
    Restart
}
