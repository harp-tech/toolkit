using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_VERSION : Suite
{
    private const byte address = 0x13;
    private const int expectedLength = 32;
    public override string Description => "Version Register Tests";

    [HarpTest(Description = "Validates that Version register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            try
            {
                await device.ReadByteArrayAsync(address);
                return new AssertionResult(true, "Version is readable.");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }
    }

    [HarpTest(Description = "Validates that Version register has exactly 32 bytes.")]
    public async Task<IResult> AssertLength(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableArrayAsync(device, address, expectedLength, "Version");
        }
    }

    [HarpTest(Description = "Validates that Version register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromByte(address, MessageType.Write, 0x00);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "Version register correctly rejected write."
                    : "Version register should NOT be writable.");
        }
    }
}
