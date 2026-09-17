using System.CommandLine;

namespace Harp.Toolkit;

public class ListCommand : Command
{
    public ListCommand()
        : base("list", "List all available system serial ports.")
    {
        SetAction(parseResult =>
        {
            var portNames = PortDiscovery.GetCandidatePortNames();
            Console.WriteLine($"PortNames: [{string.Join(", ", portNames)}]");
        });
    }
}
