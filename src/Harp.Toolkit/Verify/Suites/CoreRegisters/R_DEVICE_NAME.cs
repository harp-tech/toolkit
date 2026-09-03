using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_DEVICE_NAME : Suite
{
    public override string Description => "Device Name Register Tests";

    [HarpTest(Description = "Validates that DeviceName register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        try
        {
            await device.ReadByteArrayAsync(DeviceName.Address);
            return new AssertionResult(true, "DeviceName is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Validates that DeviceName register has exactly 25 bytes.")]
    public async Task<IResult> AssertLength(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableArrayAsync(device, DeviceName.Address, DeviceName.RegisterLength, "DeviceName");
    }
}
