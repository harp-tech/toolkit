using Bonsai.Harp;

namespace Harp.Toolkit.Benchmark.Suites;

internal class R_CLOCK_CONFIG : Suite
{
    private const byte address = 0x0E;
    public override string Description => "Clock Configuration Register Tests";

    [HarpTest(Description = "Validates that ClockConfig register is readable.")]
    public async Task<IResult> IsReadable(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            return await RegisterHelpers.AssertReadableByteAsync(device, address, "ClockConfig");
        }
    }

    [HarpTest(Description = "Reports clock synchronization capability: REP_ABLE (bit 3) and GEN_ABLE (bit 4).")]
    public async Task<IResult> ReportSyncCapability(string portName)
    {
        using (var device = new AsyncDevice(portName))
        {
            var value = await device.ReadByteAsync(address);
            bool repAble = (value & (1 << 3)) != 0;
            bool genAble = (value & (1 << 4)) != 0;
            return new AssertionResult(
                true,
                $"ClockConfig sync capability: REP_ABLE={repAble}, GEN_ABLE={genAble}.");
        }
    }
}
