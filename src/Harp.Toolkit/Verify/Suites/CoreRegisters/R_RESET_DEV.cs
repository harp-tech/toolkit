using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_RESET_DEV : Suite
{
    const ResetFlags BootProvenance = ResetFlags.BootFromDefault | ResetFlags.BootFromEeprom;

    public override string Description => "Reset Device Register Tests";

    [HarpTest(Description = "Validates that ResetDev register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableAsync(a => device.ReadByteAsync(a), ResetDevice.Address, "ResetDev");
    }

    [HarpTest(Description = "Validates that a read of ResetDev clears every command bit and reports exactly one boot provenance bit.")]
    public async Task<IResult> AssertBootProvenanceReported(VerifyConnection device)
    {
        var value = await device.ReadByteAsync(ResetDevice.Address);
        var flags = (ResetFlags)value;
        return new AssertionResult(
            flags == ResetFlags.BootFromDefault || flags == ResetFlags.BootFromEeprom,
            x => x
                ? $"ResetDev read 0x{value:X2}, reporting {DescribeBootSource(flags)}."
                : $"ResetDev read 0x{value:X2}. {DescribeReadViolation(flags)}");
    }

    [HarpTest(Description = "Validates that ResetDev rejects a write setting BOOT_DEF, which is read-only state.")]
    public async Task<IResult> BootFromDefaultIsNotWritable(VerifyConnection device)
    {
        return await AssertReadOnlyBitRejectedAsync(device, ResetFlags.BootFromDefault, "BOOT_DEF");
    }

    [HarpTest(Description = "Validates that ResetDev rejects a write setting BOOT_EE, which is read-only state.")]
    public async Task<IResult> BootFromEepromIsNotWritable(VerifyConnection device)
    {
        return await AssertReadOnlyBitRejectedAsync(device, ResetFlags.BootFromEeprom, "BOOT_EE");
    }

    static async Task<IResult> AssertReadOnlyBitRejectedAsync(VerifyConnection device, ResetFlags bit, string bitName)
    {
        var request = HarpMessage.FromByte(ResetDevice.Address, MessageType.Write, (byte)bit);
        var rejected = await RegisterHelpers.IsWriteRejectedAsync(device, request);
        return new AssertionResult(
            rejected,
            x => x
                ? $"ResetDev correctly rejected a write setting {bitName}."
                : $"ResetDev accepted a write setting {bitName}, which is read-only state and must be answered with an error reply.");
    }

    static string DescribeBootSource(ResetFlags flags)
    {
        return flags == ResetFlags.BootFromEeprom
            ? "a boot from register values stored in non-volatile memory"
            : "a boot from default register values";
    }

    static string DescribeReadViolation(ResetFlags flags)
    {
        var provenance = flags & BootProvenance;
        if (provenance == BootProvenance)
            return "Both BOOT_DEF and BOOT_EE are set, so the reported boot provenance is contradictory.";
        if (provenance == 0)
            return "Neither BOOT_DEF nor BOOT_EE is set, so no boot provenance is reported. A device without non-volatile memory must always set BOOT_DEF.";

        var commandBits = DescribeCommandBits(flags);
        if (commandBits.Length > 0)
            return $"A read reply must clear every command bit, but {commandBits} remained set.";

        var remaining = (byte)(flags & ~BootProvenance);
        return $"A read reply must clear every bit outside the boot provenance field, but 0x{remaining:X2} remained set.";
    }

    static string DescribeCommandBits(ResetFlags flags)
    {
        var names = new List<string>();
        if (flags.HasFlag(ResetFlags.RestoreDefault)) names.Add("RST_DEF");
        if (flags.HasFlag(ResetFlags.RestoreEeprom)) names.Add("RST_EE");
        if (flags.HasFlag(ResetFlags.Save)) names.Add("SAVE");
        if (flags.HasFlag(ResetFlags.RestoreName)) names.Add("NAME_TO_DEFAULT");
        return string.Join(", ", names);
    }
}
