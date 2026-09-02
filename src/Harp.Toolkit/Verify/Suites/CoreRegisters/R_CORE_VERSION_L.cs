using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_CORE_VERSION_L : Suite
{
    public override string Description => "Core Version Low Register Tests";

    [HarpTest(Description = "Validates that CoreVersionLow matches byte 1 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var versionArray = await device.ReadByteArrayAsync(Version.Address);
            var registerValue = await device.ReadByteAsync(CoreVersionLow.Address);
            return new AssertionResult(
                registerValue == versionArray[1],
                x => x
                    ? $"CoreVersionLow (0x{registerValue:X2}) matches R_VERSION byte 1."
                    : $"CoreVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 1 (0x{versionArray[1]:X2}).");
        }
    }
}
