using System.CommandLine;
using Harp.Generators;

namespace Harp.Toolkit.Generate;

class GenerateFirmwareCommand : Command
{
    public GenerateFirmwareCommand()
        : base("firmware", "Generate firmware headers and implementation template.")
    {
        MetadataPathArgument metadataPathArgument = new();
        IOMetadataPathOption iosMetadataPathOption = new();
        OutputPathOption outputPathOption = new();

        Option<bool> generateImplementationOption = new("--implementation")
        {
            Description = "Indicates whether to generate implementation (.c) files. The default is false."
        };

        Arguments.Add(metadataPathArgument);
        Options.Add(iosMetadataPathOption);
        Options.Add(generateImplementationOption);
        Options.Add(outputPathOption);

        SetAction(parseResult =>
        {
            var outputPath = parseResult.GetRequiredValue(outputPathOption);
            var registerMetadataFileName = parseResult.GetRequiredValue(metadataPathArgument).FullName;
            var iosMetadataFileName = parseResult.GetRequiredValue(iosMetadataPathOption).FullName;
            var generateImplementation = parseResult.GetValue(generateImplementationOption);

            var deviceMetadata = GeneratorHelper.ReadDeviceMetadata(registerMetadataFileName);
            var portPinMetadata = GeneratorHelper.ReadPortPinMetadata(iosMetadataFileName);
            var generator = new FirmwareGenerator(deviceMetadata, portPinMetadata);
            var headers = generator.GenerateHeaders();
            var implementation = generateImplementation ? generator.GenerateImplementation() : default;
            if (GeneratorHelper.AssertNoGeneratorErrors(generator.Errors))
            {
                GenerateCommand.WriteFileContents(outputPath.FullName, headers);
                if (generateImplementation)
                    GenerateCommand.WriteFileContents(outputPath.FullName, implementation);
            }
        });
    }
}
