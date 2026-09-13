
using Bonsai.Harp;
namespace Harp.Toolkit.Verify.Suites;

internal class R_TIMESTAMP_SECOND : Suite
{
    public override string Description => "Timestamp Seconds Register Tests";

    const string ClockLockedMessage = "The timestamp register is locked (CLK_LOCK), so the device correctly refuses writes.";

    [HarpTest(Description = "Validates that the Timestamp Seconds register is writable.")]
    public async Task<IResult> IsWritable(VerifyConnection device)
    {
        if (await IsClockLockedAsync(device))
            return new Result<bool>(false, Status.Skipped, ClockLockedMessage);

        const uint setSeconds = 42;
        const double maximumElapsedSeconds = 2.0;
        await device.WriteTimestampSecondsAsync(setSeconds);
        await Task.Delay(1);
        HarpMessage response = await device.CommandAsync(TimestampSeconds.FromPayload(MessageType.Read, default));
        double readSeconds = response.GetTimestamp();
        double elapsedSeconds = readSeconds - setSeconds;
        return new AssertionResult(
            elapsedSeconds >= 0 && elapsedSeconds < maximumElapsedSeconds,
            (success) => success
                ? "TimestampSeconds register is writable and updates as expected."
                : $"Wrote {setSeconds} to TimestampSeconds and the reply timestamp was {readSeconds:F6}, " +
                  $"outside the expected range of {setSeconds} to {setSeconds + maximumElapsedSeconds}.");
    }

    [HarpTest(Description = "Validates that TimestampSeconds register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        try
        {
            await device.ReadTimestampSecondsAsync();
            return new AssertionResult(true, "TimestampSeconds is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Validates that TimestampSeconds register is monotonically non-decreasing.")]
    public async Task<IResult> IsMonotonic(VerifyConnection device)
    {
        var first = await device.ReadTimestampSecondsAsync();
        await Task.Delay(100);
        var second = await device.ReadTimestampSecondsAsync();
        return new AssertionResult(
            second >= first,
            x => x
                ? $"TimestampSeconds is monotonic: {first} -> {second}."
                : $"TimestampSeconds decreased from {first} to {second}.");
    }

    [HarpTest(Description = "Validates that writing a past timestamp value takes effect and can be read back.")]
    public async Task<IResult> WritePastValueRoundTrip(VerifyConnection device)
    {
        if (await IsClockLockedAsync(device))
            return new Result<bool>(false, Status.Skipped, ClockLockedMessage);

        const long maximumElapsedSeconds = 1;
        var current = await device.ReadTimestampSecondsAsync();
        var tPast = current >= 10 ? current - 10 : 0u;

        await device.WriteTimestampSecondsAsync(tPast);
        await Task.Delay(50);

        var readBack = await device.ReadTimestampSecondsAsync();
        var elapsedSeconds = (long)readBack - tPast;

        return new AssertionResult(
            elapsedSeconds >= 0 && elapsedSeconds <= maximumElapsedSeconds,
            x => x
                ? $"Wrote {tPast} to TimestampSeconds and read it back as {readBack}."
                : $"Wrote {tPast} to TimestampSeconds and read it back as {readBack}, "
                    + $"outside the expected range of {tPast} to {tPast + maximumElapsedSeconds}.");
    }

    static async Task<bool> IsClockLockedAsync(VerifyConnection device)
    {
        try
        {
            var configuration = (ClockConfigurationFlags)await device.ReadByteAsync(ClockConfiguration.Address);
            return configuration.HasFlag(ClockConfigurationFlags.ClockLock);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
