using System.CommandLine;

namespace Harp.Toolkit;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var parseResult = new ToolkitCommand().Parse(args);
        try
        {
            return await parseResult.InvokeAsync(new InvocationConfiguration
            {
                EnableDefaultExceptionHandler = false
            });
        }
        catch (Exception ex)
        {
            var message = DescribeException(ex);
            if (message.Length > 0)
                Console.Error.WriteLine(message);
            return 1;
        }
    }

    static string DescribeException(Exception exception)
    {
        if (exception.TryGetDescription(out var description))
            return description;

        return exception is OperationCanceledException
            ? string.Empty
            : $"Unhandled exception: {exception}";
    }
}
