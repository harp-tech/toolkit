using System.Reflection;
using System.Runtime.CompilerServices;
using Bonsai.Harp;

namespace Harp.Toolkit;


public abstract class Suite
{
    public abstract string Description { get; }

    /// <summary>
    /// Tests whose number and identity is only known at runtime.
    /// During test collection, these will be enumerated and run
    /// after the fixed <see cref="HarpTestAttribute"/> methods.
    /// </summary>
    protected virtual IReadOnlyList<DynamicTest> DynamicTests { get; } = new List<DynamicTest>();

    public int TestCount => CollectTests().Count() + DynamicTests.Count;

    private IEnumerable<(MethodInfo Method, HarpTestAttribute Attribute)> CollectTests()
    {
        return GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<HarpTestAttribute>()!))
            .Where(x => x.Attribute != null);
    }

    public async IAsyncEnumerable<MethodResult> RunAllAsync(string portName, [EnumeratorCancellation] CancellationToken cancellationToken = default, Action<string, string>? onTestStart = null)
    {
        foreach (var (method, attr) in CollectTests())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Notify that test is starting
            onTestStart?.Invoke(method.Name, attr.Description ?? string.Empty);

            IResult testResult;
            try
            {
                object? resultObj = method.Invoke(this, new object[] { portName });
                if (resultObj is Task<IResult> task)
                {
                    testResult = await task;
                }
                else if (resultObj is IResult syncResult)
                {
                    testResult = syncResult;
                }
                else
                {
                    throw new InvalidOperationException($"Test method '{method.Name}' must return IResult or Task<IResult>.");
                }
            }
            catch (Exception ex)
            {
                testResult = new ErrorResult(ex.InnerException ?? ex);
            }
            yield return new MethodResult
            {
                Result = testResult,
                Name = method.Name,
                Description = attr.Description ?? string.Empty
            };
        }

        foreach (var test in DynamicTests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            onTestStart?.Invoke(test.Name, test.Description);

            IResult testResult;
            try
            {
                testResult = await test.Run(portName, cancellationToken);
            }
            catch (Exception ex)
            {
                testResult = new ErrorResult(ex);
            }
            yield return new MethodResult
            {
                Result = testResult,
                Name = test.Name,
                Description = test.Description
            };
        }
    }
}

/// <summary>
/// A test whose name and behavior is determined at runtime rather than declared
/// with <see cref="HarpTestAttribute"/> on a fixed method.
/// </summary>
public record DynamicTest(string Name, string Description, Func<string, CancellationToken, Task<IResult>> Run);

public class SuiteResult
{
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<MethodResult> Results { get; set; } = new();
}

public class MethodResult
{
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public required IResult Result { get; set; }
}
