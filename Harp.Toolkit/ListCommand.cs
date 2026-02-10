using System.CommandLine;
using System.IO.Ports;

namespace Harp.Toolkit;

public class ListCommand : Command
{
    public ListCommand()
        : base("list", "List all available system serial ports.")
    {
        SetAction(parseResult =>
        {
            var portNames = SerialPort.GetPortNames();
            Console.WriteLine($"PortNames: [{string.Join(", ", portNames)}]");
        });
    }
}
