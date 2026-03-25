using System.Reflection;
using System.Runtime.CompilerServices;
using Bonsai.Harp;

namespace Harp.Toolkit;


public abstract class Suite
{
    public abstract string Description { get; }

    public int TestCount => CollectTests().Count();

    private IEnumerable<(MethodInfo Method, HarpTestAttribute Attribute)> CollectTests()
    {
        return GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(m => (Method: m, Attribute: m.GetCustomAttribute<HarpTestAttribute>()!))
            .Where(x => x.Attribute != null);
    }

    public async IAsyncEnumerable<MethodResult> RunAllAsync(string portName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var (method, attr) in CollectTests())
        {
            cancellationToken.ThrowIfCancellationRequested();

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
    }
}

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
