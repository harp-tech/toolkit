using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_RESET_DEV : Suite
{
    private const byte address = 0x0B;
    public override string Description => "Reset Device Register Tests";

    [HarpTest(Description = "Validates that ResetDev register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableByteAsync(device, address, "ResetDev");
        }
    }
}
