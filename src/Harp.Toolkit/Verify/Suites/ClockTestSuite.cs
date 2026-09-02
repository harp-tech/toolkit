
using Bonsai.Harp;
using Harp.Toolkit.Verify;

namespace Harp.Toolkit.Verify.Suites;

internal class ClockTestSuite : Suite
{
    private readonly ClockTestOptions? options;

    public ClockTestSuite(ClockTestOptions? options)
    {
        this.options = options;
    }

    public override string Description => "Tests clock alignment and PPS synchronization accuracy against a reference clock device.";

    [HarpTest(Description = "Sends 100 simultaneous WhoAmI reads to both devices and compares embedded timestamps to measure clock offset.")]
    public async Task<IResult> SimultaneousWhoAmI(string portName)
    {
        if (options is null)
            return new Result<bool>(false, Status.Skipped, "No clock port provided (--clock-port).");

        const int n = 100;
        double[] deltas = new double[n];
        var probe = WhoAmI.FromPayload(MessageType.Read, default);

        using var testedDevice = new AsyncDevice(portName);
        using var clockDevice = new AsyncDevice(options.ClockPort);

        for (int i = 0; i < n; i++)
        {
            var results = await Task.WhenAll(testedDevice.CommandAsync(probe), clockDevice.CommandAsync(probe));
            deltas[i] = results[0].GetTimestamp() - results[1].GetTimestamp();
            await Task.Delay(Random.Shared.Next(20, 70));
        }

        var summary = new BenchmarkSummary(deltas);
        return new NumericBenchmarkResult(
            summary,
            Status.Passed,
            $"Clock offset: mean={summary.Mean:F6}s ({summary.Mean * 1e3:F3}ms), " +
            $"stddev={summary.StdDev:F6}s ({summary.StdDev * 1e3:F3}ms)");
    }

    [HarpTest(Description = "Subscribes to PPS events on both devices and compares timestamps to measure hardware clock synchronization accuracy.")]
    public async Task<IResult> PpsEventAlignment(string portName)
    {
        if (options is null)
            return new Result<bool>(false, Status.Skipped, "No clock port provided (--clock-port).");
        if (!options.PpsEvent.HasValue)
            return new Result<bool>(false, Status.Skipped, "No tested device register provided (--pps-event).");

        // The clock device (WhiteRabbit) emits a TimestampSecond event on every PPS tick.
        // ALIVE_EN (0x80) | OP_MODE (0x01) enables those events.
        // TODO: consider also supporting Heartbeat via HEARTBEAT_EN (0x04) | OP_MODE (0x01).
        const int clockDeviceReg = TimestampSeconds.Address;
        const byte clockDeviceOpCtrl = 0x81;

        var listenDuration = TimeSpan.FromSeconds(options.ClockSamples + 5);
        var allMessages = await Task.WhenAll(
            RegisterHelpers.WriteToTransportAsync(
                options.ClockPort,
                [HarpMessage.FromByte(OperationControl.Address, MessageType.Write, clockDeviceOpCtrl)],
                listenDuration),
            RegisterHelpers.WriteToTransportAsync(
                portName,
                [HarpMessage.FromByte(OperationControl.Address, MessageType.Write, 0x01)],
                listenDuration));

        var clockEvents = allMessages[0]
            .Where(m => m.MessageType == MessageType.Event && m.Address == clockDeviceReg)
            .Take(options.ClockSamples).ToList();
        var testedEvents = allMessages[1]
            .Where(m => m.MessageType == MessageType.Event && m.Address == options.PpsEvent!.Value)
            .Take(options.ClockSamples).ToList();

        int pairCount = Math.Min(clockEvents.Count, testedEvents.Count);
        if (pairCount == 0)
            return new AssertionResult(false,
                $"No event pairs received within {listenDuration.TotalSeconds}s " +
                $"(clock register {clockDeviceReg}, tested register {options.PpsEvent!.Value}).");

        var deltas = clockEvents
            .Zip(testedEvents, (c, t) => c.GetTimestamp() - t.GetTimestamp())
            .ToArray();
        var summary = new BenchmarkSummary(deltas);
        return new NumericBenchmarkResult(
            summary,
            Status.Passed,
            $"PPS delta ({pairCount} pairs): mean={summary.Mean:F6}s ({summary.Mean * 1e3:F3}ms), " +
            $"stddev={summary.StdDev:F6}s ({summary.StdDev * 1e3:F3}ms)");
    }
}
