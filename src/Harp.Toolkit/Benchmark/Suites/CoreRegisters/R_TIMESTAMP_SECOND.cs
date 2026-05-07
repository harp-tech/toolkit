
using Bonsai.Harp;
using System.Diagnostics;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_TIMESTAMP_SECOND : Suite
{
    public override string Description => "Timestamp Seconds Register Tests";

    [HarpTest(Description = "Validates that the Timestamp Seconds register is writable.")]
    public async Task<IResult> IsWritable(string portName)
    {
        const uint setSeconds = 42;
        using (var device = new AsyncDevice(portName))
        {
            await device.WriteTimestampSecondsAsync(setSeconds);
            await Task.Delay(1);
            HarpMessage response = await device.CommandAsync(TimestampSeconds.FromPayload(MessageType.Read, default));
            double readSeconds = response.GetTimestamp();
            return new AssertionResult(
                readSeconds - setSeconds < 1.0,
                (success) => success ? $"`TimestampSeconds` register is writable and updates as expected." : $"`TimestampSeconds` register is not writable, Expected value: {setSeconds}, read value: {readSeconds}.");
        }
    }

    [HarpTest(Description = "Validates that TimestampSeconds register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
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
    }

    [HarpTest(Description = "Validates that TimestampSeconds register is monotonically non-decreasing.")]
    public async Task<IResult> IsMonotonic(string portName)
    {
        using (var device = new AsyncDevice(portName))
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
    }

    [HarpTest(Description = "Validates that writing a past timestamp value takes effect and can be read back.")]
    public async Task<IResult> WritePastValueRoundTrip(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var sw = Stopwatch.StartNew();
            var current = await device.ReadTimestampSecondsAsync();
            var tPast = current >= 10 ? current - 10 : 0u;

            await device.WriteTimestampSecondsAsync(tPast);
            await Task.Delay(50);

            var readBack = await device.ReadTimestampSecondsAsync();
            bool withinBounds = Math.Abs((long)readBack - (long)tPast) <= 1;

            return new AssertionResult(
                withinBounds,
                x => x
                    ? $"WritePastValueRoundTrip: wrote {tPast}, read back {readBack} (within 1s tolerance)."
                    : $"WritePastValueRoundTrip: wrote {tPast}, read back {readBack} (difference {Math.Abs((long)readBack - (long)tPast)}s, expected <= 1).");
        }
    }
}
