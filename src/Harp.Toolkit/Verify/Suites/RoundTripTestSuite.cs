
using System.Diagnostics;
using Bonsai.Harp;
namespace Harp.Toolkit.Verify.Suites;

internal class RoundTripTestSuite : Suite
{
    public override string Description => "A bunch of tests to benchmark round trip read/writes.";

    [HarpTest(Description = "Benchmarks the round trip time for a WhoAmI read command.")]
    public async Task<IResult> BenchmarkRoundTrip(VerifyConnection device)
    {
        const int n = 1000;
        double[] elapsed = new double[n];
        HarpMessage probe = Bonsai.Harp.WhoAmI.FromPayload(MessageType.Read, default);
        var clock = new Stopwatch();
        for (int i = 0; i < n; i++)
        {
            clock.Restart();
            await device.CommandAsync(probe);
            elapsed[i] = clock.Elapsed.TotalMilliseconds;
        }
        var benchmark = new BenchmarkSummary(elapsed);
        return new NumericBenchmarkResult(benchmark, Status.Passed,
            $"Round trip WhoAmI read latency over {n} samples, in milliseconds.");
    }
}
