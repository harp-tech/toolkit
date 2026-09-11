using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_VERSION : Suite
{
    public override string Description => "Version Register Tests";

    [HarpTest(Description = "Validates that Version register is readable.", Prerelease = true)]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        try
        {
            await device.ReadByteArrayAsync(Version.Address);
            return new AssertionResult(true, "Version is readable.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Validates that Version register has exactly 32 bytes.", Prerelease = true)]
    public async Task<IResult> AssertLength(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableArrayAsync(device, Version.Address, Version.RegisterLength, "Version");
    }

    [HarpTest(Description = "Validates that Version register is NOT writable.", Prerelease = true)]
    public async Task<IResult> IsNotWritable(VerifyConnection device)
    {
        var req = HarpMessage.FromByte(Version.Address, MessageType.Write, new byte[Version.RegisterLength]);
        var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
        return new AssertionResult(
            rejected,
            x => x
                ? "Version register correctly rejected write."
                : "Version register should NOT be writable.");
    }

    [HarpTest(Description = "Validates that Version register declares the protocol major version being checked.", Prerelease = true)]
    public async Task<IResult> AssertDeclaresCheckedMajorVersion(VerifyConnection device)
    {
        try
        {
            var reply = await device.CommandAsync(HarpCommand.ReadByte(Version.Address));
            var payload = reply.GetPayloadArray<byte>();
            if (payload.Length != Version.RegisterLength)
            {
                return new AssertionResult(
                    false,
                    $"Version returned {payload.Length} bytes, expected {Version.RegisterLength}.");
            }

            var declared = Version.GetPayload(reply).ProtocolVersion;
            return new AssertionResult(
                declared.Major == ProtocolReference.PrereleaseMajorVersion,
                x => x
                    ? $"Version declares protocol {declared}, matching the major version being checked."
                    : $"Version declares protocol {declared}, but these checks are against major "
                        + $"version {ProtocolReference.PrereleaseMajorVersion}.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }

    [HarpTest(Description = "Reports the version information declared by the device.", Prerelease = true)]
    public async Task<IResult> ReportVersionInformation(VerifyConnection device)
    {
        try
        {
            var reply = await device.CommandAsync(HarpCommand.ReadByte(Version.Address));
            var payload = reply.GetPayloadArray<byte>();
            if (payload.Length != Version.RegisterLength)
            {
                return new AssertionResult(
                    false,
                    $"Version returned {payload.Length} bytes, expected {Version.RegisterLength}.");
            }

            var version = Version.GetPayload(reply);
            return new Result<VersionPayload>(
                version,
                Status.Passed,
                $"PROTOCOL {version.ProtocolVersion}, FIRMWARE {version.FirmwareVersion}, " +
                $"HARDWARE {version.HardwareVersion}, CORE_ID {version.CoreId}, " +
                $"INTERFACE_HASH {Convert.ToHexString(version.InterfaceHash)}.");
        }
        catch (Exception ex)
        {
            return new ErrorResult(ex);
        }
    }
}
