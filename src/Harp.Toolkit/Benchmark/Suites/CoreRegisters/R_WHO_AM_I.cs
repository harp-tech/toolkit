
using Bonsai.Harp;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_WHO_AM_I : Suite
{
    public override string Description => "WhoAmI Register Tests";

    [HarpTest(Description = "Validates that the WhoAmI register exists and contains a value.")]
    public async Task<IResult> CheckWhoAmI(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            int value = await device.ReadWhoAmIAsync();
            return new Result<int>(
                value,
                (v) => v > 0 && v < 9999,
                (v, success) => success ? $"WhoAmI register contains valid value: {v}." : $"WhoAmI register contains invalid value: {v}.");
        }
    }

    [HarpTest(Description = "Validates that the WhoAmI register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromUInt16(0x00, MessageType.Write, 0);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x ?
                    "WhoAmI register correctly rejected write." :
                    "WhoAmI register should NOT be writable.");
        }
    }
}
