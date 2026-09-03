
using Bonsai.Harp;
namespace Harp.Toolkit.Benchmark.Suites;

internal class R_UID : Suite
{
    private const byte address = 0x10;
    private const byte expected_length = 16;
    public override string Description => "UID Register Tests";

    [HarpTest(Description = "Validates whether the UID register is 0 and thus likely not in use.")]
    public async Task<IResult> AssertLength(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var value = await device.ReadByteArrayAsync(address);
            return new AssertionResult(
                value.Length == expected_length,
                x => x ?
                    $"Length is 16 as expected." :
                    $"Expected length of register to be 16, got {value.Length} instead");
        }
    }

    [HarpTest(Description = "Checks if the register value is 0, indicating it is likely not used.")]
    public async Task<IResult> AssertReturnsZero(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var value = await device.ReadByteArrayAsync(address);
            string msg = value.All(x => x == 0) ? "Value of all bytes is 0. Register likely not being used" : $"Register returned a non-zero value: {BitConverter.ToString(value)}";
            return new Result<byte[]>(
                value,
                Status.Passed,
                msg);
        }
    }
}
