using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_FW_VERSION_H : Suite
{
    private const byte address = 0x06;
    public override string Description => "Firmware Version High Register Tests";

    [HarpTest(Description = "Validates that FwVersionHigh matches byte 3 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(0x13);
            var registerValue = await device.ReadByteAsync(address);
            return new AssertionResult(
                registerValue == versionArray[3],
                x => x
                    ? $"FwVersionHigh (0x{registerValue:X2}) matches R_VERSION byte 3."
                    : $"FwVersionHigh (0x{registerValue:X2}) does not match R_VERSION byte 3 (0x{versionArray[3]:X2}).");
        }
    }
}
