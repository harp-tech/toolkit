using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_TAG : Suite
{
    private const byte address = 0x11;
    private const int expectedLength = 8;
    public override string Description => "Tag Register Tests";

    [HarpTest(Description = "Validates that Tag register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            try
            {
                await device.ReadByteArrayAsync(address);
                return new AssertionResult(true, "Tag is readable.");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }
    }

    [HarpTest(Description = "Validates that Tag register has exactly 8 bytes.")]
    public async Task<IResult> AssertLength(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableArrayAsync(device, address, expectedLength, "Tag");
        }
    }

    [HarpTest(Description = "Validates that Tag register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromByte(address, MessageType.Write, 0x00);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "Tag register correctly rejected write."
                    : "Tag register should NOT be writable.");
        }
    }
}
