
using Bonsai.Harp;
namespace Harp.Toolkit;

public class RoundTripTestSuite : Suite
{
    private double maxRoundTripDelayMs;
    public RoundTripTestSuite(double maxRoundTripDelayMs = 4.0)
    {
        this.maxRoundTripDelayMs = maxRoundTripDelayMs;
    }

    public override string Description => "A bunch of tests to benchmark round trip read/writes.";

    [HarpTest(Description = "Benchmarks the round trip time for a WhoAmI read command.")]
    public async Task<IResult> BenchmarkRoundTrip(string portName)
    {
        const int n = 1000;
        double[] timestamps = new double[n];
        HarpMessage probe = WhoAmI.FromPayload(MessageType.Read, default);
        using (var device = new AsyncDevice(portName))
        {
            for (int i = 0; i < n; i++)
            {
                var reply = await device.CommandAsync(probe);
                timestamps[i] = reply.GetTimestamp();
            }
        }
        var derivatives = timestamps
            .Zip(timestamps.Skip(1), (previous, current) => (current - previous) * 1e3)
            .ToArray();
        var benchmark = new BenchmarkSummary(derivatives);
        if (benchmark.Max > maxRoundTripDelayMs)
        {
            return new NumericBenchmarkResult(benchmark, Status.Failed, $"Round trip WhoAmI read benchmark exceeded maximum allowed delay of {maxRoundTripDelayMs} ms.");
        }
        else
        {
            return new NumericBenchmarkResult(benchmark, Status.Passed, "Round trip WhoAmI read benchmark.");
        }
    }
}
