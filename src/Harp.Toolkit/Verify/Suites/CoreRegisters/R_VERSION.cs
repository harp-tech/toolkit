using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_VERSION : Suite
{
    public override string Description => "Version Register Tests";

    [HarpTest(Description = "Validates that Version register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
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
    }

    [HarpTest(Description = "Validates that Version register has exactly 32 bytes.")]
    public async Task<IResult> AssertLength(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableArrayAsync(device, Version.Address, Version.RegisterLength, "Version");
        }
    }

    [HarpTest(Description = "Validates that Version register is NOT writable.")]
    public async Task<IResult> IsNotWritable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var req = HarpMessage.FromByte(Version.Address, MessageType.Write, new byte[Version.RegisterLength]);
            var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, req);
            return new AssertionResult(
                rejected,
                x => x
                    ? "Version register correctly rejected write."
                    : "Version register should NOT be writable.");
        }
    }

    [HarpTest(Description = "Reports the version information declared by the device.")]
    public async Task<IResult> ReportVersionInformation(string portName)
    {
        using (var device = new AsyncDevice(portName))
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
}
