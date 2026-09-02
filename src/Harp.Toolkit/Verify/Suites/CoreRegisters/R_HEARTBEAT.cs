using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_HEARTBEAT : Suite
{
    internal const byte Address = 18;
    public override string Description => "Heartbeat Register Tests";

    [HarpTest(Description = "Validates that Heartbeat register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableAsync(a => device.ReadUInt16Async(a), Address, "Heartbeat");
        }
    }

    [HarpTest(Description = "Validates that Heartbeat register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromUInt16(Address, MessageType.Write, 0);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "Heartbeat register correctly rejected write."
                    : "Heartbeat register should NOT be writable.");
        }
    }
}
