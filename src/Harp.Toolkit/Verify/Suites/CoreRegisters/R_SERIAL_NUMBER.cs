using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_SERIAL_NUMBER : Suite
{
    public override string Description => "Serial Number Register Tests";

    [HarpTest(Description = "Validates that SerialNumber register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableAsync(a => device.ReadUInt16Async(a), SerialNumber.Address, "SerialNumber");
    }
}
