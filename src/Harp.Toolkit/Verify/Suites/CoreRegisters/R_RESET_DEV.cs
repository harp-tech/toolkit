using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_RESET_DEV : Suite
{
    public override string Description => "Reset Device Register Tests";

    [HarpTest(Description = "Validates that ResetDev register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableAsync(a => device.ReadByteAsync(a), ResetDevice.Address, "ResetDev");
        }
    }
}
