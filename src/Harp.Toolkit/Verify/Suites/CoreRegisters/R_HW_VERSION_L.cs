using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_HW_VERSION_L : Suite
{
    public override string Description => "Hardware Version Low Register Tests";

    [HarpTest(Description = "Validates that HwVersionLow matches byte 7 of R_VERSION.", Prerelease = true)]
    public async Task<IResult> AssertConsistentWithVersion(VerifyConnection device)
    {
        var versionArray = await device.ReadByteArrayAsync(Version.Address);
        var registerValue = await device.ReadByteAsync(HardwareVersionLow.Address);
        return new AssertionResult(
            registerValue == versionArray[7],
            x => x
                ? $"HwVersionLow (0x{registerValue:X2}) matches R_VERSION byte 7."
                : $"HwVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 7 (0x{versionArray[7]:X2}).");
    }
}
