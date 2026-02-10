using System.CommandLine;

namespace Harp.Toolkit;

public class PortTimeoutOption : Option<int?>
{
    public PortTimeoutOption()
        : base("--timeout")
    {
        Description = "Specifies an optional timeout, in milliseconds, to receive a response from the device.";
    }
}
