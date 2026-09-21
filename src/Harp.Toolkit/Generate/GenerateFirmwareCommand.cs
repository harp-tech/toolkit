using System.CommandLine;

namespace Harp.Toolkit.Generate;

class GenerateFirmwareCommand : Command
{
    public GenerateFirmwareCommand()
        : base("firmware", "Generate device firmware code for a target core.")
    {
        Subcommands.Add(new GenerateATxmegaFirmwareCommand());
    }
}
