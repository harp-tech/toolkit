using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_CORE_VERSION_H : Suite
{
    public override string Description => "Core Version High Register Tests";

    [HarpTest(Description = "Validates that CoreVersionHigh matches byte 0 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(Version.Address);
            var registerValue = await device.ReadByteAsync(CoreVersionHigh.Address);
            return new AssertionResult(
                registerValue == versionArray[0],
                x => x
                    ? $"CoreVersionHigh (0x{registerValue:X2}) matches R_VERSION byte 0."
                    : $"CoreVersionHigh (0x{registerValue:X2}) does not match R_VERSION byte 0 (0x{versionArray[0]:X2}).");
        }
    }
}
