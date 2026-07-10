namespace Harp.Toolkit.Benchmark;

/// <summary>
/// Options for clock alignment and PPS synchronization tests run against a reference clock device.
/// </summary>
/// <param name="ClockPort">
/// Serial port of the reference clock device (WhiteRabbit). Enabling this option runs the
/// simultaneous WhoAmI timestamp comparison test.
/// </param>
/// <param name="PpsAddress">
/// Register address on the tested device that emits an event whenever the incoming PPS signal
/// goes high. When provided, also runs the PPS alignment test.
/// </param>
/// <param name="ClockSamples">Number of PPS event pairs to collect for the PPS alignment test.</param>
internal record ClockTestOptions(
    string ClockPort,
    int? PpsAddress = null,
    int ClockSamples = 5);
