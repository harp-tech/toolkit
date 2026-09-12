namespace Harp.Toolkit;

static class PortErrors
{
    const string AvailablePortsHint = "Run 'harp.toolkit list' to see the serial ports available on this system.";

    public static Task<int> ReportErrorsAsync(this PortNameOption portOption, string portName, Func<Task> action)
    {
        return portOption.ReportErrorsAsync(portName, async () =>
        {
            await action();
            return 0;
        });
    }

    public static async Task<int> ReportErrorsAsync(this PortNameOption portOption, string portName, Func<Task<int>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (portOption.TryDescribe(ex, portName, out var message))
        {
            Console.Error.WriteLine(message);
            return 1;
        }
    }

    public static bool TryDescribe(this PortNameOption portOption, Exception exception, string portName, out string message)
    {
        switch (exception)
        {
            case ArgumentException argument when argument.ParamName == "portName":
                message = $"The value {portName} specified with {portOption.Name} is not a valid serial port name. {AvailablePortsHint}";
                return true;
            case FileNotFoundException notFound when notFound.FileName == portName:
                message = $"The serial port {portName} specified with {portOption.Name} was not found. {AvailablePortsHint}";
                return true;
            case UnauthorizedAccessException when ContainsPortName(exception, portName):
                message = $"Access to the serial port {portName} specified with {portOption.Name} was denied. Another program may have it open.";
                return true;
            case TimeoutException:
                message = $"The device on the serial port {portName} specified with {portOption.Name} did not respond in time.";
                return true;
            default:
                message = string.Empty;
                return false;
        }
    }

    static bool ContainsPortName(Exception exception, string portName)
    {
        return exception.Message.Contains(portName, StringComparison.OrdinalIgnoreCase);
    }
}
