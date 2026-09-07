
using Bonsai.Harp;
namespace Harp.Toolkit.Verify.Suites;

internal class R_TIMESTAMP_OFFSET : Suite
{
    private const byte Address = 15;
    public override string Description => "Timestamp Offset Register Tests";

    [HarpTest(Description = "Validates the deprecated register TimestampOffset returns 0x00.", Prerelease = true)]
    public async Task<IResult> AssertReturnsZero(VerifyConnection device)
    {
        var value = await device.ReadByteAsync(Address);
        return new AssertionResult(
            value == 0x00,
            x => x ?
                "TimestampOffset register correctly returned 0x00." :
                $"TimestampOffset register returned a non-zero value (0x{value:X2}).");
    }

    [HarpTest(Description = "Validates the deprecated register TimestampOffset is NOT writable.", Prerelease = true)]
    public async Task<IResult> IsNotWritable(VerifyConnection device)
    {
        var req = HarpMessage.FromByte(Address, MessageType.Write, 0x00);
        var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
        return new AssertionResult(
            rejected,
            x => x ?
                "Device correctly reported an error when trying to write to TimestampOffset register." :
                "Timestamp Offset register is deprecated and MUST NOT allow writes.");
    }
}
