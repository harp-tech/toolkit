using System.Text;
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
            return await RegisterHelpers.AssertReadableAsync(a => device.ReadByteAsync(a), address, "ClockConfig");
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
    }
}
