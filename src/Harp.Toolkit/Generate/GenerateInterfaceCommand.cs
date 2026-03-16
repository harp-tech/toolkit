using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateInterfaceCommand : Command
{
    public GenerateInterfaceCommand()
        : base("interface", "Generate reactive programming API and async API.")
    {
        MetadataPathArgument metadataPathArgument = new();
        OutputPathOption outputPathOption = new();
        Option<string> namespaceOption = new("-ns", "--namespace")
        {
            Description = "The namespace for the generated code. The default is `Harp.DeviceName`."
        };

        Arguments.Add(metadataPathArgument);
        Options.Add(namespaceOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var metadataPath = parseResult.GetRequiredValue(metadataPathArgument);
            var ns = parseResult.GetValue(namespaceOption);

            var deviceMetadata = GeneratorHelper.ReadDeviceMetadata(metadataPath.FullName);
            var generator = new InterfaceGenerator(deviceMetadata, ns ?? $"Harp.{deviceMetadata.Device}");
            var implementation = generator.GenerateImplementation();
            if (GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
                GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
        });
    }
}
