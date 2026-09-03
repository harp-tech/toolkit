using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_TIMESTAMP_MICRO : Suite
{
    private const byte address = 0x09;
    public override string Description => "Timestamp Microseconds Register Tests";

    [HarpTest(Description = "Validates that TimestampMicro register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            try
            {
                await device.ReadUInt16Async(address);
                return new AssertionResult(true, "TimestampMicro is readable.");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }
    }

    [HarpTest(Description = "Validates that TimestampMicro register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromUInt16(address, MessageType.Write, 0);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "TimestampMicro register correctly rejected write."
                    : "TimestampMicro register should NOT be writable.");
        }
    }

    [HarpTest(Description = "Validates that TimestampMicro value is within bounds (0 to 31249).")]
    public async Task<IResult> ValueWithinBounds(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var microValue = await device.ReadUInt16Async(address);
            return new AssertionResult(
                microValue < 31250,
                x => x
                    ? $"TimestampMicro value ({microValue}) is within expected bounds (< 31250)."
                    : $"TimestampMicro value ({microValue}) exceeds expected maximum (31249).");
        }
    }
}
