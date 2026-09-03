using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_FW_VERSION_L : Suite
{
    private const byte address = 0x07;
    public override string Description => "Firmware Version Low Register Tests";

    [HarpTest(Description = "Validates that FwVersionLow matches byte 4 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(0x13);
            var registerValue = await device.ReadByteAsync(address);
            return new AssertionResult(
                registerValue == versionArray[4],
                x => x
                    ? $"FwVersionLow (0x{registerValue:X2}) matches R_VERSION byte 4."
                    : $"FwVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 4 (0x{versionArray[4]:X2}).");
        }
    }
}
