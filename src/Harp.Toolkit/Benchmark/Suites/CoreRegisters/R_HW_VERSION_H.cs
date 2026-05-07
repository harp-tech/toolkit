using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_HW_VERSION_H : Suite
{
    private const byte address = 0x01;
    public override string Description => "Hardware Version High Register Tests";

    [HarpTest(Description = "Validates that HwVersionHigh matches byte 6 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(0x13);
            var registerValue = await device.ReadByteAsync(address);
            return new AssertionResult(
                registerValue == versionArray[6],
                x => x
                    ? $"HwVersionHigh (0x{registerValue:X2}) matches R_VERSION byte 6."
                    : $"HwVersionHigh (0x{registerValue:X2}) does not match R_VERSION byte 6 (0x{versionArray[6]:X2}).");
        }
    }
}
