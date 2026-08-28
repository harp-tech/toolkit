using System.CommandLine;

namespace Harp.Toolkit.Generate;

class GenerateCSharpInterfaceCommand : Command
{
    public GenerateCSharpInterfaceCommand(
        Argument<FileInfo> metadataPathArgument,
        Option<string> namespaceOption,
        Option<DirectoryInfo> outputPathOption)
        : base("csharp", "Generate reactive programming API and async API. This is the default.")
    {
        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var metadataPath = parseResult.GetRequiredValue(metadataPathArgument);
            var ns = parseResult.GetValue(namespaceOption);
            GenerateInterfaceCommand.GenerateCSharpInterface(metadataPath, ns, outputPath);
        });
    }
}
