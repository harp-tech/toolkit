using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_FW_VERSION_H : Suite
{
    public override string Description => "Firmware Version High Register Tests";

    [HarpTest(Description = "Validates that FwVersionHigh matches byte 3 of R_VERSION.")]
    public async Task<IResult> AssertConsistentWithVersion(VerifyConnection device)
    {
        var versionArray = await device.ReadByteArrayAsync(Version.Address);
        var registerValue = await device.ReadByteAsync(FirmwareVersionHigh.Address);
        return new AssertionResult(
            registerValue == versionArray[3],
            x => x
                ? $"FwVersionHigh (0x{registerValue:X2}) matches R_VERSION byte 3."
                : $"FwVersionHigh (0x{registerValue:X2}) does not match R_VERSION byte 3 (0x{versionArray[3]:X2}).");
    }
}
