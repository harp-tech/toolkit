using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateInterfaceCommand : Command
{
    public GenerateInterfaceCommand()
        : base("interface", "Generate reactive programming API and async API.")
    {
        MetadataPathArgument metadataPathArgument = new();
        Option<string> namespaceOption = new("-ns", "--namespace")
        {
            Description = "The namespace for the generated code. The default is `Harp.DeviceName`.",
            Recursive = true
        };
        OutputPathOption outputPathOption = new() { Recursive = true };

        Arguments.Add(metadataPathArgument);
        Options.Add(namespaceOption);
        Options.Add(outputPathOption);
        Subcommands.Add(new GenerateCSharpInterfaceCommand(
            metadataPathArgument, namespaceOption, outputPathOption));
        Subcommands.Add(new GeneratePythonInterfaceCommand(
            metadataPathArgument, namespaceOption, outputPathOption));

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var metadataPath = parseResult.GetRequiredValue(metadataPathArgument);
            var ns = parseResult.GetValue(namespaceOption);
            GenerateCSharpInterface(metadataPath, ns, outputPath);
        });
    }

    internal static void GenerateCSharpInterface(FileInfo metadataPath, string? ns, DirectoryInfo outputPath)
    {
        var deviceMetadata = GeneratorHelper.ReadDeviceMetadata(metadataPath.FullName);
        var generator = new InterfaceGenerator(deviceMetadata, ns ?? $"Harp.{deviceMetadata.Device}");
        var implementation = generator.GenerateImplementation();
        if (GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
            GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
    }
}
