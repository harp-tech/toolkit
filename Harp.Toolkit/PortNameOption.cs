using System.CommandLine;

namespace Harp.Toolkit;

public class PortNameOption : Option<string>
{
    public PortNameOption()
        : base("--port")
    {
        Description = "Specifies the name of the serial port used to communicate with the device.";
        Required = true;
    }
}
