using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_HW_VERSION_L : Suite
{
    private const byte address = 0x02;
    public override string Description => "Hardware Version Low Register Tests";

    [HarpTest(Description = "Validates that HwVersionLow matches byte 7 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(0x13);
            var registerValue = await device.ReadByteAsync(address);
            return new AssertionResult(
                registerValue == versionArray[7],
                x => x
                    ? $"HwVersionLow (0x{registerValue:X2}) matches R_VERSION byte 7."
                    : $"HwVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 7 (0x{versionArray[7]:X2}).");
        }
    }
}
