using System.CommandLine;

namespace Harp.Toolkit;

public class PortTimeoutOption : Option<int>
{
    public PortTimeoutOption()
        : base("--timeout")
    {
        Description = "Specifies the timeout, in milliseconds, to receive a response from the device. Use -1 to wait indefinitely.";
        DefaultValueFactory = _ => 2000;
        Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < -1)
                result.AddError("The timeout must be -1 to wait indefinitely, or a value in milliseconds.");
        });
    }
}
