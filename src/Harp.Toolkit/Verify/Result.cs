
namespace Harp.Toolkit;


public enum Status
{
    Passed,
    Failed,
    Skipped,
    Error
}

public interface IResult
{
    string? Message { get; }

    Status Status { get; }
}

public class ErrorResult(Exception exception) : IResult
{
    public string? Message { get; } = exception.Message;
    public Status Status { get; } = Status.Error;
    public Exception Exception { get; } = exception;
}

public class Result<T> : IResult
{
    public Result(T value, Status status, string message = "")
    {
        Status = status;
        Value = value;
        Message = message;
    }

    public Result(T value, Func<T, bool> predicate, Func<T, bool, string>? messageFactory = null)
    {
        bool evaluation = predicate(value);
        Status = evaluation ? Status.Passed : Status.Failed;
        Value = value;
        Message = messageFactory?.Invoke(value, evaluation) ?? string.Empty;
    }


    public string Message { get; }
    public Status Status { get; }
    public T Value { get; }

    public override string? ToString()
    {
        return $"Result(Status={Status}, Value={Value}, Message={Message})";
    }
}


public class AssertionResult : Result<bool>
{
    public AssertionResult(bool value, string message = "")
        : base(value, value ? Status.Passed : Status.Failed, message)
    {
    }

    public AssertionResult(bool value, Func<bool, string>? messageFactory = null)
        : base(
            value,
            v => v,
            messageFactory is null ? null : ((value, evaluation) => messageFactory(value)))
    {

    }
}


public class NumericBenchmarkResult : Result<double[]>
{

    public NumericBenchmarkResult(double[] values, Status status, string message = "")
        : base(values, status, message)
    {
        Summary = new BenchmarkSummary(values);
    }

    public NumericBenchmarkResult(BenchmarkSummary summary, Status status, string message = "")
        : base(summary.Values, status, message)
    {
        Summary = summary;
    }

    public NumericBenchmarkResult(double[] values, Func<double[], bool> predicate, Func<double[], bool, string>? messageFactory = null)
        : base(values, predicate, messageFactory)
    {
        Summary = new BenchmarkSummary(values);
    }

    public BenchmarkSummary Summary { get; }
}


public class BenchmarkSummary
{
    public readonly double[] Values;


    public BenchmarkSummary(double[] values)
    {
        Values = values ?? Array.Empty<double>();
        // TODO consider copying here since we are mutating
        Array.Sort(Values);
    }

    public double Mean => Values.Length == 0 ? double.NaN : Values.Average();

    public double StdDev
    {
        get
        {
            if (Values.Length == 0) return double.NaN;
            var mean = Mean;
            var sumOfSquares = Values.Sum(v => (v - mean) * (v - mean));
            return Math.Sqrt(sumOfSquares / Values.Length);
        }
    }

    public double Median
    {
        get
        {
            if (Values.Length == 0) return double.NaN;
            int mid = Values.Length / 2;
            if (Values.Length % 2 == 0)
                return (Values[mid - 1] + Values[mid]) / 2.0;
            else
                return Values[mid];
        }
    }

    public double Max => Values.Length == 0 ? double.NaN : Values[Values.Length - 1];

    public double Min => Values.Length == 0 ? double.NaN : Values[0];

    public double Percentile99 => Percentile(0.99);
    public double Percentile01 => Percentile(0.01);

    public double Percentile(double percentile)
    {
        if (Values.Length == 0) return double.NaN;
        if (percentile < 0f || percentile > 1.0f)
        {
            throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 1.");
        }

        double rank = percentile * (Values.Length - 1);
        int lower = (int)Math.Floor(rank);
        int upper = (int)Math.Ceiling(rank);
        if (lower == upper) return Values[lower];
        // Apparently this is how you solve rounding with percentiles
        // https://en.wikipedia.org/wiki/Percentile
        double weight = rank - lower;
        return Values[lower] * (1 - weight) + Values[upper] * weight;
    }
}
