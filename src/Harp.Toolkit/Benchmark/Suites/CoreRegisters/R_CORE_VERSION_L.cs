using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_CORE_VERSION_L : Suite
{
    private const byte address = 0x05;
    public override string Description => "Core Version Low Register Tests";

    [HarpTest(Description = "Validates that CoreVersionLow matches byte 1 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(0x13);
            var registerValue = await device.ReadByteAsync(address);
            return new AssertionResult(
                registerValue == versionArray[1],
                x => x
                    ? $"CoreVersionLow (0x{registerValue:X2}) matches R_VERSION byte 1."
                    : $"CoreVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 1 (0x{versionArray[1]:X2}).");
        }
    }
}
