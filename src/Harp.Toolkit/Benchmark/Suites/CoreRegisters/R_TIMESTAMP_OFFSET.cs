
using Bonsai.Harp;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_TIMESTAMP_OFFSET : Suite
{
    private const byte address = 0x0F;
    public override string Description => "Timestamp Offset Register Tests";

    [HarpTest(Description = "Validates the deprecated register TimestampOffset returns 0x00.")]
    public async Task<IResult> AssertReturnsZero(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var value = await device.ReadByteAsync(address);
            return new AssertionResult(
                value == 0x00,
                x => x ?
                    $"TimestampOffset register correctly returned 0x00." :
                    $"TimestampOffset register returned a non-zero value (0x{value:X2})");
        }
    }

    [HarpTest(Description = "Validates the deprecated register TimestampOffset is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromByte(address, MessageType.Write, 0x00);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x ?
                    "Device correctly reported an error when trying to write to TimestampOffset register." :
                    "Timestamp Offset register is deprecated and MUST NOT allow writes.");
        }
    }
}
