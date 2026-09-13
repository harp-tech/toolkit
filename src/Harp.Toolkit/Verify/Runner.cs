using System.Runtime.CompilerServices;

namespace Harp.Toolkit.Verify;

public class Runner
{
    private readonly List<Suite> suites = new();
    private readonly bool includePrerelease;

    public Runner(bool includePrerelease)
    {
        this.includePrerelease = includePrerelease;
    }

    public int TestCount => suites.Sum(s => s.GetTestCount(includePrerelease));

    public int PrereleaseTestCount => suites.Sum(s => s.GetPrereleaseTestCount());

    public IEnumerable<Suite> CollectSuites()
    {
        return suites.AsReadOnly();
    }

    public async IAsyncEnumerable<(Suite Suite, MethodResult Result)> RunAllAsync(VerifyConnection connection, [EnumeratorCancellation] CancellationToken cancellationToken = default, Action<Suite, string, string>? onTestStart = null)
    {
        foreach (var suite in suites)
        {
            await foreach (var result in suite.RunAllAsync(connection, includePrerelease, cancellationToken, (testName, testDesc) => onTestStart?.Invoke(suite, testName, testDesc)))
            {
                if (result.Result is ErrorResult { Exception: TimeoutException timeout })
                {
                    throw timeout;
                }

                yield return (suite, result);
            }
        }
    }

    public void AddSuite(Suite suite)
    {
        if (suite == null)
        {
            throw new ArgumentNullException(nameof(suite));
        }
        suites.Add(suite);
    }

    public void ClearSuites()
    {
        suites.Clear();
    }

    public bool RemoveSuite(Suite suite)
    {
        if (suite == null)
        {
            throw new ArgumentNullException(nameof(suite));
        }
        return suites.Remove(suite);
    }
}
