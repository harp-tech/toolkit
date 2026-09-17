using System.CommandLine;

namespace Harp.Toolkit.Generate;

class GenerateInterfaceCommand : Command
{
    public GenerateInterfaceCommand()
        : base("interface", "Generate device interface code for a target language.")
    {
        Subcommands.Add(new GenerateCSharpInterfaceCommand());
        Subcommands.Add(new GeneratePythonInterfaceCommand());
    }
}
