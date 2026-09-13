using System.Text;
using Bonsai.Harp;

namespace Harp.Toolkit.Verify.Suites;

internal class R_CLOCK_CONFIG : Suite
{
    const uint WriteOffsetSeconds = 10;
    const string LockNotImplementedMessage = "Setting the lock state had no effect, so the device does not implement the timestamp lock.";
    const string ClockRoutingMessage = "The device is repeating or generating the synchronization clock, where the effect of CLK_LOCK on writes is unspecified.";
    const ClockConfigurationFlags ClockRouting = ClockConfigurationFlags.ClockRepeater | ClockConfigurationFlags.ClockGenerator;

    public override string Description => "Clock Configuration Register Tests";

    [HarpTest(Description = "Validates that ClockConfig register is readable.")]
    public async Task<IResult> IsReadable(VerifyConnection device)
    {
        return await RegisterHelpers.AssertReadableAsync(a => device.ReadByteAsync(a), ClockConfiguration.Address, "ClockConfig");
    }

    [HarpTest(Description = "Validates that the timestamp register accepts a write while CLK_UNLOCK is set.")]
    public async Task<IResult> UnlockPermitsTimestampWrite(VerifyConnection device)
    {
        if (await IsClockRoutingEnabledAsync(device))
            return new Result<bool>(false, Status.Skipped, ClockRoutingMessage);

        if (!await TrySetClockStateAsync(device, ClockConfigurationFlags.ClockUnlock))
            return new Result<bool>(false, Status.Skipped, LockNotImplementedMessage);

        var target = await device.ReadTimestampSecondsAsync() + WriteOffsetSeconds;
        await device.WriteTimestampSecondsAsync(target);
        var readBack = await device.ReadTimestampSecondsAsync();
        var elapsedSeconds = (long)readBack - target;
        return new AssertionResult(
            elapsedSeconds >= 0 && elapsedSeconds <= 1,
            x => x
                ? $"TimestampSeconds accepted {target} while unlocked."
                : $"Wrote {target} to TimestampSeconds while unlocked and read it back as {readBack}.");
    }

    [HarpTest(Description = "Validates that the timestamp register refuses a write while CLK_LOCK is set.")]
    public async Task<IResult> LockRefusesTimestampWrite(VerifyConnection device)
    {
        if (await IsClockRoutingEnabledAsync(device))
            return new Result<bool>(false, Status.Skipped, ClockRoutingMessage);

        try
        {
            if (!await TrySetClockStateAsync(device, ClockConfigurationFlags.ClockLock))
                return new Result<bool>(false, Status.Skipped, LockNotImplementedMessage);

            var target = await device.ReadTimestampSecondsAsync() + WriteOffsetSeconds;
            var refused = await RegisterHelpers.IsWriteRejectedAsync(
                device, HarpCommand.WriteUInt32(TimestampSeconds.Address, target));
            var readBack = await device.ReadTimestampSecondsAsync();
            return new AssertionResult(
                readBack < target,
                x => x
                    ? "TimestampSeconds kept its value while locked, and the write was " +
                        (refused ? "answered with an error." : "acknowledged without taking effect.")
                    : $"Wrote {target} to TimestampSeconds while locked and it took the value, reading back as {readBack}.");
        }
        finally
        {
            await TrySetClockStateAsync(device, ClockConfigurationFlags.ClockUnlock);
        }
    }

    [HarpTest(Description = "Reports clock synchronization capability: REP_ABLE (bit 3) and GEN_ABLE (bit 4).")]
    public async Task<IResult> ReportSyncCapability(VerifyConnection device)
    {
        var value = (ClockConfigurationFlags)await device.ReadByteAsync(ClockConfiguration.Address);
        bool repAble = value.HasFlag(ClockConfigurationFlags.RepeaterCapability);
        bool genAble = value.HasFlag(ClockConfigurationFlags.GeneratorCapability);
        StringBuilder sb = new StringBuilder("ClockConfig sync capability:");
        sb.Append("\n");
        sb.Append(repAble ? "Device can repeat clock signal" : "Device cannot repeat clock signal");
        sb.Append("\n");
        sb.Append(genAble ? "Device can generate clock signal" : "Device cannot generate clock signal");
        sb.Append("\n");
        return new AssertionResult(
            true,
            sb.ToString());
    }

    static async Task<bool> IsClockRoutingEnabledAsync(VerifyConnection device)
    {
        var configuration = (ClockConfigurationFlags)await device.ReadByteAsync(ClockConfiguration.Address);
        return (configuration & ClockRouting) != 0;
    }

    static async Task<bool> TrySetClockStateAsync(VerifyConnection device, ClockConfigurationFlags state)
    {
        var write = HarpCommand.WriteByte(ClockConfiguration.Address, (byte)state);
        if (await RegisterHelpers.IsWriteRejectedAsync(device, write))
            return false;

        var configuration = (ClockConfigurationFlags)await device.ReadByteAsync(ClockConfiguration.Address);
        return (configuration & state) == state;
    }
}
