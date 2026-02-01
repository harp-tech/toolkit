
using Bonsai.Harp;
namespace Harp.Toolkit;

public class TimestampSecondsSuite : Suite
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
}
