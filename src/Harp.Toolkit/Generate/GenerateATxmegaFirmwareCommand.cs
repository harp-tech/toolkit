using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateATxmegaFirmwareCommand : Command
{
    public GenerateATxmegaFirmwareCommand()
        : base("atxmega", "Generate firmware headers and implementation stubs.")
    {
        MetadataPathArgument metadataPathArgument = new();
        IOMetadataPathOption iosMetadataPathOption = new();
        OutputPathOption outputPathOption = new();

        Option<bool> generateImplementationOption = new("--implementation")
        {
            Description = "Generate implementation stubs."
        };

        Arguments.Add(metadataPathArgument);
        Options.Add(iosMetadataPathOption);
        Options.Add(generateImplementationOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var generateImplementation = parseResult.GetValue(generateImplementationOption);

            var deviceMetadata = metadataPathArgument.Load(parseResult);
            var portPinMetadata = iosMetadataPathOption.Load(parseResult);
            var generator = new FirmwareGenerator(deviceMetadata, portPinMetadata);
            var headers = generator.GenerateHeaders();
            var implementation = generateImplementation ? generator.GenerateImplementation() : default;
            GeneratorHelper.AssertNoGeneratorErrors(generator.Errors);
            GenerateCommand.WriteFileContents(outputPath.FullName, headers);
            if (generateImplementation)
                GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
        });
    }
}
