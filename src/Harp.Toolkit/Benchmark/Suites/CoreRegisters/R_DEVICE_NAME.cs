using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_DEVICE_NAME : Suite
{
    private const byte address = 0x0C;
    private const int expectedLength = 25;
    public override string Description => "Device Name Register Tests";

    [HarpTest(Description = "Validates that DeviceName register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            try
            {
                await device.ReadByteArrayAsync(address);
                return new AssertionResult(true, "DeviceName is readable.");
            }
            catch (Exception ex)
            {
                return new ErrorResult(ex);
            }
        }
    }

    [HarpTest(Description = "Validates that DeviceName register has exactly 25 bytes.")]
    public async Task<IResult> AssertLength(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableArrayAsync(device, address, expectedLength, "DeviceName");
        }
    }
}
