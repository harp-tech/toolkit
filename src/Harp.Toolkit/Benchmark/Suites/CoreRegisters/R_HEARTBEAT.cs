using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_HEARTBEAT : Suite
{
    private const byte address = 0x12;
    public override string Description => "Heartbeat Register Tests";

    [HarpTest(Description = "Validates that Heartbeat register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableByteAsync(device, address, "Heartbeat");
        }
    }

    [HarpTest(Description = "Validates that Heartbeat register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromByte(address, MessageType.Write, 0x00);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "Heartbeat register correctly rejected write."
                    : "Heartbeat register should NOT be writable.");
        }
    }
}
