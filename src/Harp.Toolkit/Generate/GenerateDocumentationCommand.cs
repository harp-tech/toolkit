using System.CommandLine;

namespace Harp.Toolkit.Generate;

class GenerateDocumentationCommand : Command
{
    public GenerateDocumentationCommand()
        : base("documentation", "Generate device reference documentation for a target site generator.")
    {
        Subcommands.Add(new GenerateDocfxDocumentationCommand());
    }
}
