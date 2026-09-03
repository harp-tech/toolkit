using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_TIMESTAMP_MICRO : Suite
{
    public override string Description => "Timestamp Microseconds Register Tests";

    [HarpTest(Description = "Validates that TimestampMicro register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        try
        {
            await device.ReadUInt16Async(TimestampMicroseconds.Address);
            return new AssertionResult(true, "TimestampMicro is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Validates that TimestampMicro register is NOT writable.")]
    public async Task<IResult> IsNotWritable(VerifyConnection device)
    {
        var req = HarpMessage.FromUInt16(TimestampMicroseconds.Address, MessageType.Write, 0);
        var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
        return new AssertionResult(
            rejected,
            x => x
                ? "TimestampMicro register correctly rejected write."
                : "TimestampMicro register should NOT be writable.");
    }

    [HarpTest(Description = "Validates that TimestampMicro value is within bounds (0 to 31249).")]
    public async Task<IResult> ValueWithinBounds(VerifyConnection device)
    {
        var microValue = await device.ReadUInt16Async(TimestampMicroseconds.Address);
        return new AssertionResult(
            microValue < 31250,
            x => x
                ? $"TimestampMicro value ({microValue}) is within expected bounds (< 31250)."
                : $"TimestampMicro value ({microValue}) exceeds expected maximum (31249).");
    }
}
