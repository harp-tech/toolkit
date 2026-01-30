using System.CommandLine;
using System.IO.Ports;

namespace Harp.Toolkit;

public class ListCommand : Command
{
    public ListCommand()
        : base("list", "Lists all available system serial ports.")
    {
        SetAction(parseResult =>
        {
            var portNames = SerialPort.GetPortNames();
            Console.WriteLine($"PortNames: [{string.Join(", ", portNames)}]");
        });
    }
}
