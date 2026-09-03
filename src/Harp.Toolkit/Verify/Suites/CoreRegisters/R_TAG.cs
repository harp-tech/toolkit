using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_TAG : Suite
{
    private const byte Address = 17;
    private const int ExpectedLength = 8;
    public override string Description => "Tag Register Tests";

    [HarpTest(Description = "Validates that Tag register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        try
        {
            await device.ReadByteArrayAsync(Address);
            return new AssertionResult(true, "Tag is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Validates that Tag register has exactly 8 bytes.")]
    public async Task<IResult> AssertLength(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableArrayAsync(device, Address, ExpectedLength, "Tag");
    }

    [HarpTest(Description = "Validates that Tag register is NOT writable.")]
    public async Task<IResult> IsNotWritable(VerifyConnection device)
    {
        var req = HarpMessage.FromByte(Address, MessageType.Write, new byte[ExpectedLength]);
        var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
        return new AssertionResult(
            rejected,
            x => x
                ? "Tag register correctly rejected write."
                : "Tag register should NOT be writable.");
    }
}
