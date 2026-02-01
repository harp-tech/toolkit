using System.Runtime.CompilerServices;

namespace Harp.Toolkit;

public class Runner
{
    private readonly List<Suite> suites = new();

    public Runner()
    {
    }

    public int TestCount => suites.Sum(s => s.TestCount);

    public IEnumerable<Suite> CollectSuites()
    {
        return suites.AsReadOnly();
    }

    public async IAsyncEnumerable<(Suite Suite, MethodResult Result)> RunAllAsync(string portName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var suite in suites)
        {
            await foreach (var result in suite.RunAllAsync(portName, cancellationToken))
            {
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
