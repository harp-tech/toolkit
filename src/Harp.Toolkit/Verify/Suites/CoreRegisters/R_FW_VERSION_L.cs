using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_FW_VERSION_L : Suite
{
    public override string Description => "Firmware Version Low Register Tests";

    [HarpTest(Description = "Validates that FwVersionLow matches byte 4 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(VerifyConnection device)
    {
        var versionArray = await device.ReadByteArrayAsync(Version.Address);
        var registerValue = await device.ReadByteAsync(FirmwareVersionLow.Address);
        return new AssertionResult(
            registerValue == versionArray[4],
            x => x
                ? $"FwVersionLow (0x{registerValue:X2}) matches R_VERSION byte 4."
                : $"FwVersionLow (0x{registerValue:X2}) does not match R_VERSION byte 4 (0x{versionArray[4]:X2}).");
    }
}
