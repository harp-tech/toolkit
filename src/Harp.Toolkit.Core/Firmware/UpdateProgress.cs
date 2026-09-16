namespace Harp.Toolkit.Firmware;

/// <summary>
/// Represents the progress of a firmware update.
/// </summary>
/// <param name="Stage">The stage the update reached.</param>
/// <param name="Percent">
/// The percentage of the whole update completed, from 0 to 100. The percentage decreases when
/// the update retries a stage.
/// </param>
public readonly record struct UpdateProgress(UpdateStage Stage, int Percent);
